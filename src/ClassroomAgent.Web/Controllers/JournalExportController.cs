using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Application.Validation;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The journal export (US-028 openapi <c>POST /api/v1/exports/journal-xlsx</c>; spec FR-001, FR-005, FR-006, FR-008):
/// the report page's script posts a JSON body with the antiforgery header and saves the file it gets back. Policy
/// <c>UseReportTemplates</c> — Admin and Dean (§2 "Просмотр и экспорт журнала успеваемости").
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3): the content type, the JSON syntax of api-design §2.3, the API-6 bodies and the file
/// response. Every rule — validation, existence, the report, the workbook, the audit row — is
/// <see cref="ExportJournalCommand"/>. The actor, the role and the language come from the session (spec VR-002).
/// </remarks>
[Authorize(Policy = InstallationPolicies.UseReportTemplates)]
public sealed class JournalExportController(ExportJournalCommand export, ILogger<JournalExportController> logger) : Controller
{
    public const string Route = "/api/v1/exports/journal-xlsx";

    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>The body members of openapi <c>JournalExportRequest</c>; any other member is ignored and never echoed.</summary>
    private static readonly string[] Members = ["template", "courseId", "from", "to", "names", "orientation"];

    [HttpPost(Route)]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        // Api-design §3: content type after antiforgery (a filter, already passed), then body syntax.
        if (!Request.HasJsonContentType())
        {
            return Refuse(StatusCodes.Status415UnsupportedMediaType, JournalExportTextKeys.RequestMalformed, "ContentType");
        }

        var body = await ReadBodyAsync(cancellationToken);
        if (body is null)
        {
            return Refuse(StatusCodes.Status400BadRequest, JournalExportTextKeys.RequestMalformed, "RequestMalformed");
        }

        var request = new JournalExportRequest(
            body.GetValueOrDefault("template"),
            body.GetValueOrDefault("courseId"),
            body.GetValueOrDefault("from"),
            body.GetValueOrDefault("to"),
            body.GetValueOrDefault("names"),
            body.GetValueOrDefault("orientation"));

        var actorId = ActorId();
        var result = await export.ExecuteAsync(
            request, actorId, ActorRole(), CultureInfo.CurrentUICulture, HttpContext.TraceIdentifier, cancellationToken);

        if (result.Outcome != JournalExportOutcome.Exported || result.File is not { } file || result.Summary is not { } summary)
        {
            var status = result.Outcome == JournalExportOutcome.NotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            JournalExportLog.Refused(
                logger,
                HttpContext.TraceIdentifier,
                status,
                string.Join(", ", result.Errors.Select(e => e.Field + ":" + e.Key)));
            var errors = result.Errors.Select(e => (FieldName(e.Field), MessageKey(e.Key))).ToList();
            return ApiErrorResponse.Result(ApiErrorResponse.Create(HttpContext, status, errors[0].Item2, errors));
        }

        JournalExportLog.Exported(
            logger,
            summary.Template,
            summary.CourseId,
            summary.From,
            summary.To,
            actorId,
            NameSourceCode.Of(summary.NameSource),
            summary.NameSourceOrigin.ToString(),
            summary.RowCount,
            summary.TopicCount,
            file.Content.Length);

        // Api-design §2.7: attachment with filename and filename* (RFC 6266); Cache-Control: no-store comes from the
        // host-wide rule (SC-14).
        var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
        disposition.SetHttpFileName(file.FileName);
        Response.Headers.ContentDisposition = disposition.ToString();
        return File(file.Content, XlsxContentType);
    }

    /// <summary>
    /// Api-design §2.3: a JSON object whose known members are strings, none repeated; anything else is a syntax
    /// failure (null) — no field value can be named honestly.
    /// </summary>
    private async Task<Dictionary<string, string>?> ReadBodyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(Request.Body, default, cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in document.RootElement.EnumerateObject())
            {
                if (!seen.Add(member.Name))
                {
                    return null;
                }

                if (Array.IndexOf(Members, member.Name) < 0)
                {
                    continue;
                }

                if (member.Value.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                values[member.Name] = member.Value.GetString()!;
            }

            return values;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private JsonResult Refuse(int status, string messageKey, string rule)
    {
        JournalExportLog.Refused(logger, HttpContext.TraceIdentifier, status, rule);
        return ApiErrorResponse.Result(ApiErrorResponse.Create(HttpContext, status, messageKey));
    }

    /// <summary>Openapi <c>fieldErrors[].field</c>: the body member's own name.</summary>
    private static string FieldName(ExportField field) => field switch
    {
        ExportField.Template => "template",
        ExportField.CourseId => "courseId",
        ExportField.From => "from",
        ExportField.To => "to",
        ExportField.Names => "names",
        ExportField.Orientation => "orientation",
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };

    /// <summary>Openapi <c>ExportMessageKey</c>: the report page's own messages (spec VR-001), plus the orientation's.</summary>
    private static string MessageKey(ExportMessageKey key) => key switch
    {
        ExportMessageKey.TemplateMalformed => ReportTemplateTextKeys.Message(ReportMessageKey.TemplateMalformed),
        ExportMessageKey.TemplateNotFound => ReportTemplateTextKeys.Message(ReportMessageKey.TemplateNotFound),
        ExportMessageKey.CourseMalformed => ReportTemplateTextKeys.Message(ReportMessageKey.CourseMalformed),
        ExportMessageKey.CourseUnknown => ReportTemplateTextKeys.Message(ReportMessageKey.CourseUnknown),
        ExportMessageKey.FromMalformed => ReportTemplateTextKeys.Message(ReportMessageKey.FromMalformed),
        ExportMessageKey.ToMalformed => ReportTemplateTextKeys.Message(ReportMessageKey.ToMalformed),
        ExportMessageKey.PeriodInverted => ReportTemplateTextKeys.Message(ReportMessageKey.PeriodInverted),
        ExportMessageKey.NameSourceMalformed => ReportTemplateTextKeys.Message(ReportMessageKey.NameSourceMalformed),
        ExportMessageKey.OrientationInvalid => JournalExportTextKeys.OrientationInvalid,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };

    /// <summary>Spec VR-002: the signed-in account from the session, never a request value; the policy guarantees one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            CultureInfo.InvariantCulture);

    private AppRole ActorRole() =>
        Enum.TryParse<AppRole>(User.FindFirstValue(ClaimTypes.Role), out var role) && Enum.IsDefined(role)
            ? role
            : throw new InvalidOperationException("An authorized request carries no known role.");
}
