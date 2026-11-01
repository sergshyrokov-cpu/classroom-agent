using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MeetLinkingTestData;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-061 FR-001, FR-004, VR-002, I-6; AC-004, AC-007: the linking step of a synchronization run through the real host
/// and PostgreSQL, with the Meet port substituted (TC-4). Courses, rosters and the US-059 course marks are seeded in SQL
/// before the host starts; the run's Classroom step reads an empty port and leaves them as they are.
/// </summary>
public sealed class MarkedCourseMeetLinkingHostTests(PostgreSqlFixture database)
{
    private Task<InstallationTestHost> StartAsync(
        CancellationToken ct,
        Func<InstallationTestHost, Task> seed,
        int? intervalMinutes = null) =>
        SyncHostExtensions.StartAsync(
            database,
            ct,
            intervalMinutes: intervalMinutes,
            seed: async (host, _) => await seed(host));

    private static Task<string?> MarkOfAsync(InstallationTestHost host, long courseId, CancellationToken ct) =>
        host.ScalarAsync<string?>("SELECT absence_mark FROM course WHERE id = @id", ct, ("id", courseId));

    /// <summary>
    /// AC-004, FR-001.1, FR-001.3, I-6: the old course A (marked) and the re-created course B have the same teacher and
    /// students — 100 % each, ambiguous while A is a candidate. Without A the code links itself to B, with its audit row.
    /// </summary>
    [Theory]
    [InlineData("not_in_classroom")]
    [InlineData("not_visible_to_technical_account")]
    public async Task ARecreatedCourse_GetsTheCodeAutomatically_WhenTheOldOneIsMarked(string mark)
    {
        var ct = TestContext.Current.CancellationToken;
        Courses courses = null!;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                courses = await SeedCoursesAsync(h, ct);
                await SeedRosterAsync(h, courses.A, CourseTestData.Roles.Student, Students(1, 20), ct);
                await SeedRosterAsync(h, courses.B, CourseTestData.Roles.Student, Students(1, 20), ct);
                await h.MarkCourseAsync(courses.A, mark, LongAgo.AddDays(20), ct);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(mark, await MarkOfAsync(host, courses.A, ct));
        var link = Assert.Single(await LinksAsync(host, ct));
        Assert.Equal(Code, link.MeetingCode);
        Assert.Equal(courses.B, link.CourseId);
        Assert.True(link.LinkedAutomatically);
        Assert.Null(link.LinkedBy);
        var audit = Assert.Single(await AutoLinkAuditRowsAsync(host, ct));
        Assert.Equal("system", audit.ActorType);
        Assert.Equal(courses.B, audit.TargetId);
        Assert.Equal(Code, audit.MeetCode);
    }

    /// <summary>FR-001.1, VR-002: A (85 %) is marked, B (20 %) is the only candidate left and below 60 %, so nothing is linked.</summary>
    [Fact]
    public async Task AMarkedCourse_IsNeverChosen_EvenWithTheBestShare()
    {
        var ct = TestContext.Current.CancellationToken;
        Courses courses = null!;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                courses = await SeedClearRostersAsync(h, ct);
                await h.MarkCourseAsync(courses.A, "not_in_classroom", LongAgo.AddDays(20), ct);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal("not_in_classroom", await MarkOfAsync(host, courses.A, ct));
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct));
        Assert.Equal(0L, await host.CountAsync("audit_event", ct, $"action = '{AutoLinkedAction}'"));
    }

    /// <summary>FR-001.1, FR-001.2: removing a marked B (20 %) leaves A's share as before, so A (85 %) is linked as today.</summary>
    [Fact]
    public async Task RemovingAMarkedCandidate_LeavesTheOthersSharesAsBefore()
    {
        var ct = TestContext.Current.CancellationToken;
        Courses courses = null!;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                courses = await SeedClearRostersAsync(h, ct);
                await h.MarkCourseAsync(courses.B, "not_visible_to_technical_account", LongAgo.AddDays(20), ct);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        var link = Assert.Single(await LinksAsync(host, ct));
        Assert.Equal(courses.A, link.CourseId);
        Assert.True(link.LinkedAutomatically);
    }

    /// <summary>AC-007, FR-001.4, FR-004.1: a code linked automatically to A stays with A after A gets a mark and the Meet data is loaded again.</summary>
    [Fact]
    public async Task AnAutomaticLink_StaysWithItsCourse_AfterTheCourseIsMarked()
    {
        var ct = TestContext.Current.CancellationToken;
        Courses courses = null!;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                courses = await SeedClearRostersAsync(h, ct);
                h.Meet.WithPage(FirstMeetingEvents());
            },
            intervalMinutes: 1);
        var first = await host.WaitForFinishedRunAsync(ct);
        var linked = Assert.Single(await LinksAsync(host, ct));
        Assert.Equal(courses.A, linked.CourseId);
        var snapshot = await LinkSnapshotAsync(host, ct);

        await host.MarkCourseAsync(courses.A, "not_in_classroom", host.Time.GetUtcNow(), ct);
        host.Meet.Reset().WithPage([.. FirstMeetingEvents(), .. Meeting(2, Students(1, 20), LaterMeeting)]);
        await host.WaitForSecondRunAsync(first, ct);

        Assert.Equal("not_in_classroom", await MarkOfAsync(host, courses.A, ct));
        Assert.Equal(snapshot, await LinkSnapshotAsync(host, ct));
        Assert.Single(await AutoLinkAuditRowsAsync(host, ct));
    }
}
