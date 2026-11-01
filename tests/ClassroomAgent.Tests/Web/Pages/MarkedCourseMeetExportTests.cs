using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using ClosedXML.Excel;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Me = ClassroomAgent.Tests.TestInfrastructure.MeetExportHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-061 over HTTP, exports: the Meet line of a marked course in the third cell of the Course header row (C1) of each
/// sheet of the reports workbook and of the meeting workbook; no row added — spec FR-005.3, FR-005.4, FR-006; AC-001,
/// AC-002, AC-003, AC-008, AC-009, AC-011. Workbooks are read back with ClosedXML from the response bytes.
/// </summary>
public sealed class MarkedCourseMeetExportTests(PostgreSqlFixture database)
{
    private const string Uk = "uk";

    private static Task<int> SetLastReadAsync(InstallationTestHost host, long courseId, DateTimeOffset? read, CancellationToken ct) =>
        host.ExecuteAsync(
            "UPDATE course SET last_complete_read_at = @read WHERE id = @id", ct, ("read", read), ("id", courseId));

    private static async Task<(XLWorkbook Reports, XLWorkbook Meeting)> ExportBothAsync(
        FormClient client, Mr.SeededWorld world, CancellationToken ct)
    {
        var reports = await client.ExportReportsAsync(Me.October(world.CourseId), ct);
        var meeting = await client.ExportMeetingAsync(Me.Meeting(world.CourseId, world.MeetingA), ct);
        Assert.Equal(HttpStatusCode.OK, reports.Status);
        Assert.Equal(HttpStatusCode.OK, meeting.Status);
        return (reports.Workbook(), meeting.Workbook());
    }

    /// <summary>Every sheet of both workbooks: the third cell of the first row (the Course header row).</summary>
    private static IEnumerable<IXLWorksheet> Sheets(XLWorkbook reports, XLWorkbook meeting) =>
        reports.Worksheets.Concat(meeting.Worksheets);

    private static void AssertLineInC1(XLWorkbook reports, XLWorkbook meeting, string line)
    {
        Assert.Equal(3, reports.Worksheets.Count);
        foreach (var sheet in Sheets(reports, meeting))
        {
            Assert.Equal(Mr.CourseName, Me.Text(sheet.Cell(1, 2)));
            Assert.Equal(line, Me.Text(sheet.Cell(1, 3)));
        }
    }

    /// <summary>Every used cell but C1 of the first sheet row, as sheet!address = value — the content the line must not change.</summary>
    private static List<string> ContentBesideC1(XLWorkbook workbook) =>
        workbook.Worksheets
            .SelectMany(sheet => sheet.CellsUsed()
                .Where(c => !(c.Address.RowNumber == 1 && c.Address.ColumnNumber == 3))
                .Select(c => $"{sheet.Name}!{c.Address} = {c.Value}"))
            .ToList();

    /// <summary>
    /// AC-001, FR-005.3, FR-005.4: "not in Classroom" since D1, last read R1 — C1 of every sheet holds the line; every
    /// other cell is as it was before the mark, so no row was added and no figure changed.
    /// </summary>
    [Fact]
    public async Task ANotInClassroomCourse_PutsTheLineInC1_OfEverySheet_AndChangesNothingElse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, DataMarksWorld.R1, ct);
        var (reportsBefore, meetingBefore) = await ExportBothAsync(client, world, ct);
        using var _rb = reportsBefore;
        using var _mb = meetingBefore;

        await host.MarkCourseAsync(world.CourseId, "not_in_classroom", DataMarksWorld.D1, ct);
        var (reports, meeting) = await ExportBothAsync(client, world, ct);
        using var _r = reports;
        using var _m = meeting;

        AssertLineInC1(reports, meeting, MarkedCourseMeetTexts.Uk.NotInClassroomLine(DataMarksWorld.D1, DataMarksWorld.R1));
        Assert.Equal(ContentBesideC1(reportsBefore), ContentBesideC1(reports));
        Assert.Equal(ContentBesideC1(meetingBefore), ContentBesideC1(meeting));
    }

    /// <summary>AC-002: "not visible to the technical account" since D2, last read R2.</summary>
    [Fact]
    public async Task ANotVisibleCourse_PutsItsLineInC1_OfEverySheet()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, DataMarksWorld.R2, ct);
        await host.MarkCourseAsync(world.CourseId, "not_visible_to_technical_account", DataMarksWorld.D2, ct);

        var (reports, meeting) = await ExportBothAsync(client, world, ct);
        using var _r = reports;
        using var _m = meeting;

        AssertLineInC1(reports, meeting, MarkedCourseMeetTexts.Uk.NotVisibleLine(DataMarksWorld.D2, DataMarksWorld.R2));
    }

    /// <summary>AC-003, FR-005.3: an ordinary course leaves C1 empty, and no sheet holds either text.</summary>
    [Fact]
    public async Task AnOrdinaryCourse_LeavesC1Empty()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, DataMarksWorld.R1, ct);

        var (reports, meeting) = await ExportBothAsync(client, world, ct);
        using var _r = reports;
        using var _m = meeting;

        foreach (var sheet in Sheets(reports, meeting))
        {
            Assert.True(sheet.Cell(1, 3).IsEmpty(), $"{sheet.Name}: C1 is not empty");
            Assert.DoesNotContain(MarkedCourseMeetTexts.Uk.RosterPrefix, Me.AllText(sheet), StringComparison.Ordinal);
        }
    }

    /// <summary>AC-009, OD-001 a: no last complete read — the mark and the undated roster part.</summary>
    [Fact]
    public async Task AMarkedCourseNeverReadCompletely_PutsTheUndatedRosterPartInC1()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, null, ct);
        await host.MarkCourseAsync(world.CourseId, "not_in_classroom", DataMarksWorld.D1, ct);

        var (reports, meeting) = await ExportBothAsync(client, world, ct);
        using var _r = reports;
        using var _m = meeting;

        AssertLineInC1(reports, meeting, MarkedCourseMeetTexts.Uk.NotInClassroomLine(DataMarksWorld.D1, null));
    }

    /// <summary>AC-011, FR-005.5: a course only skipped by synchronization leaves C1 empty.</summary>
    [Fact]
    public async Task ASkippedButUnmarkedCourse_LeavesC1Empty()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, Uk, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, DataMarksWorld.R2, ct);
        await host.ExecuteAsync(
            """
            INSERT INTO sync_skipped_course (course_google_id, course_name, reason, skipped_since, created_at, updated_at)
            VALUES (@googleId, @name, 'value_out_of_range', @since, @since, @since)
            """,
            ct,
            ("googleId", CourseTestData.CourseId(1)),
            ("name", Mr.CourseName),
            ("since", DataMarksWorld.R2.AddDays(1)));

        var (reports, meeting) = await ExportBothAsync(client, world, ct);
        using var _r = reports;
        using var _m = meeting;

        Assert.All(Sheets(reports, meeting), sheet => Assert.True(sheet.Cell(1, 3).IsEmpty(), $"{sheet.Name}: C1 is not empty"));
    }

    /// <summary>AC-008, FR-006.3, BR-026: in read-only mode an English user's exports work and carry the English line.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_AnEnglishUsersExports_CarryTheEnglishLine(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, UiLanguageTestData.English, ct, cause);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, DataMarksWorld.R1, ct);
        await host.MarkCourseAsync(world.CourseId, "not_in_classroom", DataMarksWorld.D1, ct);

        var (reports, meeting) = await ExportBothAsync(client, world, ct);
        using var _r = reports;
        using var _m = meeting;

        AssertLineInC1(reports, meeting, MarkedCourseMeetTexts.En.NotInClassroomLine(DataMarksWorld.D1, DataMarksWorld.R1));
        Assert.Equal(2, (await host.MeetExportAuditRowsAsync(ct)).Count);
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>FR-006, English: the "not visible" line in the meeting workbook.</summary>
    [Fact]
    public async Task AnEnglishUser_GetsTheEnglishNotVisibleLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, UiLanguageTestData.English, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, DataMarksWorld.R2, ct);
        await host.MarkCourseAsync(world.CourseId, "not_visible_to_technical_account", DataMarksWorld.D2, ct);

        var (reports, meeting) = await ExportBothAsync(client, world, ct);
        using var _r = reports;
        using var _m = meeting;

        AssertLineInC1(reports, meeting, MarkedCourseMeetTexts.En.NotVisibleLine(DataMarksWorld.D2, DataMarksWorld.R2));
    }
}
