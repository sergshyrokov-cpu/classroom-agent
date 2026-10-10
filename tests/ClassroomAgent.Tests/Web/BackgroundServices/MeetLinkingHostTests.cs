using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using ClassroomAgent.Web.Configuration;
using static ClassroomAgent.Tests.TestInfrastructure.MeetLinkingTestData;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-032 FR-002 … FR-006, AC-001, AC-003, AC-004, AC-005, AC-006, AC-007, AC-011, AC-013, I-1 (db-design §2, §3, §7):
/// the linking step of a synchronization run through the real host, its composition root and PostgreSQL, with the Meet
/// port substituted (TC-4). Courses and rosters are seeded in SQL before the host starts, with <c>first_seen_at</c> before
/// the meetings; the run's Classroom step reads an empty port and leaves them as they are.
/// </summary>
public sealed class MeetLinkingHostTests(PostgreSqlFixture database)
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

    /// <summary>AC-001, FR-004, FR-006, FR-014: 85 % against 20 % links the code to A, unconfirmed, with its audit row.</summary>
    [Fact]
    public async Task AnUnambiguousCode_IsLinkedAutomatically_WithOneAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        Courses courses = null!;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                courses = await SeedClearRostersAsync(h, ct);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        var link = Assert.Single(await LinksAsync(host, ct));
        Assert.Equal(Code, link.MeetingCode);
        Assert.Equal(courses.A, link.CourseId);
        Assert.True(link.LinkedAutomatically);
        Assert.Null(link.LinkedBy);
        Assert.Equal(host.Time.GetUtcNow(), link.LinkedAt);
        Assert.Null(link.ConfirmedBy);
        Assert.Null(link.ConfirmedAt);
        Assert.Null(link.MarkedBy);
        Assert.Null(link.MarkedAt);
        var audit = Assert.Single(await AutoLinkAuditRowsAsync(host, ct));
        Assert.Equal("system", audit.ActorType);
        Assert.Equal("course", audit.TargetType);
        Assert.Equal(courses.A, audit.TargetId);
        Assert.Equal("succeeded", audit.Outcome);
        Assert.Null(audit.RequestId);
        Assert.Equal(Code, audit.MeetCode);
        Assert.Null(audit.PreviousCourseId);
    }

    /// <summary>AC-003, FR-004: 70 % against 55 % is ambiguous (gap 15 points), so nothing is linked and the run completes.</summary>
    [Fact]
    public async Task AnAmbiguousCode_StaysUnassigned_AndTheRunCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                await SeedCloseRostersAsync(h, ct);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(1L, await host.CountAsync("meet_session", ct));
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct));
        Assert.Equal(0L, await host.CountAsync("audit_event", ct, $"action = '{AutoLinkedAction}'"));
    }

    /// <summary>AC-013, FR-003: an organizer who teaches no course gives the code no candidate, so it stays unassigned.</summary>
    [Fact]
    public async Task ACodeWithNoCandidate_StaysUnassigned()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                var a = await CourseRows.InsertCourseAsync(
                    h,
                    ct,
                    googleId: CourseTestData.CourseId(1),
                    name: CourseTestData.CourseName(1),
                    updateTime: InstallationTestHost.DefaultStart - TimeSpan.FromDays(10));
                await SeedRosterAsync(h, a, CourseTestData.Roles.Student, Students(1, 20), ct);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(1L, await host.CountAsync("meet_session", ct));
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct));
    }

    /// <summary>AC-004, FR-006: an unassigned code is re-scored on every run; further meetings make it unambiguous.</summary>
    [Fact]
    public async Task AnAmbiguousCode_IsLinkedByALaterRun_WhenNewMeetingsMakeItUnambiguous()
    {
        var ct = TestContext.Current.CancellationToken;
        Courses courses = null!;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                courses = await SeedCloseRostersAsync(h, ct);

                // Ten more students of A only; they join in the second run's new meeting: 24 of 30 against 11 of 30.
                await SeedRosterAsync(h, courses.A, CourseTestData.Roles.Student, Students(21, 30), ct);
                h.Meet.WithPage(FirstMeetingEvents());
            },
            intervalMinutes: 1);
        var first = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct));

        host.Meet.Reset().WithPage([.. FirstMeetingEvents(), .. Meeting(2, Students(21, 30), LaterMeeting)]);
        await host.WaitForSecondRunAsync(first, ct);

        var link = Assert.Single(await LinksAsync(host, ct));
        Assert.Equal(courses.A, link.CourseId);
        Assert.True(link.LinkedAutomatically);
        Assert.Equal(host.Time.GetUtcNow(), link.LinkedAt);
        Assert.Single(await AutoLinkAuditRowsAsync(host, ct));
    }

    /// <summary>AC-005, FR-006, BR-065: an automatic link is never revised, however the shares move afterwards.</summary>
    [Fact]
    public async Task AnAutomaticLink_StaysWithA_WhenLaterMeetingsFavourB()
    {
        var ct = TestContext.Current.CancellationToken;
        Courses courses = null!;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                courses = await SeedClearRostersAsync(h, ct);

                // Thirty students of B only join a later meeting: 34 of 50 for B against 17 of 50 for A.
                await SeedRosterAsync(h, courses.B, CourseTestData.Roles.Student, Students(21, 50), ct);
                h.Meet.WithPage(FirstMeetingEvents());
            },
            intervalMinutes: 1);
        var first = await host.WaitForFinishedRunAsync(ct);
        var linkedBefore = Assert.Single(await LinksAsync(host, ct));
        Assert.Equal(courses.A, linkedBefore.CourseId);
        var snapshot = await LinkSnapshotAsync(host, ct);

        host.Meet.Reset().WithPage([.. FirstMeetingEvents(), .. Meeting(2, Students(21, 50), LaterMeeting)]);
        await host.WaitForSecondRunAsync(first, ct);

        Assert.Equal(snapshot, await LinkSnapshotAsync(host, ct));
        Assert.Single(await AutoLinkAuditRowsAsync(host, ct));
    }

    /// <summary>AC-011, BR-083, FR-006: a code with a "not a course" mark is never scored, however clear its meetings are.</summary>
    [Fact]
    public async Task AMarkedCode_IsNeverLinked_AndItsRowDoesNotChange()
    {
        var ct = TestContext.Current.CancellationToken;
        IReadOnlyList<string> before = [];
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                // The purge at host start deletes a mark whose code has no meeting yet (spec FR-016 rule 4); here the
                // meetings arrive with the run, so the purge service is left out of this host.
                var configure = h.ConfigureServices;
                h.ConfigureServices = services =>
                {
                    configure?.Invoke(services);
                    RetentionPurgeHost.RemovePurgeService(services);
                };
                await SeedClearRostersAsync(h, ct);
                await InsertMarkAsync(h, InstallationTestHost.DefaultStart - TimeSpan.FromDays(1), ct);
                before = await LinkSnapshotAsync(h, ct);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(1L, await host.CountAsync("meet_session", ct));
        Assert.Equal(before, await LinkSnapshotAsync(host, ct));
        Assert.Equal(0L, await host.CountAsync("audit_event", ct, $"action = '{AutoLinkedAction}'"));
    }

    /// <summary>FR-006, BR-065: a link a person made is never revised, even when the shares favour another course.</summary>
    [Fact]
    public async Task APersonsLink_IsNeverRevised_WhenTheSharesFavourAnotherCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        IReadOnlyList<string> before = [];
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                var courses = await SeedClearRostersAsync(h, ct);
                await InsertPersonLinkAsync(h, courses.B, InstallationTestHost.DefaultStart - TimeSpan.FromDays(1), ct);
                before = await LinkSnapshotAsync(h, ct);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(1L, await host.CountAsync("meet_session", ct));
        Assert.Equal(before, await LinkSnapshotAsync(host, ct));
        Assert.Equal(0L, await host.CountAsync("audit_event", ct, $"action = '{AutoLinkedAction}'"));
    }

    /// <summary>AC-007, FR-006: a run whose Meet step fails does not reach the linking step, even for stored unambiguous meetings.</summary>
    [Fact]
    public async Task AFailedMeetStep_LinksNothing_AndTheRunFailsAtTheMeetStep()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                await SeedClearRostersAsync(h, ct);
                await SeedStoredMeetingAsync(h, 1, Students(1, 20), FirstMeeting, ct);
                h.Meet.FailOnFirstPage = new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.ScopeNotAuthorized);
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Failed, run.Status);
        Assert.Equal("meet", await host.ScalarAsync<string>("SELECT failed_step FROM sync_state", ct));
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct));
        Assert.Equal(0L, await host.CountAsync("audit_event", ct, $"action = '{AutoLinkedAction}'"));
    }

    /// <summary>AC-006, FR-005: thresholds of 55 % and 10 points link a 70 % / 55 % code to the 70 % course.</summary>
    [Fact]
    public async Task ConfiguredThresholds_LinkACodeTheDefaultsLeaveAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        Courses courses = null!;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                h.Settings[InstallationSettingsReader.MeetLinkingMinSharePercentKey] = "55";
                h.Settings[InstallationSettingsReader.MeetLinkingMinGapPointsKey] = "10";
                courses = await SeedCloseRostersAsync(h, ct);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        var link = Assert.Single(await LinksAsync(host, ct));
        Assert.Equal(courses.A, link.CourseId);
        Assert.True(link.LinkedAutomatically);
    }

    /// <summary>I-1, FR-002: meetings older than every membership's first sighting find nobody on the roster, so nothing is linked.</summary>
    [Fact]
    public async Task MeetingsBeforeEveryFirstSeenDate_FindNobodyOnTheRoster_AndLinkNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                await SeedClearRostersAsync(h, ct, firstSeenAt: LaterMeeting);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(1L, await host.CountAsync("meet_session", ct));
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct));
        Assert.Equal(0L, await host.CountAsync("audit_event", ct, $"action = '{AutoLinkedAction}'"));
    }

    /// <summary>FR-006, §8: a failure of the linking step fails the run at the Linking step; the meetings the Meet step stored stay.</summary>
    [Fact]
    public async Task AFailureOfTheLinkingStep_FailsTheRunAtThatStep_AndKeepsTheStoredMeetings()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(
            ct,
            async h =>
            {
                await SeedClearRostersAsync(h, ct);
                await FailLinkInsertsAsync(h, ct);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Failed, run.Status);
        Assert.Equal("Unexpected", run.LastError);
        Assert.Equal("linking", await host.ScalarAsync<string>("SELECT failed_step FROM sync_state", ct));
        Assert.Equal(1L, await host.CountAsync("meet_session", ct));
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct));
        Assert.Equal(0L, await host.CountAsync("audit_event", ct, $"action = '{AutoLinkedAction}'"));
    }
}
