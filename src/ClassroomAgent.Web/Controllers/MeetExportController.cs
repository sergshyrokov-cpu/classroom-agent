using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The Meet exports (US-044 openapi <c>POST /api/v1/exports/meet-reports-xlsx</c> and
/// <c>POST /api/v1/exports/meet-meeting-xlsx</c>; spec FR-001, FR-008, FR-009): the Meet pages' script posts a JSON body
/// with the antiforgery header and saves the file it gets back. Policy <c>ViewMeetReports</c> — Admin and Dean (§2
/// "Просмотр и экспорт статистики Meet-занятий").
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3): the content type, the JSON syntax of api-design §2.2, the API-6 bodies and the file
/// response. Every rule — validation, existence, the reports, the workbook, the audit row — is
/// <see cref="ExportMeetReportsCommand"/> or <see cref="ExportMeetMeetingCommand"/>. The actor, the role and the language
/// come from the session (spec VR-004).
/// </remarks>
[Authorize(Policy = InstallationPolicies.ViewMeetReports)]
public sealed class MeetExportController(
    ExportMeetReportsCommand reports,
    ExportMeetMeetingCommand meeting,
    ILogger<MeetExportController> logger) : Controller
{
    public const string ReportsRoute = "/api/v1/exports/meet-reports-xlsx";

    public const string MeetingRoute = "/api/v1/exports/meet-meeting-xlsx";

    /// <summary>The body members of openapi <c>MeetReportsExportRequest</c>; any other member is ignored and never echoed.</summary>
    private static readonly string[] ReportsMembers = ["courseId", "from", "to", "orientation"];

    /// <summary>The body members of openapi <c>MeetMeetingExportRequest</c>.</summary>
    private static readonly string[] MeetingMembers = ["courseId", "meetingId", "orientation"];

    [HttpPost(ReportsRoute)]
    public async Task<IActionResult> ExportReports(CancellationToken cancellationToken)
    {
        var (body, refusal) = await ReadAsync(ReportsMembers, cancellationToken);
        if (body is null)
        {
            return refusal!;
        }

        var actorId = ActorId();
        var result = await reports.ExecuteAsync(
            new MeetReportsExportRequest(
                body.GetValueOrDefault("courseId"),
                body.GetValueOrDefault("from"),
                body.GetValueOrDefault("to"),
                body.GetValueOrDefault("orientation")),
            actorId,
            ActorRole(),
            CultureInfo.CurrentUICulture,
            HttpContext.TraceIdentifier,
            cancellationToken);
        if (Refused(result) is { } refused)
        {
            return refused;
        }

        var summary = result.Summary!;
        MeetExportLog.ReportsExported(
            logger,
            summary.CourseId,
            Date(summary.From!.Value),
            Date(summary.To!.Value),
            actorId,
            summary.Rows,
            summary.Meetings!.Value,
            summary.Orientation,
            result.File!.Content.Length);
        return FileOf(result.File);
    }

    [HttpPost(MeetingRoute)]
    public async Task<IActionResult> ExportMeeting(CancellationToken cancellationToken)
    {
        var (body, refusal) = await ReadAsync(MeetingMembers, cancellationToken);
        if (body is null)
        {
            return refusal!;
        }

        var actorId = ActorId();
        var result = await meeting.ExecuteAsync(
            new MeetMeetingExportRequest(
                body.GetValueOrDefault("courseId"),
                body.GetValueOrDefault("meetingId"),
                body.GetValueOrDefault("orientation")),
            actorId,
            ActorRole(),
            CultureInfo.CurrentUICulture,
            HttpContext.TraceIdentifier,
            cancellationToken);
        if (Refused(result) is { } refused)
        {
            return refused;
        }

        var summary = result.Summary!;
        MeetExportLog.MeetingExported(
            logger,
            summary.MeetingId!.Value,
            summary.CourseId,
            actorId,
            summary.Rows,
            summary.Orientation,
            result.File!.Content.Length);
        return FileOf(result.File);
    }

    /// <summary>Api-design §2.4: content type after antiforgery (a filter, already passed), then body syntax.</summary>
    private async Task<(Dictionary<string, string>? Body, IActionResult? Refusal)> ReadAsync(
        string[] members, CancellationToken cancellationToken)
    {
        if (!Request.HasJsonContentType())
        {
            return (null, Refuse(StatusCodes.Status415UnsupportedMediaType, "ContentType"));
        }

        var body = await ReadBodyAsync(members, cancellationToken);
        return body is null
            ? (null, Refuse(StatusCodes.Status400BadRequest, "RequestMalformed"))
            : (body, null);
    }

    /// <summary>
    /// Api-design §2.2: a JSON object whose known members are strings, none repeated; anything else is a syntax failure
    /// (null) — no field value can be named honestly.
    /// </summary>
    private async Task<Dictionary<string, string>?> ReadBodyAsync(string[] members, CancellationToken cancellationToken)
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

                if (Array.IndexOf(members, member.Name) < 0)
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

    /// <summary>Spec FR-009: <c>400</c> or <c>404</c> with the API-6 body naming every failing field; the value never.</summary>
    private JsonResult? Refused(MeetExportResult result)
    {
        if (result.Outcome == MeetExportOutcome.Exported && result.File is not null && result.Summary is not null)
        {
            return null;
        }

        var status = result.Outcome == MeetExportOutcome.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;
        MeetExportLog.Refused(
            logger,
            HttpContext.TraceIdentifier,
            status,
            string.Join(", ", result.Errors.Select(e => e.Field + ":" + e.Key)));
        var errors = result.Errors.Select(e => (FieldName(e.Field), MessageKey(e.Key))).ToList();
        return ApiErrorResponse.Result(ApiErrorResponse.Create(HttpContext, status, errors[0].Item2, errors));
    }

    private JsonResult Refuse(int status, string rule)
    {
        MeetExportLog.Refused(logger, HttpContext.TraceIdentifier, status, rule);
        return ApiErrorResponse.Result(ApiErrorResponse.Create(HttpContext, status, JournalExportTextKeys.RequestMalformed));
    }

    /// <summary>
    /// Spec FR-008: attachment with <c>filename</c> and <c>filename*</c> (RFC 6266); <c>Cache-Control: no-store</c>
    /// comes from the host-wide rule (SC-14).
    /// </summary>
    private FileContentResult FileOf(JournalExportFile file)
    {
        var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
        disposition.SetHttpFileName(file.FileName);
        Response.Headers.ContentDisposition = disposition.ToString();
        return File(file.Content, JournalExportController.XlsxContentType);
    }

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Openapi <c>fieldErrors[].field</c>: the body member's own name.</summary>
    private static string FieldName(MeetExportField field) => field switch
    {
        MeetExportField.CourseId => "courseId",
        MeetExportField.From => "from",
        MeetExportField.To => "to",
        MeetExportField.MeetingId => "meetingId",
        MeetExportField.Orientation => "orientation",
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };

    /// <summary>Api-design §2.3: the Meet pages' own messages, the orientation's, and one new for a malformed meeting id.</summary>
    private static string MessageKey(MeetExportMessageKey key) => key switch
    {
        MeetExportMessageKey.CourseMalformed => MeetReportsTextKeys.Validation(MeetReportsMessageKey.CourseMalformed),
        MeetExportMessageKey.CourseUnknown => MeetReportsTextKeys.Validation(MeetReportsMessageKey.CourseUnknown),
        MeetExportMessageKey.FromMalformed => MeetReportsTextKeys.Validation(MeetReportsMessageKey.FromMalformed),
        MeetExportMessageKey.ToMalformed => MeetReportsTextKeys.Validation(MeetReportsMessageKey.ToMalformed),
        MeetExportMessageKey.PeriodInverted => MeetReportsTextKeys.Validation(MeetReportsMessageKey.PeriodInverted),
        MeetExportMessageKey.MeetingMalformed => MeetExportTextKeys.MeetingMalformed,
        MeetExportMessageKey.MeetingNotFound => MeetReportsTextKeys.Meeting(MeetReportMeetingMessageKey.MeetingNotFound),
        MeetExportMessageKey.OrientationInvalid => JournalExportTextKeys.OrientationInvalid,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };

    /// <summary>Spec VR-004: the signed-in account from the session, never a request value; the policy guarantees one.</summary>
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
