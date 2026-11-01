using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-061 over HTTP, screens: the Meet line of a marked course on the reports page (reports 1–3) and the meeting page
/// (report 4) — spec FR-005, FR-006, VR-001, VR-003, VR-004; AC-001, AC-002, AC-003, AC-008, AC-009, AC-011. The world of
/// US-033 is seeded by SQL, then the course gets the stored state of each scenario (US-059 columns).
/// </summary>
public sealed class MarkedCourseMeetReportsPageTests(PostgreSqlFixture database)
{
    private const string Uk = "uk";

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static string MeetingUrl(Mr.SeededWorld world) =>
        MeetReportsTestData.MeetingUrl(
            Id(world.MeetingA), world.CourseId, MeetReportsTestData.Period.FromText, MeetReportsTestData.Period.ToText);

    private static Task<int> SetLastReadAsync(InstallationTestHost host, long courseId, DateTimeOffset? read, CancellationToken ct) =>
        host.ExecuteAsync(
            "UPDATE course SET last_complete_read_at = @read WHERE id = @id", ct, ("read", read), ("id", courseId));

    private static async Task<(string Reports, string Meeting)> OpenBothAsync(FormClient client, Mr.SeededWorld world, CancellationToken ct)
    {
        var reports = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);
        var meeting = await client.GetAsync(MeetingUrl(world), ct);
        Assert.Equal(HttpStatusCode.OK, reports.Status);
        Assert.Equal(HttpStatusCode.OK, meeting.Status);
        return (Mc.VisibleText(reports.Body), Mc.VisibleText(meeting.Body));
    }

    private static void AssertNoLine(string text, string page)
    {
        foreach (var texts in new[] { MarkedCourseMeetTexts.Uk, MarkedCourseMeetTexts.En })
        {
            Assert.False(text.Contains(texts.RosterPrefix, StringComparison.Ordinal), $"{page}: a roster line is shown.");
            Assert.False(
                text.Contains(texts.NotInClassroomSince[..texts.NotInClassroomSince.IndexOf("{0}", StringComparison.Ordinal)], StringComparison.Ordinal),
                $"{page}: a 'not in Classroom' mark is shown.");
            Assert.False(
                text.Contains(texts.NotVisibleSince[..texts.NotVisibleSince.IndexOf("{0}", StringComparison.Ordinal)], StringComparison.Ordinal),
                $"{page}: a 'not visible' mark is shown.");
        }
    }

    /// <summary>
    /// AC-001, FR-005.1, FR-005.2, FR-005.4: a course "not in Classroom" since D1, last read R1 — both pages show the one
    /// line; apart from the line, each page reads exactly as it did before the mark (figures, rows, roles, delay notice).
    /// </summary>
    [Theory]
    [InlineData(Actor.Dean)]
    [InlineData(Actor.Admin)]
    public async Task ANotInClassroomCourse_ShowsTheMarkAndRosterDate_OnBothPages_AndNothingElseChanges(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, DataMarksWorld.R1, ct);
        var before = await OpenBothAsync(client, world, ct);
        AssertNoLine(before.Reports, "reports page before the mark");

        await host.MarkCourseAsync(world.CourseId, "not_in_classroom", DataMarksWorld.D1, ct);
        var after = await OpenBothAsync(client, world, ct);

        var line = MarkedCourseMeetTexts.Uk.NotInClassroomLine(DataMarksWorld.D1, DataMarksWorld.R1);
        Assert.Contains(line, after.Reports, StringComparison.Ordinal);
        Assert.Contains(line, after.Meeting, StringComparison.Ordinal);
        // The comparison starts at the reports (the delay notice) and at the meeting's course name: the US-062 banner
        // above them counts marked courses and rightly changes with the mark.
        var delayNotice = host.Text(MeetReportsTestData.Keys.DelayNotice, Uk);
        Assert.Equal(
            From(before.Reports, delayNotice),
            Normalize(From(after.Reports, delayNotice).Replace(line, string.Empty, StringComparison.Ordinal)));
        Assert.Equal(
            From(before.Meeting, Mr.CourseName),
            Normalize(From(after.Meeting, Mr.CourseName).Replace(line, string.Empty, StringComparison.Ordinal)));
    }

    /// <summary>FR-005.2: on the meeting page the line comes under the course name.</summary>
    [Fact]
    public async Task OnTheMeetingPage_TheLineComesAfterTheCourseName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, DataMarksWorld.R1, ct);
        await host.MarkCourseAsync(world.CourseId, "not_in_classroom", DataMarksWorld.D1, ct);

        var (_, meeting) = await OpenBothAsync(client, world, ct);

        var name = meeting.IndexOf(Mr.CourseName, StringComparison.Ordinal);
        var line = meeting.IndexOf(MarkedCourseMeetTexts.Uk.NotInClassroomLine(DataMarksWorld.D1, DataMarksWorld.R1), StringComparison.Ordinal);
        Assert.True(name >= 0 && line > name, "The line must follow the course name.");
    }

    /// <summary>AC-002, FR-005.1: a course "not visible to the technical account" since D2, last read R2.</summary>
    [Fact]
    public async Task ANotVisibleCourse_ShowsItsMarkAndRosterDate_OnBothPages()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, DataMarksWorld.R2, ct);
        await host.MarkCourseAsync(world.CourseId, "not_visible_to_technical_account", DataMarksWorld.D2, ct);

        var (reports, meeting) = await OpenBothAsync(client, world, ct);

        var line = MarkedCourseMeetTexts.Uk.NotVisibleLine(DataMarksWorld.D2, DataMarksWorld.R2);
        Assert.Contains(line, reports, StringComparison.Ordinal);
        Assert.Contains(line, meeting, StringComparison.Ordinal);
    }

    /// <summary>AC-003, FR-005.2: an ordinary course with meetings shows neither text on either page.</summary>
    [Fact]
    public async Task AnOrdinaryCourse_ShowsNoLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, DataMarksWorld.R1, ct);

        var (reports, meeting) = await OpenBothAsync(client, world, ct);

        Assert.Contains(Mr.StudentOneName, reports, StringComparison.Ordinal);
        AssertNoLine(reports, "reports page");
        AssertNoLine(meeting, "meeting page");
    }

    /// <summary>AC-009, OD-001 a, VR-004: no last complete read — the mark, then the undated roster part.</summary>
    [Fact]
    public async Task AMarkedCourseNeverReadCompletely_ShowsTheUndatedRosterPart()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, null, ct);
        await host.MarkCourseAsync(world.CourseId, "not_in_classroom", DataMarksWorld.D1, ct);

        var (reports, meeting) = await OpenBothAsync(client, world, ct);

        var line = MarkedCourseMeetTexts.Uk.NotInClassroomLine(DataMarksWorld.D1, null);
        Assert.Contains(line, reports, StringComparison.Ordinal);
        Assert.Contains(line, meeting, StringComparison.Ordinal);
    }

    /// <summary>AC-011, FR-005.5, I-1: a course only skipped by synchronization (no mark) gets no Meet line.</summary>
    [Fact]
    public async Task ASkippedButUnmarkedCourse_ShowsNoLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
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

        var (reports, meeting) = await OpenBothAsync(client, world, ct);

        AssertNoLine(reports, "reports page");
        AssertNoLine(meeting, "meeting page");
    }

    /// <summary>AC-008, FR-006, §7, BR-026: in read-only mode a user with English sees both pages with the English line.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_AnEnglishUser_SeesTheEnglishLine(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, UiLanguageTestData.English, ct, cause);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, DataMarksWorld.R1, ct);
        await host.MarkCourseAsync(world.CourseId, "not_in_classroom", DataMarksWorld.D1, ct);

        var (reports, meeting) = await OpenBothAsync(client, world, ct);

        var line = MarkedCourseMeetTexts.En.NotInClassroomLine(DataMarksWorld.D1, DataMarksWorld.R1);
        Assert.Contains(line, reports, StringComparison.Ordinal);
        Assert.Contains(line, meeting, StringComparison.Ordinal);
        Assert.DoesNotContain(MarkedCourseMeetTexts.Uk.RosterPrefix, reports, StringComparison.Ordinal);
    }

    /// <summary>FR-006, English: the "not visible" mark and the undated roster part.</summary>
    [Fact]
    public async Task AnEnglishUser_SeesTheEnglishNotVisibleMark_AndTheUndatedRoster()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, UiLanguageTestData.English, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await SetLastReadAsync(host, world.CourseId, null, ct);
        await host.MarkCourseAsync(world.CourseId, "not_visible_to_technical_account", DataMarksWorld.D2, ct);

        var (reports, meeting) = await OpenBothAsync(client, world, ct);

        var line = MarkedCourseMeetTexts.En.NotVisibleLine(DataMarksWorld.D2, null);
        Assert.Contains(line, reports, StringComparison.Ordinal);
        Assert.Contains(line, meeting, StringComparison.Ordinal);
    }

    /// <summary>The visible text from the first occurrence of <paramref name="start"/> on.</summary>
    private static string From(string text, string start)
    {
        var index = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(index >= 0, $"'{start}' is not on the page.");
        return text[index..];
    }

    private static string Normalize(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
}
