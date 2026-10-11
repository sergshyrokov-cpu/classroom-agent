using System.Globalization;
using System.Net;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Me = ClassroomAgent.Tests.TestInfrastructure.MeetExportHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-044 over HTTP against PostgreSQL (TC-2; openapi <c>exportMeetReportsXlsx</c>), the course workbook: the file and
/// its headers, the header block, the three sheets read back from the response bytes and compared with the page's own
/// query, typed cells, autofilter, frozen header, page setup, file name, formula-like text, audit row, read-only mode,
/// the refusals and the unchanged journal export (AC-001 … AC-017; spec FR-004 … FR-009, FR-013). Rows are seeded by SQL.
/// </summary>
public sealed class MeetReportsExportTests(PostgreSqlFixture database)
{
    private const string Uk = UiLanguageTestData.Ukrainian;

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>Minutes as the page computes them (US-033 I-5): whole minutes, half up.</summary>
    private static double Minutes(long seconds) => (seconds + 30) / 60;

    /// <summary>The host clock's instant as a local date-time of the school's zone.</summary>
    private static DateTime ExportedLocal => TimeZoneInfo.ConvertTime(InstallationTestHost.DefaultStart, MeetReportsTestData.Kyiv).DateTime;

    private static async Task<MeetReports> ReportsAsync(
        InstallationTestHost host, long courseId, string language, CancellationToken cancellationToken)
    {
        using var scope = host.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<GetMeetReportsQuery>();
        var result = await query.ExecuteAsync(
            new MeetReportsRequest(
                [Id(courseId)], [MeetReportsTestData.Period.FromText], [MeetReportsTestData.Period.ToText]),
            CultureInfo.GetCultureInfo(language),
            cancellationToken);
        return Assert.IsType<MeetReports>(result.Page.Reports);
    }

    private static void AssertTextOrEmpty(string expected, IXLCell cell)
    {
        if (expected.Length == 0)
        {
            Assert.True(cell.Value.IsBlank || (cell.Value.IsText && cell.GetText().Length == 0), $"{cell.Address} should be empty, holds '{cell.GetFormattedString()}'");
            return;
        }

        Assert.Equal(expected, Me.Text(cell));
    }

    /// <summary>Spec FR-007 person rule: name and email in two cells.</summary>
    private static void AssertPerson(
        InstallationTestHost host, IXLWorksheet sheet, int row, int nameColumn, PersonRef person, string language)
    {
        var name = person.FullName ?? (person.Email is null ? host.Text("MeetReports.Person.Unnamed", language) : string.Empty);
        AssertTextOrEmpty(name, sheet.Cell(row, nameColumn));
        AssertTextOrEmpty(person.Email ?? string.Empty, sheet.Cell(row, nameColumn + 1));
    }

    private static bool RowIsEmpty(IXLWorksheet sheet, int row) => !sheet.Row(row).CellsUsed().Any();

    private static IXLCell Find(IXLWorksheet sheet, string text)
    {
        var cell = sheet.CellsUsed().FirstOrDefault(c => c.Value.IsText && c.GetText() == text);
        Assert.NotNull(cell);
        return cell;
    }

    private static bool ShowsDate(IXLWorksheet sheet, int row, DateOnly date, CultureInfo culture)
    {
        foreach (var cell in sheet.Row(row).CellsUsed().Where(c => c.Address.ColumnNumber >= 2))
        {
            if (cell.Value.IsText)
            {
                var text = cell.GetText();
                if (text.Contains(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    || text.Contains(date.ToString(culture.DateTimeFormat.ShortDatePattern, culture), StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (Math.Abs(Me.Serial(cell) - Me.SerialOf(date)) < 1e-6)
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertMeetingRow(
        InstallationTestHost host, IXLWorksheet sheet, int row, MeetListRow meeting, string language)
    {
        Me.AssertSerial(Me.SerialOf(DateOnly.FromDateTime(meeting.StartedAt)), sheet.Cell(row, 1));
        Me.AssertSerial(Me.SerialOf(TimeOnly.FromDateTime(meeting.StartedAt)), sheet.Cell(row, 2));
        Me.AssertSerial(
            meeting.EndedAt.Date == meeting.StartedAt.Date
                ? Me.SerialOf(TimeOnly.FromDateTime(meeting.EndedAt))
                : Me.SerialOf(meeting.EndedAt),
            sheet.Cell(row, 3));
        Assert.Equal(Minutes(meeting.DurationSeconds), Me.Number(sheet.Cell(row, 4)));
        AssertPerson(host, sheet, row, 5, meeting.Organizer, language);
        AssertTextOrEmpty(
            meeting.OrganizerNotCourseTeacher ? host.Text("MeetReports.Mark.NotCourseTeacher", language) : string.Empty,
            sheet.Cell(row, 7));
        Assert.Equal(meeting.CourseTeachers, Me.Number(sheet.Cell(row, 8)));
        Assert.Equal(meeting.CourseStudents, Me.Number(sheet.Cell(row, 9)));
        Assert.Equal(meeting.NotOnCourseList, Me.Number(sheet.Cell(row, 10)));
        Assert.Equal(meeting.OtherParticipants, Me.Number(sheet.Cell(row, 11)));
    }

    private static void AssertPageSetup(IXLWorksheet sheet, XLPageOrientation orientation)
    {
        Assert.Equal(orientation, sheet.PageSetup.PageOrientation);
        Assert.Equal(1, sheet.PageSetup.PagesWide);
        Assert.Equal(0, sheet.PageSetup.PagesTall);
        Assert.Equal(Me.ReportsTableHeaderRow, sheet.PageSetup.FirstRowToRepeatAtTop);
        Assert.Equal(Me.ReportsTableHeaderRow, sheet.PageSetup.LastRowToRepeatAtTop);
    }

    private static async Task<(InstallationTestHost Host, FormClient Client, Mr.SeededWorld World)> StartWorldAsync(
        PostgreSqlFixture database,
        Actor actor,
        string language,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly)
    {
        var (host, client) = await Mr.StartAsync(database, actor, language, cancellationToken, cause);
        var world = await host.SeedWorldAsync(cancellationToken);
        return (host, client, world);
    }

    /// <summary>AC-001, FR-005, FR-008: a Dean exports the course — xlsx, attachment, no-store, three sheets in order.</summary>
    [Fact]
    public async Task ADean_ExportsTheCourse_AsAnXlsxAttachment_WithThreeSheets()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.StartsWith(JournalExportHostExtensions.XlsxType, response.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("attachment", response.Header("Content-Disposition"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", response.Header("Cache-Control"), StringComparison.OrdinalIgnoreCase);
        using var workbook = response.Workbook();
        Assert.Equal(
            [
                host.ReportText(ReportText.MeetSheetSummary, Uk),
                host.ReportText(ReportText.MeetSheetMeetings, Uk),
                host.ReportText(ReportText.MeetSheetStudents, Uk),
            ],
            workbook.Worksheets.Select(w => w.Name));
        Assert.Equal(["Зведення", "Зустрічі", "Участь студентів"], workbook.Worksheets.Select(w => w.Name));
    }

    /// <summary>AC-008: an Admin is allowed as well.</summary>
    [Fact]
    public async Task AnAdmin_ExportsTheCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Admin, Uk, ct);
        await using var _host = host;

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    /// <summary>FR-004: the header block on every sheet — course, period, exported, the delay line, then an empty row.</summary>
    [Fact]
    public async Task EverySheet_StartsWithTheHeaderBlock()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var culture = CultureInfo.GetCultureInfo(Uk);

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal(3, workbook.Worksheets.Count);
        foreach (var sheet in workbook.Worksheets)
        {
            Assert.Equal(host.ReportText(ReportText.MeetHeaderCourse, Uk), Me.Text(sheet.Cell(1, 1)));
            Assert.Equal(Mr.CourseName, Me.Text(sheet.Cell(1, 2)));
            Assert.Equal(host.ReportText(ReportText.MeetHeaderPeriod, Uk), Me.Text(sheet.Cell(2, 1)));
            Assert.True(ShowsDate(sheet, 2, MeetReportsTestData.Period.From, culture), $"{sheet.Name}: row 2 does not show the first day");
            Assert.True(ShowsDate(sheet, 2, MeetReportsTestData.Period.To, culture), $"{sheet.Name}: row 2 does not show the last day");
            Assert.Equal(host.ReportText(ReportText.MeetHeaderExported, Uk), Me.Text(sheet.Cell(3, 1)));
            Me.AssertSerial(Me.SerialOf(ExportedLocal), sheet.Cell(3, 2));
            var notice = Me.Text(sheet.Cell(4, 1));
            Assert.Contains(host.Text("MeetReports.DelayNotice", Uk), notice, StringComparison.Ordinal);
            Assert.Contains(host.Text("MeetReports.DelayNotice.NotLoaded", Uk), notice, StringComparison.Ordinal);
            Assert.True(RowIsEmpty(sheet, 5), $"{sheet.Name}: row 5 is not empty");
        }
    }

    /// <summary>FR-004: with a watermark the delay line shows it in the school's zone, on every sheet.</summary>
    [Fact]
    public async Task TheDelayLine_ShowsTheWatermark_InTheSchoolTimeZone()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var watermark = new DateTimeOffset(2026, 10, 25, 6, 0, 0, TimeSpan.Zero);
        await host.SeedWatermarkAsync(watermark, ct);
        var culture = CultureInfo.GetCultureInfo(Uk);
        var local = TimeZoneInfo.ConvertTime(watermark, MeetReportsTestData.Kyiv).DateTime;

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        foreach (var sheet in workbook.Worksheets)
        {
            var notice = Me.Text(sheet.Cell(4, 1));
            Assert.Contains(host.Text("MeetReports.DelayNotice", Uk), notice, StringComparison.Ordinal);
            Assert.Contains(host.Text("MeetReports.DelayNotice.LoadedUpTo", Uk), notice, StringComparison.Ordinal);
            Assert.Contains(local.ToString(culture.DateTimeFormat.ShortDatePattern + " HH:mm", culture), notice, StringComparison.Ordinal);
            Assert.DoesNotContain(host.Text("MeetReports.DelayNotice.NotLoaded", Uk), notice, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-002, FR-005.1: the Summary is the page's report 1 as numbers.</summary>
    [Fact]
    public async Task TheSummarySheet_HoldsTheFourValuesOfReport1_AsNumbers()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var reports = await ReportsAsync(host, world.CourseId, Uk, ct);

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var sheet = workbook.Worksheet(1);
        Assert.Equal(host.ReportText(ReportText.MeetSummaryIndicator, Uk), Me.Text(sheet.Cell(6, 1)));
        Assert.Equal(host.ReportText(ReportText.MeetSummaryValue, Uk), Me.Text(sheet.Cell(6, 2)));
        Assert.Equal(host.ReportText(ReportText.MeetSummaryMeetingCount, Uk), Me.Text(sheet.Cell(7, 1)));
        Assert.Equal(host.ReportText(ReportText.MeetSummaryTotalMinutes, Uk), Me.Text(sheet.Cell(8, 1)));
        Assert.Equal(host.ReportText(ReportText.MeetSummaryAverageMinutes, Uk), Me.Text(sheet.Cell(9, 1)));
        Assert.Equal(host.ReportText(ReportText.MeetSummaryAverageStudents, Uk), Me.Text(sheet.Cell(10, 1)));
        var summary = reports.Summary;
        Assert.Equal(2, summary.MeetingCount);
        Assert.Equal(summary.MeetingCount, Me.Number(sheet.Cell(7, 2)));
        Assert.Equal(Minutes(summary.TotalDurationSeconds), Me.Number(sheet.Cell(8, 2)));
        Assert.Equal(Minutes(summary.AverageDurationSeconds), Me.Number(sheet.Cell(9, 2)));
        Assert.Equal((double)summary.AverageStudentsPerMeeting, Me.Number(sheet.Cell(10, 2)));
        Assert.Equal("0.0", sheet.Cell(10, 2).Style.NumberFormat.Format);
    }

    /// <summary>AC-002, AC-003, FR-005.2: the Meetings sheet equals the page's report 2 row for row.</summary>
    [Fact]
    public async Task TheMeetingsSheet_EqualsReport2_RowForRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var reports = await ReportsAsync(host, world.CourseId, Uk, ct);

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var sheet = workbook.Worksheet(2);
        string[] headings =
        [
            host.Text("MeetReports.Meetings.Date", Uk),
            host.Text("MeetReports.Meetings.Start", Uk),
            host.Text("MeetReports.Meetings.End", Uk),
            host.Text("MeetReports.Meetings.Duration", Uk),
            host.Text("MeetReports.Meetings.Organizer", Uk),
            host.ReportText(ReportText.MeetColumnOrganizerEmail, Uk),
            host.ReportText(ReportText.MeetColumnOrganizerMark, Uk),
            host.Text("MeetReports.Meetings.CourseTeachers", Uk),
            host.Text("MeetReports.Meetings.CourseStudents", Uk),
            host.Text("MeetReports.Meetings.NotOnCourseList", Uk),
            host.Text("MeetReports.Meetings.OtherParticipants", Uk),
        ];
        for (var column = 0; column < headings.Length; column++)
        {
            Assert.Equal(headings[column], Me.Text(sheet.Cell(Me.ReportsTableHeaderRow, column + 1)));
        }

        var meetings = reports.Meetings;
        Assert.Equal(2, meetings.Count);
        for (var i = 0; i < meetings.Count; i++)
        {
            AssertMeetingRow(host, sheet, Me.ReportsTableHeaderRow + 1 + i, meetings[i], Uk);
        }

        Assert.True(RowIsEmpty(sheet, Me.ReportsTableHeaderRow + 1 + meetings.Count), "The row after the table is not empty.");
        var all = Me.AllText(sheet);
        Assert.DoesNotContain(Mr.Codes.Marked, all, StringComparison.Ordinal);
        Assert.DoesNotContain(Mr.Codes.Unlinked, all, StringComparison.Ordinal);
    }

    /// <summary>AC-002, FR-005.3: the Student participation sheet equals the page's report 3 row for row.</summary>
    [Fact]
    public async Task TheStudentsSheet_EqualsReport3_RowForRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var reports = await ReportsAsync(host, world.CourseId, Uk, ct);

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var sheet = workbook.Worksheet(3);
        string[] headings =
        [
            host.Text("MeetReports.Students.Student", Uk),
            host.ReportText(ReportText.MeetColumnEmail, Uk),
            host.Text("MeetReports.Students.Meetings", Uk),
            host.Text("MeetReports.Students.Minutes", Uk),
        ];
        for (var column = 0; column < headings.Length; column++)
        {
            Assert.Equal(headings[column], Me.Text(sheet.Cell(Me.ReportsTableHeaderRow, column + 1)));
        }

        var students = reports.Students;
        Assert.Equal(3, students.Count);
        for (var i = 0; i < students.Count; i++)
        {
            var row = Me.ReportsTableHeaderRow + 1 + i;
            AssertPerson(host, sheet, row, 1, students[i].Student, Uk);
            Assert.Equal(students[i].Meetings, Me.Number(sheet.Cell(row, 3)));
            Assert.Equal(Minutes(students[i].TotalSeconds), Me.Number(sheet.Cell(row, 4)));
        }

        Assert.Contains(students, s => s.Meetings == 0);
        var studentThree = Find(sheet, Mr.StudentThreeName);
        Assert.Equal(0, Me.Number(sheet.Cell(studentThree.Address.RowNumber, 3)));
        Assert.Equal(0, Me.Number(sheet.Cell(studentThree.Address.RowNumber, 4)));
    }

    /// <summary>AC-001, FR-003: every text of the table rows is on the reports page for the same inputs.</summary>
    [Fact]
    public async Task TheTableTexts_AreOnTheReportsPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);
        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        foreach (var sheet in new[] { workbook.Worksheet(2), workbook.Worksheet(3) })
        {
            var texts = sheet.CellsUsed()
                .Where(c => c.Address.RowNumber > Me.ReportsTableHeaderRow && c.Value.IsText && c.GetText().Length > 0)
                .Select(c => c.GetText())
                .ToList();
            Assert.NotEmpty(texts);
            Assert.All(texts, t => Assert.Contains(t, page.Text, StringComparison.Ordinal));
        }
    }

    /// <summary>FR-007: autofilter over each table, frozen header, one page wide, header row repeated, landscape by default.</summary>
    [Fact]
    public async Task EverySheet_HasTheAutofilter_TheFrozenHeader_AndThePageSetup()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal("A6:B10", Me.AutoFilterRange(workbook.Worksheet(1)));
        Assert.Equal("A6:K8", Me.AutoFilterRange(workbook.Worksheet(2)));
        Assert.Equal("A6:D9", Me.AutoFilterRange(workbook.Worksheet(3)));
        foreach (var sheet in workbook.Worksheets)
        {
            Assert.Equal(Me.ReportsTableHeaderRow, sheet.SheetView.SplitRow);
            AssertPageSetup(sheet, XLPageOrientation.Landscape);
        }
    }

    /// <summary>FR-007, OD-009: the chosen orientation is the page setup of every sheet; absent means landscape.</summary>
    [Theory]
    [InlineData(null, XLPageOrientation.Landscape)]
    [InlineData("landscape", XLPageOrientation.Landscape)]
    [InlineData("portrait", XLPageOrientation.Portrait)]
    public async Task TheChosenOrientation_IsThePageSetupOfEverySheet(string? orientation, XLPageOrientation expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var response = await client.ExportReportsAsync(Me.October(world.CourseId, orientation), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal(3, workbook.Worksheets.Count);
        Assert.All(workbook.Worksheets, sheet => AssertPageSetup(sheet, expected));
    }

    /// <summary>FR-009: an unknown orientation is a 400 with its own message; nothing is echoed or audited.</summary>
    [Fact]
    public async Task AnUnknownOrientation_Is400_WithItsOwnMessage_AndNoAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var response = await client.ExportReportsAsync(Me.October(world.CourseId, "Portrait"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.StartsWith("application/json", response.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        var json = response.Json();
        Assert.Equal(host.Text("Export.Validation.OrientationInvalid", Uk), json.GetProperty("message").GetString());
        var fields = json.GetProperty("fieldErrors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).ToList();
        Assert.Contains("orientation", fields);
        Assert.DoesNotContain("Portrait", response.Text, StringComparison.Ordinal);
        Assert.Empty(await host.MeetExportAuditRowsAsync(ct));
    }

    /// <summary>AC-008, FR-008.1: course and period only, the Cyrillic name intact, no person, no code, no address.</summary>
    [Fact]
    public async Task TheFileName_IsTheCourseAndPeriod_AndCarriesNoPersonOrCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        await host.ExecuteAsync("UPDATE course SET name = @n WHERE id = @id", ct, ("n", "Алгебра 7/А"), ("id", world.CourseId));

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Meet Алгебра 7_А 2026-10-01–2026-10-31.xlsx", response.FileNameStar());
        var disposition = response.Header("Content-Disposition")!;
        Assert.DoesNotContain("@", disposition, StringComparison.Ordinal);
        Assert.All(
            Mr.SensitiveValues,
            value => Assert.DoesNotContain(value, disposition, StringComparison.Ordinal));
    }

    /// <summary>AC-009, FR-007: a text that starts like a formula stays text, with the quote prefix.</summary>
    [Fact]
    public async Task FormulaLikeText_StaysText_WithTheQuotePrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        await host.ExecuteAsync("UPDATE course SET name = @n WHERE id = @id", ct, ("n", "=1+1"), ("id", world.CourseId));
        foreach (var (email, name) in new[] { (Mr.StudentOneEmail, "+SUM(1)"), (Mr.StudentTwoEmail, "@cmd"), (Mr.StudentThreeEmail, "-2") })
        {
            await host.ExecuteAsync(
                "UPDATE classroom_participant SET full_name = @n WHERE email = @e", ct, ("n", name), ("e", email));
        }

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        foreach (var sheet in workbook.Worksheets)
        {
            AssertQuoted("=1+1", sheet.Cell(1, 2));
        }

        var students = workbook.Worksheet(3);
        foreach (var name in new[] { "+SUM(1)", "@cmd", "-2" })
        {
            AssertQuoted(name, Find(students, name));
        }

        static void AssertQuoted(string expected, IXLCell cell)
        {
            Assert.Equal(expected, Me.Text(cell));
            Assert.False(cell.HasFormula, $"{cell.Address} holds a formula");
            Assert.True(cell.Style.IncludeQuotePrefix, $"{cell.Address} has no quote prefix");
        }
    }

    /// <summary>AC-007, FR-013: one audit row with ids, period, row counts and format — and nothing personal.</summary>
    [Fact]
    public async Task AnExport_WritesOneAuditRow_WithoutPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var deanId = await host.AccountIdAsync(DeanAccountTestData.DeanEmail, ct);
        var reports = await ReportsAsync(host, world.CourseId, Uk, ct);

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var row = Assert.Single(await host.MeetExportAuditRowsAsync(ct));
        Assert.Equal(Me.ReportsAction, row.Action);
        Assert.Equal("app_user", row.ActorType);
        Assert.Equal(deanId, row.ActorId);
        Assert.Equal("dean", row.ActorRole);
        Assert.Equal("course", row.TargetType);
        Assert.Equal(world.CourseId, row.TargetId);
        Assert.Equal("succeeded", row.Outcome);
        Assert.Equal(MeetReportsTestData.Period.From, row.From);
        Assert.Equal(MeetReportsTestData.Period.To, row.To);
        Assert.Equal(reports.Students.Count, row.Rows);
        Assert.Equal(reports.Meetings.Count, row.Meetings);
        Assert.Null(row.MeetingId);
        Assert.Null(row.TemplateId);
        Assert.Null(row.BuiltIn);
        Assert.Equal("xlsx", row.Format);
        Assert.Null(row.MeetCode);
        var json = string.Join(
            '\n',
            await host.QueryAsync(
                "SELECT row_to_json(a)::text FROM audit_event a WHERE action = 'meet_reports_exported'",
                r => r.GetString(0),
                ct));
        Assert.All(Mr.SensitiveValues, value => Assert.DoesNotContain(value, json, StringComparison.Ordinal));
    }

    /// <summary>FR-005, US-033 FR-014: a course with no meeting and no student shows the empty-state lines and no autofilter.</summary>
    [Fact]
    public async Task ACourseWithoutMeetingsOrStudents_ShowsTheEmptyLines_AndNoAutofilter()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var empty = await CourseRows.InsertCourseAsync(
            host, ct, googleId: CourseTestData.CourseId(9), name: "Test Course Empty");

        var response = await client.ExportReportsAsync(Me.October(empty), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var meetings = workbook.Worksheet(2);
        var students = workbook.Worksheet(3);
        Assert.Equal(host.Text("MeetReports.Empty.NoMeetings", Uk), Me.Text(meetings.Cell(Me.ReportsTableHeaderRow, 1)));
        Assert.Equal(host.Text("MeetReports.Empty.NoStudents", Uk), Me.Text(students.Cell(Me.ReportsTableHeaderRow, 1)));
        Assert.Null(Me.AutoFilterRange(meetings));
        Assert.Null(Me.AutoFilterRange(students));
        var summary = workbook.Worksheet(1);
        for (var row = 7; row <= 10; row++)
        {
            Assert.Equal(0, Me.Number(summary.Cell(row, 2)));
        }

        var audit = Assert.Single(await host.MeetExportAuditRowsAsync(ct));
        Assert.Equal(0, audit.Rows);
        Assert.Equal(0, audit.Meetings);
    }

    /// <summary>AC-017: a meeting at 00:30 on the first day is in, one ending after midnight keeps its end date-time, the next month's is out.</summary>
    [Fact]
    public async Task AtTheKyivBounds_MeetingsAreInOrOutByTheirStart_AndALateEndKeepsItsDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, MeetReportsTestData.Period.Local(10, 1, 0, 30), TimeSpan.FromMinutes(30), ct);
        await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, MeetReportsTestData.Period.Local(10, 31, 23, 30), TimeSpan.FromMinutes(70), ct);
        var reports = await ReportsAsync(host, world.CourseId, Uk, ct);

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var sheet = workbook.Worksheet(2);
        Assert.Equal(4, reports.Meetings.Count);
        var dates = Enumerable.Range(0, reports.Meetings.Count)
            .Select(i => Me.Serial(sheet.Cell(Me.ReportsTableHeaderRow + 1 + i, 1)))
            .ToList();
        Assert.Contains(dates, d => Math.Abs(d - Me.SerialOf(new DateOnly(2026, 10, 1))) < 1e-6);
        Assert.Contains(dates, d => Math.Abs(d - Me.SerialOf(new DateOnly(2026, 10, 31))) < 1e-6);
        Assert.DoesNotContain(dates, d => Math.Abs(d - Me.SerialOf(new DateOnly(2026, 11, 1))) < 1e-6);
        Assert.True(RowIsEmpty(sheet, Me.ReportsTableHeaderRow + 1 + reports.Meetings.Count), "The file holds more than four meetings.");
        var late = reports.Meetings.Select((m, i) => (Meeting: m, Row: Me.ReportsTableHeaderRow + 1 + i))
            .Single(x => x.Meeting.StartedAt.Date == new DateTime(2026, 10, 31));
        Me.AssertSerial(Me.SerialOf(new DateTime(2026, 11, 1, 0, 40, 0)), sheet.Cell(late.Row, 3));
    }

    /// <summary>FR-010, OD-012: an English-speaking user gets English program text; school data stays as stored.</summary>
    [Fact]
    public async Task AnEnglishUser_GetsEnglishProgramText_AndUntranslatedData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, UiLanguageTestData.English, ct);
        await using var _host = host;
        var reports = await ReportsAsync(host, world.CourseId, UiLanguageTestData.English, ct);

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal(["Summary", "Meetings", "Student participation"], workbook.Worksheets.Select(w => w.Name));
        var summary = workbook.Worksheet(1);
        Assert.Equal(host.ReportText(ReportText.MeetHeaderCourse, UiLanguageTestData.English), Me.Text(summary.Cell(1, 1)));
        Assert.Equal(Mr.CourseName, Me.Text(summary.Cell(1, 2)));
        var students = workbook.Worksheet(3);
        for (var i = 0; i < reports.Students.Count; i++)
        {
            AssertPerson(host, students, Me.ReportsTableHeaderRow + 1 + i, 1, reports.Students[i].Student, UiLanguageTestData.English);
        }

        var all = Me.AllText(students);
        Assert.Contains(Mr.StudentOneEmail, all, StringComparison.Ordinal);
        Assert.Contains(Mr.StudentTwoEmail, all, StringComparison.Ordinal);
        Assert.Contains(Mr.StudentThreeEmail, all, StringComparison.Ordinal);
    }

    /// <summary>AC-006, FR-012, BR-026: in each read-only cause the export works, is audited and calls no Google API.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheExportWorks_IsAudited_AndCallsNoGoogleApi(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct, cause);
        await using var _host = host;

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Single(await host.MeetExportAuditRowsAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>AC-006, FR-012: a successful export reads nothing from Google.</summary>
    [Fact]
    public async Task ASuccessfulExport_CallsNoGoogleApi()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var response = await client.ExportReportsAsync(Me.October(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>AC-009, FR-009, API-6: each malformed or missing input is a 400 JSON body naming the field; nothing is echoed or audited.</summary>
    [Theory]
    [InlineData("courseMissing")]
    [InlineData("courseMalformed")]
    [InlineData("fromMalformed")]
    [InlineData("toMissing")]
    [InlineData("periodInverted")]
    public async Task AnInvalidInput_Is400_NamingTheField_WithoutEchoOrAudit(string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var body = Me.October(world.CourseId);
        string[] fields;
        string key;
        string? rejected = null;
        switch (scenario)
        {
            case "courseMissing":
                body.Remove("courseId");
                fields = ["courseId"];
                key = "MeetReports.Validation.CourseMalformed";
                break;
            case "courseMalformed":
                body["courseId"] = rejected = "abc";
                fields = ["courseId"];
                key = "MeetReports.Validation.CourseMalformed";
                break;
            case "fromMalformed":
                body["from"] = rejected = "2026-13-01";
                fields = ["from"];
                key = "MeetReports.Validation.FromMalformed";
                break;
            case "toMissing":
                body.Remove("to");
                fields = ["to"];
                key = "MeetReports.Validation.ToMalformed";
                break;
            case "periodInverted":
                body["from"] = "2026-11-01";
                body["to"] = "2026-10-01";
                fields = ["from", "to"];
                key = "MeetReports.Validation.PeriodInverted";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        var response = await client.ExportReportsAsync(body, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.StartsWith("application/json", response.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        var json = response.Json();
        var expected = host.Text(key, Uk);
        Assert.Equal(400, json.GetProperty("status").GetInt32());
        Assert.True(json.TryGetProperty("error", out _), "The API-6 body has no 'error'.");
        Assert.True(json.TryGetProperty("timestamp", out _), "The API-6 body has no 'timestamp'.");
        Assert.Equal(expected, json.GetProperty("message").GetString());
        var error = json.GetProperty("fieldErrors").EnumerateArray()
            .Single(e => e.GetProperty("message").GetString() == expected);
        Assert.Contains(error.GetProperty("field").GetString(), fields);
        if (rejected is not null)
        {
            Assert.DoesNotContain(rejected, response.Text, StringComparison.Ordinal);
        }

        Assert.Empty(await host.MeetExportAuditRowsAsync(ct));
    }

    /// <summary>AC-009, FR-009, API-5: an unknown course is a 404 JSON body naming the problem; no audit row.</summary>
    [Fact]
    public async Task AnUnknownCourse_Is404_NamingIt_WithoutAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var response = await client.ExportReportsAsync(Me.October(999999), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        var json = response.Json();
        Assert.Equal(404, json.GetProperty("status").GetInt32());
        Assert.Equal(host.Text("MeetReports.Validation.CourseUnknown", Uk), json.GetProperty("message").GetString());
        Assert.Empty(await host.MeetExportAuditRowsAsync(ct));
    }

    /// <summary>AC-016: the journal export is unchanged — its sheets carry no autofilter.</summary>
    [Fact]
    public async Task TheJournalExport_IsUnchanged_ItsSheetsHaveNoAutofilter()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.NotEmpty(workbook.Worksheets);
        Assert.All(workbook.Worksheets, sheet => Assert.Null(Me.AutoFilterRange(sheet)));
    }
}
