using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MembershipStartHost;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-056 AC-009, FR-005 (db-design §1, §5): memberships stored before the upgrade follow the rule with no new
/// synchronization. The schema does not change, so the pre-upgrade database is these rows seeded directly as the runs
/// stored them: the first load's memberships first seen at its <c>observedAt</c>, the course row stamped a few seconds
/// later at save time, a later arrival first seen by the run of 2026-11-05. The installation has no Workspace
/// connection, so no run touches them before the Dean opens the journal.
/// </summary>
public sealed class MembershipStartPreUpgradeTests(PostgreSqlFixture database)
{
    /// <summary>The course row's <c>created_at</c>: saved later in the first-loading run than its <c>observedAt</c>.</summary>
    private static readonly DateTimeOffset CourseSaved = FirstRun.AddSeconds(5);

    private async Task<(InstallationTestHost Host, FormClient Dean)> StartUpgradedAsync(CancellationToken ct)
    {
        var host = await InstallationTestHost.CreateAsync(database, ct, now: LaterRun.AddDays(1));
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        host.ControlPlane.ReplySuccess().ReplySuccess().ReplySuccess();
        host.Start();

        // The start-up purge deletes a participant without a membership; seeding after it keeps the two from racing.
        await host.WaitForPurgeRunsAsync(1, ct);
        return (host, await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct));
    }

    private static async Task<long> StudentAsync(
        InstallationTestHost host,
        long courseId,
        int ordinal,
        DateTimeOffset firstSeenAt,
        CancellationToken ct)
    {
        var id = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(ordinal), email: CourseTestData.Email($"person{ordinal}"),
            fullName: CourseTestData.Name(ordinal));
        await CourseRows.InsertMembershipAsync(host, courseId, id, ct, firstSeenAt: firstSeenAt, lastSeenAt: LaterRun);
        return id;
    }

    private static async Task<PageResponse> Journal2025Async(FormClient dean, long courseId, CancellationToken ct)
    {
        var page = await dean.GetAsync(JournalTestData.Url(courseId, "2025-09-01", "2025-12-31"), ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        return page;
    }

    /// <summary>
    /// AC-009 first paragraph: the members the first load stored are listed for 01.09–31.12.2025; the member first seen
    /// on 2026-11-05 is not.
    /// </summary>
    [Fact]
    public async Task StoredFirstLoadMembers_CountFromTheCourseCreation_AndALaterArrivalDoesNot()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, dean) = await StartUpgradedAsync(ct);
        await using var _host = host;
        var course = await CourseRows.InsertCourseAsync(host, ct, creationTime: CourseCreated, createdAt: CourseSaved);
        await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, itemDate: October2025Item);
        await StudentAsync(host, course, 2, FirstRun, ct);
        await StudentAsync(host, course, 3, FirstRun, ct);
        await StudentAsync(host, course, 5, LaterRun, ct);

        var journal = await Journal2025Async(dean, course, ct);

        Assert.Contains(CourseTestData.Name(2), journal.Text, StringComparison.Ordinal);
        Assert.Contains(CourseTestData.Name(3), journal.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.Name(5), journal.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-009 second paragraph (FR-005, §3 "Кто появился позже"): a course whose first load stored nobody — the same rows
    /// as one whose first-load memberships were all deleted before the upgrade — and which gained student M on
    /// 2026-11-05: M is not listed for 2025, while a submitter of that period is (so the journal is shown). Green before
    /// implementation by design (M is excluded by first seen today); it fails an implementation that recognises the
    /// first load by the course's earliest surviving first-seen instant.
    /// </summary>
    [Fact]
    public async Task ALaterArrival_InACourseWithNoStoredFirstLoadMember_DoesNotCountFromTheCreation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, dean) = await StartUpgradedAsync(ct);
        await using var _host = host;
        var course = await CourseRows.InsertCourseAsync(host, ct, creationTime: CourseCreated, createdAt: CourseSaved);
        var item = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, itemDate: October2025Item);
        await StudentAsync(host, course, 6, LaterRun, ct);
        var submitter = await StudentAsync(host, course, 7, LaterRun, ct);
        await CourseWorkRows.InsertSubmissionAsync(host, item, submitter, ct);

        var journal = await Journal2025Async(dean, course, ct);

        Assert.Contains(CourseTestData.Name(7), journal.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.Name(6), journal.Text, StringComparison.Ordinal);
    }
}
