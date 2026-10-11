using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-044 over HTTP (openapi <c>exportMeetReportsXlsx</c>, <c>exportMeetMeetingXlsx</c>): the JSON bodies, the
/// antiforgery header taken from the Meet reports page, the cell readers for the workbook read back from the response
/// bytes in memory (spec §9 — never written to disk), and the Meet export audit rows of db-design §3 read with raw SQL.
/// The world is the US-033 October world of <see cref="MeetReportsHostExtensions.SeedWorldAsync"/>.
/// </summary>
public static class MeetExportHostExtensions
{
    public const string ReportsPath = "/api/v1/exports/meet-reports-xlsx";

    public const string MeetingPath = "/api/v1/exports/meet-meeting-xlsx";

    public const string ReportsAction = "meet_reports_exported";

    public const string MeetingAction = "meet_meeting_exported";

    /// <summary>Spec FR-004: the course workbook's header block is 4 rows, then one empty row, then the table header.</summary>
    public const int ReportsTableHeaderRow = 6;

    /// <summary>Spec FR-004: the meeting workbook's header block is 7 rows, then one empty row, then the table header.</summary>
    public const int MeetingTableHeaderRow = 9;

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>The body of the October course export, as the page's script sends it (all members strings).</summary>
    public static Dictionary<string, string?> October(long courseId, string? orientation = null)
    {
        var body = new Dictionary<string, string?>
        {
            ["courseId"] = Id(courseId),
            ["from"] = MeetReportsTestData.Period.FromText,
            ["to"] = MeetReportsTestData.Period.ToText,
        };
        if (orientation is not null)
        {
            body["orientation"] = orientation;
        }

        return body;
    }

    /// <summary>The body of a meeting export.</summary>
    public static Dictionary<string, string?> Meeting(long courseId, long meetingId, string? orientation = null)
    {
        var body = new Dictionary<string, string?>
        {
            ["courseId"] = Id(courseId),
            ["meetingId"] = Id(meetingId),
        };
        if (orientation is not null)
        {
            body["orientation"] = orientation;
        }

        return body;
    }

    /// <summary>Posts to the course export with a fresh token from the Meet reports page (API-7).</summary>
    public static Task<BinaryResponse> ExportReportsAsync(
        this FormClient client,
        object body,
        CancellationToken cancellationToken,
        bool withToken = true,
        string contentType = "application/json") =>
        PostAsync(client, ReportsPath, body, cancellationToken, withToken, contentType);

    /// <summary>Posts to the meeting export with a fresh token from the Meet reports page (API-7).</summary>
    public static Task<BinaryResponse> ExportMeetingAsync(
        this FormClient client,
        object body,
        CancellationToken cancellationToken,
        bool withToken = true,
        string contentType = "application/json") =>
        PostAsync(client, MeetingPath, body, cancellationToken, withToken, contentType);

    private static async Task<BinaryResponse> PostAsync(
        FormClient client,
        string path,
        object body,
        CancellationToken cancellationToken,
        bool withToken,
        string contentType)
    {
        if (withToken)
        {
            await client.GetAsync(MeetReportsTestData.Url(), cancellationToken);
        }

        var json = body as string ?? JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        var headers = withToken && client.LastToken is { } token
            ? new Dictionary<string, string> { [JournalExportHostExtensions.TokenHeader] = token }
            : null;
        return await client.SendForBytesAsync(HttpMethod.Post, path, content, cancellationToken, headers);
    }

    /// <summary>A cell of a sheet by 1-based row and column.</summary>
    public static IXLCell At(this IXLWorksheet sheet, int row, int column) => sheet.Cell(row, column);

    /// <summary>The value of a numeric cell; fails when the cell holds text (spec FR-007 "Numbers").</summary>
    public static double Number(IXLCell cell)
    {
        Assert.True(cell.Value.IsNumber, $"{cell.Address} is {cell.Value.Type}, not a number: '{cell.GetFormattedString()}'");
        return cell.Value.GetNumber();
    }

    /// <summary>
    /// The Excel serial of a date, time or date-time cell; fails when the cell holds text or a bare number without a
    /// date or time format (spec FR-007 "Dates and times"). The check does not depend on how the renderer stores it.
    /// </summary>
    public static double Serial(IXLCell cell)
    {
        Assert.False(cell.Value.IsText, $"{cell.Address} holds text '{cell.GetFormattedString()}', not a date or time");
        Assert.False(cell.Value.IsBlank, $"{cell.Address} is empty");
        if (cell.Value.IsDateTime)
        {
            return cell.Value.GetDateTime().ToOADate();
        }

        if (cell.Value.IsTimeSpan)
        {
            return cell.Value.GetTimeSpan().TotalDays;
        }

        var format = cell.Style.NumberFormat.Format;
        Assert.True(
            cell.Style.NumberFormat.NumberFormatId is >= 14 and <= 22 or >= 45 and <= 47
            || format.IndexOfAny(['d', 'y', 'h', 'H', 'm', 's']) >= 0,
            $"{cell.Address} is a number without a date or time format");
        return cell.Value.GetNumber();
    }

    /// <summary>The serial of a local date (no time).</summary>
    public static double SerialOf(DateOnly date) => date.ToDateTime(TimeOnly.MinValue).ToOADate();

    /// <summary>The serial of a time of day (fraction of a day).</summary>
    public static double SerialOf(TimeOnly time) => time.ToTimeSpan().TotalDays;

    /// <summary>The serial of a local date-time.</summary>
    public static double SerialOf(DateTime local) => local.ToOADate();

    /// <summary>Asserts a date, time or date-time cell holds the expected serial to the minute.</summary>
    public static void AssertSerial(double expected, IXLCell cell) =>
        Assert.Equal(expected, Serial(cell), 1.0 / (24 * 60 * 2));

    /// <summary>A text cell's text; fails when it is not text.</summary>
    public static string Text(IXLCell cell)
    {
        Assert.True(cell.Value.IsText, $"{cell.Address} is {cell.Value.Type}, not text");
        return cell.GetText();
    }

    /// <summary>Every text of a sheet, joined by new lines — for "never contains" checks.</summary>
    public static string AllText(IXLWorksheet sheet) =>
        string.Join('\n', sheet.CellsUsed().Select(c => c.GetFormattedString()));

    /// <summary>The autofilter range of a sheet as "A6:K8", or null when the sheet has none.</summary>
    public static string? AutoFilterRange(IXLWorksheet sheet) =>
        sheet.AutoFilter is { IsEnabled: true } filter && filter.Range is { } range
            ? range.RangeAddress.ToStringRelative()
            : null;

    /// <summary>Every Meet export audit row (db-design §3.2), both actions, in insertion order.</summary>
    public static Task<IReadOnlyList<MeetExportAuditRow>> MeetExportAuditRowsAsync(
        this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync(
            """
            SELECT action, actor_type, actor_id, actor_role, target_type, target_id, outcome, request_id,
                   export_period_from, export_period_to, export_template_id, export_template_built_in,
                   export_rows, export_meetings, export_meeting_id, export_format, meet_code
            FROM audit_event WHERE action IN ('meet_reports_exported', 'meet_meeting_exported') ORDER BY id
            """,
            r => new MeetExportAuditRow(
                r.GetString(0),
                r.GetString(1),
                r.IsDBNull(2) ? null : r.GetInt64(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetInt64(5),
                r.GetString(6),
                r.IsDBNull(7) ? null : r.GetString(7),
                r.IsDBNull(8) ? null : r.GetFieldValue<DateOnly>(8),
                r.IsDBNull(9) ? null : r.GetFieldValue<DateOnly>(9),
                r.IsDBNull(10) ? null : r.GetInt64(10),
                r.IsDBNull(11) ? null : r.GetBoolean(11),
                r.IsDBNull(12) ? null : r.GetInt32(12),
                r.IsDBNull(13) ? null : r.GetInt32(13),
                r.IsDBNull(14) ? null : r.GetInt64(14),
                r.IsDBNull(15) ? null : r.GetString(15),
                r.IsDBNull(16) ? null : r.GetString(16)),
            cancellationToken);

    /// <summary>One Meet export audit row (db-design §3.2).</summary>
    public sealed record MeetExportAuditRow(
        string Action,
        string ActorType,
        long? ActorId,
        string? ActorRole,
        string? TargetType,
        long? TargetId,
        string Outcome,
        string? RequestId,
        DateOnly? From,
        DateOnly? To,
        long? TemplateId,
        bool? BuiltIn,
        int? Rows,
        int? Meetings,
        long? MeetingId,
        string? Format,
        string? MeetCode);
}
