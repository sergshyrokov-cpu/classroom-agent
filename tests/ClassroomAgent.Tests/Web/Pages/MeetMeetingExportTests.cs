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
/// US-044 over HTTP against PostgreSQL (TC-2; openapi <c>exportMeetMeetingXlsx</c>), the meeting workbook: the file and
/// its headers, the header block, the report 4 table compared with the page's own query, the "Other participants" line
/// outside the table, typed cells, file name, audit row, read-only mode and the refusals (AC-004, AC-005, AC-006,
/// AC-007, AC-008, AC-009, AC-010; spec FR-004, FR-006 … FR-009, FR-013). Rows are seeded by SQL.
/// </summary>
public sealed class MeetMeetingExportTests(PostgreSqlFixture database)
{
    private const string Uk = UiLanguageTestData.Ukrainian;

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>Minutes as the page computes them (US-033 I-5): whole minutes, half up.</summary>
    private static double Minutes(long seconds) => (seconds + 30) / 60;

    /// <summary>The host clock's instant as a local date-time of the school's zone.</summary>
    private static DateTime ExportedLocal => TimeZoneInfo.ConvertTime(InstallationTestHost.DefaultStart, MeetReportsTestData.Kyiv).DateTime;

    private static async Task<MeetingDetail> DetailAsync(
        InstallationTestHost host, long courseId, long meetingId, string language, CancellationToken cancellationToken)
    {
        using var scope = host.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<GetMeetReportMeetingQuery>();
        var result = await query.ExecuteAsync(
            new MeetReportMeetingRequest(Id(meetingId), [Id(courseId)], [], []),
            CultureInfo.GetCultureInfo(language),
            cancellationToken);
        return Assert.IsType<MeetingDetail>(result.Page.Meeting);
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

    private static void AssertPeopleRows(
        InstallationTestHost host, IXLWorksheet sheet, MeetingDetail detail, string language)
    {
        for (var i = 0; i < detail.People.Count; i++)
        {
            var row = Me.MeetingTableHeaderRow + 1 + i;
            var line = detail.People[i];
            AssertPerson(host, sheet, row, 1, line.Person, language);
            Assert.Equal(host.Text("MeetReports.Mark." + line.Mark, language), Me.Text(sheet.Cell(row, 3)));
            Assert.Equal(Minutes(line.Seconds), Me.Number(sheet.Cell(row, 4)));
            Assert.Equal(Minutes(detail.DurationSeconds), Me.Number(sheet.Cell(row, 5)));
        }
    }

    private static void AssertPageSetup(IXLWorksheet sheet, XLPageOrientation orientation)
    {
        Assert.Equal(orientation, sheet.PageSetup.PageOrientation);
        Assert.Equal(1, sheet.PageSetup.PagesWide);
        Assert.Equal(0, sheet.PageSetup.PagesTall);
        Assert.Equal(Me.MeetingTableHeaderRow, sheet.PageSetup.FirstRowToRepeatAtTop);
        Assert.Equal(Me.MeetingTableHeaderRow, sheet.PageSetup.LastRowToRepeatAtTop);
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

    /// <summary>AC-004, FR-006, FR-008: a Dean exports one meeting — xlsx, attachment, no-store, one sheet.</summary>
    [Fact]
    public async Task ADean_ExportsTheMeeting_AsAnXlsxAttachment_WithOneSheet()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.StartsWith(JournalExportHostExtensions.XlsxType, response.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("attachment", response.Header("Content-Disposition"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", response.Header("Cache-Control"), StringComparison.OrdinalIgnoreCase);
        using var workbook = response.Workbook();
        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal(host.ReportText(ReportText.MeetSheetMeeting, Uk), sheet.Name);
        Assert.Equal("Зустріч", sheet.Name);
    }

    /// <summary>AC-008: an Admin is allowed as well.</summary>
    [Fact]
    public async Task AnAdmin_ExportsTheMeeting()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Admin, Uk, ct);
        await using var _host = host;

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    /// <summary>FR-004: the seven-row header block of the meeting workbook, then an empty row.</summary>
    [Fact]
    public async Task TheHeaderBlock_DescribesTheMeeting()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var sheet = workbook.Worksheet(1);
        Assert.Equal(host.ReportText(ReportText.MeetHeaderCourse, Uk), Me.Text(sheet.Cell(1, 1)));
        Assert.Equal(host.ReportText(ReportText.MeetHeaderMeeting, Uk), Me.Text(sheet.Cell(2, 1)));
        Assert.Equal(host.ReportText(ReportText.MeetHeaderDuration, Uk), Me.Text(sheet.Cell(3, 1)));
        Assert.Equal(host.ReportText(ReportText.MeetHeaderMeetingCode, Uk), Me.Text(sheet.Cell(4, 1)));
        Assert.Equal(host.ReportText(ReportText.MeetHeaderOrganizer, Uk), Me.Text(sheet.Cell(5, 1)));
        Assert.Equal(host.ReportText(ReportText.MeetHeaderExported, Uk), Me.Text(sheet.Cell(6, 1)));
        Assert.Equal(Mr.CourseName, Me.Text(sheet.Cell(1, 2)));
        Me.AssertSerial(Me.SerialOf(new DateOnly(2026, 10, 5)), sheet.Cell(2, 2));
        Me.AssertSerial(Me.SerialOf(new TimeOnly(10, 0)), sheet.Cell(2, 3));
        Me.AssertSerial(Me.SerialOf(new TimeOnly(10, 45)), sheet.Cell(2, 4));
        Assert.Equal(45, Me.Number(sheet.Cell(3, 2)));
        Assert.Equal(Mr.Codes.Linked, Me.Text(sheet.Cell(4, 2)));
        Assert.Equal(Mr.TeacherName, Me.Text(sheet.Cell(5, 2)));
        Assert.Equal(Mr.TeacherEmail, Me.Text(sheet.Cell(5, 3)));
        AssertTextOrEmpty(string.Empty, sheet.Cell(5, 4));
        Me.AssertSerial(Me.SerialOf(ExportedLocal), sheet.Cell(6, 2));
        var notice = Me.Text(sheet.Cell(7, 1));
        Assert.Contains(host.Text("MeetReports.DelayNotice", Uk), notice, StringComparison.Ordinal);
        Assert.Contains(host.Text("MeetReports.DelayNotice.NotLoaded", Uk), notice, StringComparison.Ordinal);
        Assert.True(RowIsEmpty(sheet, 8), "Row 8 is not empty.");
    }

    /// <summary>FR-004: an organizer who is not a teacher of the course carries the mark in the third cell.</summary>
    [Fact]
    public async Task AnOrganizerWhoIsNotACourseTeacher_CarriesTheMark()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingB), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var sheet = workbook.Worksheet(1);
        Assert.Equal(Mr.OtherTeacherName, Me.Text(sheet.Cell(5, 2)));
        Assert.Equal(Mr.OtherTeacherEmail, Me.Text(sheet.Cell(5, 3)));
        Assert.Equal(host.Text("MeetReports.Mark.NotCourseTeacher", Uk), Me.Text(sheet.Cell(5, 4)));
    }

    /// <summary>AC-004, FR-006: the table equals the page's report 4 row for row, with the filter, frozen header and page setup.</summary>
    [Fact]
    public async Task TheTable_EqualsReport4_RowForRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var detail = await DetailAsync(host, world.CourseId, world.MeetingA, Uk, ct);

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var sheet = workbook.Worksheet(1);
        string[] headings =
        [
            host.ReportText(ReportText.MeetColumnPerson, Uk),
            host.ReportText(ReportText.MeetColumnEmail, Uk),
            host.ReportText(ReportText.MeetColumnMark, Uk),
            host.ReportText(ReportText.MeetColumnMinutes, Uk),
            host.ReportText(ReportText.MeetColumnOfMeetingMinutes, Uk),
        ];
        for (var column = 0; column < headings.Length; column++)
        {
            Assert.Equal(headings[column], Me.Text(sheet.Cell(Me.MeetingTableHeaderRow, column + 1)));
        }

        var n = detail.People.Count;
        Assert.True(n > 0, "The meeting holds identified people.");
        AssertPeopleRows(host, sheet, detail, Uk);
        Assert.Equal(45, Minutes(detail.DurationSeconds));
        Assert.Equal($"A{Me.MeetingTableHeaderRow}:E{Me.MeetingTableHeaderRow + n}", Me.AutoFilterRange(sheet));
        Assert.Equal(Me.MeetingTableHeaderRow, sheet.SheetView.SplitRow);
        AssertPageSetup(sheet, XLPageOrientation.Landscape);
    }

    /// <summary>FR-007: the orientation chosen at export is the page setup; absent means landscape.</summary>
    [Theory]
    [InlineData(null, XLPageOrientation.Landscape)]
    [InlineData("portrait", XLPageOrientation.Portrait)]
    public async Task TheChosenOrientation_IsThePageSetup(string? orientation, XLPageOrientation expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA, orientation), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        AssertPageSetup(workbook.Worksheet(1), expected);
    }

    /// <summary>AC-005, FR-006, I-11: the "Other participants" line sits below the table as numbers, never naming anyone.</summary>
    [Fact]
    public async Task TheOtherParticipants_AreOneLineBelowTheTable_WithoutAnyAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var detail = await DetailAsync(host, world.CourseId, world.MeetingA, Uk, ct);
        var n = detail.People.Count;

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var sheet = workbook.Worksheet(1);
        var blank = Me.MeetingTableHeaderRow + n + 1;
        Assert.True(RowIsEmpty(sheet, blank), $"Row {blank} is not empty.");
        var line = blank + 1;
        Assert.Equal(host.ReportText(ReportText.MeetOtherParticipants, Uk), Me.Text(sheet.Cell(line, 1)));
        Assert.Equal(detail.OtherParticipants.Count, Me.Number(sheet.Cell(line, 2)));
        Assert.Equal(2, detail.OtherParticipants.Count);
        Assert.Equal(Minutes(detail.OtherParticipants.TotalSeconds), Me.Number(sheet.Cell(line, 3)));
        Assert.Equal(10, Minutes(detail.OtherParticipants.TotalSeconds));
        var all = Me.AllText(sheet);
        Assert.DoesNotContain("class.five", all, StringComparison.Ordinal);
        Assert.DoesNotContain(Mr.ClassAccountEmail, all, StringComparison.Ordinal);
    }

    /// <summary>I-6: nobody identified — the header row stays with no data row, and the line follows.</summary>
    [Fact]
    public async Task WhenNobodyIsIdentified_TheTableKeepsItsHeaderRow_AndTheLineFollows()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var meeting = await Mr.InsertMeetingAsync(
            host,
            Mr.Codes.Linked,
            Mr.TeacherEmail,
            MeetReportsTestData.Period.Local(10, 6, 12),
            TimeSpan.FromMinutes(40),
            ct,
            new Mr.Connection(Mr.ClassAccountEmail, 300),
            new Mr.Connection(null, 600));
        var detail = await DetailAsync(host, world.CourseId, meeting, Uk, ct);

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, meeting), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var sheet = workbook.Worksheet(1);
        Assert.Empty(detail.People);
        Assert.Equal(host.ReportText(ReportText.MeetColumnPerson, Uk), Me.Text(sheet.Cell(Me.MeetingTableHeaderRow, 1)));
        Assert.Equal("A9:E9", Me.AutoFilterRange(sheet));
        Assert.True(RowIsEmpty(sheet, 10), "Row 10 is not empty.");
        Assert.Equal(host.ReportText(ReportText.MeetOtherParticipants, Uk), Me.Text(sheet.Cell(11, 1)));
        Assert.Equal(2, Me.Number(sheet.Cell(11, 2)));
        Assert.Equal(Minutes(detail.OtherParticipants.TotalSeconds), Me.Number(sheet.Cell(11, 3)));
    }

    /// <summary>US-033 I-7: a meeting ending on the next day shows its end as a date-time.</summary>
    [Fact]
    public async Task WhenTheEndDateDiffers_TheEndIsADateTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var meeting = await Mr.InsertMeetingAsync(
            host,
            Mr.Codes.Linked,
            Mr.TeacherEmail,
            MeetReportsTestData.Period.Local(10, 31, 23, 30),
            TimeSpan.FromMinutes(70),
            ct);

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, meeting), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var sheet = workbook.Worksheet(1);
        Me.AssertSerial(Me.SerialOf(new DateOnly(2026, 10, 31)), sheet.Cell(2, 2));
        Me.AssertSerial(Me.SerialOf(new TimeOnly(23, 30)), sheet.Cell(2, 3));
        Me.AssertSerial(Me.SerialOf(new DateTime(2026, 11, 1, 0, 40, 0)), sheet.Cell(2, 4));
    }

    /// <summary>AC-008, FR-008.1: course, start date and time only; the Cyrillic name intact; no code, no address.</summary>
    [Fact]
    public async Task TheFileName_IsTheCourseAndTheStart_AndCarriesNoCodeOrAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var plain = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);
        await host.ExecuteAsync("UPDATE course SET name = @n WHERE id = @id", ct, ("n", "Алгебра 7/А"), ("id", world.CourseId));
        var renamed = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, plain.Status);
        Assert.Equal("Meet " + Mr.CourseName + " 2026-10-05 10-00.xlsx", plain.FileNameStar());
        Assert.Equal(HttpStatusCode.OK, renamed.Status);
        Assert.Equal("Meet Алгебра 7_А 2026-10-05 10-00.xlsx", renamed.FileNameStar());
        foreach (var disposition in new[] { plain.Header("Content-Disposition")!, renamed.Header("Content-Disposition")! })
        {
            Assert.DoesNotContain(Mr.Codes.Linked, disposition, StringComparison.Ordinal);
            Assert.DoesNotContain("@", disposition, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-007, FR-013: one audit row with the course, the internal meeting id and the row count — nothing personal.</summary>
    [Fact]
    public async Task AnExport_WritesOneAuditRow_WithoutPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var deanId = await host.AccountIdAsync(DeanAccountTestData.DeanEmail, ct);
        var detail = await DetailAsync(host, world.CourseId, world.MeetingA, Uk, ct);

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var row = Assert.Single(await host.MeetExportAuditRowsAsync(ct));
        Assert.Equal(Me.MeetingAction, row.Action);
        Assert.Equal("app_user", row.ActorType);
        Assert.Equal(deanId, row.ActorId);
        Assert.Equal("dean", row.ActorRole);
        Assert.Equal("course", row.TargetType);
        Assert.Equal(world.CourseId, row.TargetId);
        Assert.Equal("succeeded", row.Outcome);
        Assert.Equal(world.MeetingA, row.MeetingId);
        Assert.Equal(detail.People.Count, row.Rows);
        Assert.Null(row.From);
        Assert.Null(row.To);
        Assert.Null(row.Meetings);
        Assert.Equal("xlsx", row.Format);
        Assert.Null(row.MeetCode);
        var json = string.Join(
            '\n',
            await host.QueryAsync(
                "SELECT row_to_json(a)::text FROM audit_event a WHERE action = 'meet_meeting_exported'",
                r => r.GetString(0),
                ct));
        Assert.All(Mr.SensitiveValues, value => Assert.DoesNotContain(value, json, StringComparison.Ordinal));
    }

    /// <summary>AC-009, FR-009: a meeting of another course, an unknown meeting and an unknown course are 404; no file, no audit row.</summary>
    [Theory]
    [InlineData("meetingElsewhere")]
    [InlineData("meetingUnknown")]
    [InlineData("courseUnknown")]
    public async Task ANotFoundMeeting_Is404_WithoutAudit(string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var body = scenario switch
        {
            "meetingElsewhere" => Me.Meeting(world.CourseId, world.MeetingElsewhere),
            "meetingUnknown" => Me.Meeting(world.CourseId, 999999),
            "courseUnknown" => Me.Meeting(999999, world.MeetingA),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        var response = await client.ExportMeetingAsync(body, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.StartsWith("application/json", response.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        var json = response.Json();
        Assert.Equal(404, json.GetProperty("status").GetInt32());
        if (scenario != "courseUnknown")
        {
            Assert.Equal(host.Text("MeetReports.Meeting.MeetingNotFound", Uk), json.GetProperty("message").GetString());
        }

        Assert.Empty(await host.MeetExportAuditRowsAsync(ct));
    }

    /// <summary>AC-009, FR-009, API-6: a malformed or missing id is a 400 JSON body naming the field; never echoed; no audit row.</summary>
    [Theory]
    [InlineData("meetingAbc")]
    [InlineData("meetingZero")]
    [InlineData("meetingMissing")]
    [InlineData("courseMalformed")]
    public async Task AMalformedId_Is400_NamingTheField_WithoutEchoOrAudit(string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var body = Me.Meeting(world.CourseId, world.MeetingA);
        string field;
        string? message = null;
        string? rejected = null;
        switch (scenario)
        {
            case "meetingAbc":
                body["meetingId"] = rejected = "abc";
                field = "meetingId";
                message = host.Text("Export.Validation.MeetingMalformed", Uk);
                break;
            case "meetingZero":
                body["meetingId"] = "0";
                field = "meetingId";
                message = host.Text("Export.Validation.MeetingMalformed", Uk);
                break;
            case "meetingMissing":
                body.Remove("meetingId");
                field = "meetingId";
                message = host.Text("Export.Validation.MeetingMalformed", Uk);
                break;
            case "courseMalformed":
                body["courseId"] = "x";
                field = "courseId";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        var response = await client.ExportMeetingAsync(body, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.StartsWith("application/json", response.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        var json = response.Json();
        Assert.Equal(400, json.GetProperty("status").GetInt32());
        var errors = json.GetProperty("fieldErrors").EnumerateArray().ToList();
        Assert.Contains(errors, e => e.GetProperty("field").GetString() == field);
        if (message is not null)
        {
            Assert.Equal(message, json.GetProperty("message").GetString());
        }

        if (rejected is not null)
        {
            Assert.DoesNotContain(rejected, response.Text, StringComparison.Ordinal);
        }

        Assert.Empty(await host.MeetExportAuditRowsAsync(ct));
    }

    /// <summary>FR-006: a meeting outside any period still exports — no period applies to one meeting.</summary>
    [Fact]
    public async Task AMeetingOutsideOctober_StillExports()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingOutside), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Me.AssertSerial(Me.SerialOf(new DateOnly(2026, 11, 1)), workbook.Worksheet(1).Cell(2, 2));
    }

    /// <summary>AC-006, FR-012, BR-026: in each read-only cause the export works, is audited and calls no Google API.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheExportWorks_IsAudited_AndCallsNoGoogleApi(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct, cause);
        await using var _host = host;

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Single(await host.MeetExportAuditRowsAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>FR-010, OD-012: an English-speaking user gets the sheet name, labels and marks in English.</summary>
    [Fact]
    public async Task AnEnglishUser_GetsEnglishProgramText()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, UiLanguageTestData.English, ct);
        await using var _host = host;
        var detail = await DetailAsync(host, world.CourseId, world.MeetingA, UiLanguageTestData.English, ct);

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Meeting", sheet.Name);
        Assert.Equal(host.ReportText(ReportText.MeetHeaderCourse, UiLanguageTestData.English), Me.Text(sheet.Cell(1, 1)));
        Assert.Equal(Mr.CourseName, Me.Text(sheet.Cell(1, 2)));
        AssertPeopleRows(host, sheet, detail, UiLanguageTestData.English);
    }

    /// <summary>AC-009, FR-007: a name that starts like a formula stays text, with the quote prefix.</summary>
    [Fact]
    public async Task FormulaLikeText_StaysText_WithTheQuotePrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartWorldAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        const string name = "=HYPERLINK(\"x\")";
        await host.ExecuteAsync(
            "UPDATE classroom_participant SET full_name = @n WHERE email = @e", ct, ("n", name), ("e", Mr.StudentOneEmail));

        var response = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var cell = Find(workbook.Worksheet(1), name);
        Assert.Equal(name, Me.Text(cell));
        Assert.False(cell.HasFormula, $"{cell.Address} holds a formula");
        Assert.True(cell.Style.IncludeQuotePrefix, $"{cell.Address} has no quote prefix");
    }
}
