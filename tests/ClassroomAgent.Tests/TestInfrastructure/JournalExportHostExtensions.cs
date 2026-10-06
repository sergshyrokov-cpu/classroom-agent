using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClassroomAgent.Application.Localization;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Web.Security;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-028 over HTTP (openapi <c>exportJournalXlsx</c>): the JSON body, the antiforgery header, the file read back
/// with ClosedXML from the response bytes in memory (spec §9, AC-011 — never written to disk), and the export audit rows
/// of db-design §3 read with raw SQL.
/// </summary>
public static class JournalExportHostExtensions
{
    public const string ExportPath = "/api/v1/exports/journal-xlsx";

    public const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public const string TokenHeader = "RequestVerificationToken";

    /// <summary>The body of a September export of a course, as the page's script sends it (all members strings).</summary>
    public static Dictionary<string, string?> September(
        long courseId,
        string? template = ReportTemplateTestData.BuiltInKey,
        string? names = null,
        string? orientation = null)
    {
        var body = new Dictionary<string, string?>
        {
            ["template"] = template,
            ["courseId"] = courseId.ToString(CultureInfo.InvariantCulture),
            ["from"] = JournalTestData.Period.FromText,
            ["to"] = JournalTestData.Period.ToText,
        };
        if (names is not null)
        {
            body["names"] = names;
        }

        if (orientation is not null)
        {
            body["orientation"] = orientation;
        }

        return body;
    }

    /// <summary>
    /// Opens the report page (which carries a token, as every page does through the language switcher) and posts the
    /// body as JSON with that token in the header (API-7). A fresh token is always taken from the report page before
    /// the POST, as the browser does: a token taken from the sign-in page before signing in belongs to the anonymous
    /// user and is rejected after sign-in.
    /// </summary>
    public static async Task<BinaryResponse> ExportAsync(
        this FormClient client,
        object body,
        CancellationToken cancellationToken,
        bool withToken = true,
        string contentType = "application/json")
    {
        if (withToken)
        {
            await client.GetAsync(ReportTemplateTestData.ReportPath, cancellationToken);
        }

        var json = body as string ?? JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        var headers = withToken && client.LastToken is { } token
            ? new Dictionary<string, string> { [TokenHeader] = token }
            : null;
        return await client.SendForBytesAsync(HttpMethod.Post, ExportPath, content, cancellationToken, headers);
    }

    /// <summary>
    /// The program text the export's text port gives in a language (entity model §3.2), resolved through the host's own
    /// translations — the tests do not fix the key names behind <see cref="ReportText"/>.
    /// </summary>
    public static string ReportText(this InstallationTestHost host, ReportText text, string culture)
    {
        using var scope = host.CreateScope();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<SharedResource>>();
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return new LocalizedReportTexts(localizer).Get(text);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>The workbook of a <c>200</c>, read from the response bytes in memory.</summary>
    public static XLWorkbook Workbook(this BinaryResponse response) => new(new MemoryStream(response.Bytes));

    /// <summary>The RFC 6266 <c>filename*</c> of the response, decoded.</summary>
    public static string? FileNameStar(this BinaryResponse response) =>
        response.Header("Content-Disposition") is { } value
            ? ContentDispositionHeaderValue.Parse(value).FileNameStar
            : null;

    /// <summary>Every <c>journal_exported</c> audit row with the db-design §3.2 columns.</summary>
    public static Task<IReadOnlyList<ExportAuditRow>> ExportAuditRowsAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync(
            """
            SELECT actor_type, actor_id, actor_role, target_type, target_id, outcome, request_id,
                   export_period_from, export_period_to, export_template_id, export_template_built_in, export_rows, export_format
            FROM audit_event WHERE action = 'journal_exported' ORDER BY id
            """,
            r => new ExportAuditRow(
                r.GetString(0),
                r.IsDBNull(1) ? null : r.GetInt64(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetInt64(4),
                r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                r.GetFieldValue<DateOnly>(7),
                r.GetFieldValue<DateOnly>(8),
                r.IsDBNull(9) ? null : r.GetInt64(9),
                r.GetBoolean(10),
                r.GetInt32(11),
                r.GetString(12)),
            cancellationToken);

    /// <summary>One export audit row (db-design §3.2).</summary>
    public sealed record ExportAuditRow(
        string ActorType,
        long? ActorId,
        string? ActorRole,
        string? TargetType,
        long? TargetId,
        string Outcome,
        string? RequestId,
        DateOnly From,
        DateOnly To,
        long? TemplateId,
        bool BuiltIn,
        int Rows,
        string Format);
}
