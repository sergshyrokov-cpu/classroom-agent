using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MembershipStartHost;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-056 AC-001, AC-005 … AC-008, AC-010, AC-011 (spec FR-001 … FR-004): the course journal after real synchronization
/// runs through the host and PostgreSQL (TC-2), Google substituted (TC-4), the clock on the days the criteria name. The
/// course of AC-001 is created in Google on 2025-09-01 and first loaded on 2026-10-22; a Dean opens the journal.
/// </summary>
public sealed class MembershipStartJournalTests(PostgreSqlFixture database)
{
    private static readonly string CourseA = CourseTestData.CourseId(1);

    private static readonly string CourseB = CourseTestData.CourseId(2);

    /// <summary>The course of AC-008: created in Google on 2026-10-20, 09:00 in Kyiv.</summary>
    private static readonly DateTimeOffset CourseBCreated = new(2026, 10, 20, 6, 0, 0, TimeSpan.Zero);

    /// <summary>Teacher T of the course of AC-001; never a journal row.</summary>
    private static readonly RosterEntry T = Person(1);

    /// <summary>Course A, its roster as given, and items in October 2025, November 2025 and October 2026.</summary>
    private static FakeClassroomReader WithCourseA(
        FakeClassroomReader reader,
        IEnumerable<RosterEntry> students,
        DateTimeOffset? creationTime) =>
        reader
            .WithCourse(CourseA, CourseTestData.CourseName(1), teachers: [T], students: students, creationTime: creationTime)
            .WithCourseWork(CourseA, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), October2025Item)
            .WithCourseWork(CourseA, CourseWorkTestData.ItemId(2), CourseWorkResource.CourseWork, CourseWorkTestData.Title(2), November2025Item)
            .WithCourseWork(CourseA, CourseWorkTestData.ItemId(3), CourseWorkResource.CourseWork, CourseWorkTestData.Title(3), October2026Item);

    private static async Task<PageResponse> JournalAsync(
        FormClient client,
        InstallationTestHost host,
        string courseGoogleId,
        string from,
        string to,
        CancellationToken ct)
    {
        var page = await client.GetAsync(JournalTestData.Url(await host.CourseIdAsync(courseGoogleId, ct), from, to), ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        return page;
    }

    private static void Lists(PageResponse page, RosterEntry person) =>
        Assert.Contains(person.FullName!, page.Text, StringComparison.Ordinal);

    private static void DoesNotList(PageResponse page, RosterEntry person) =>
        Assert.DoesNotContain(person.FullName!, page.Text, StringComparison.Ordinal);

    /// <summary>AC-001, FR-001 a, FR-003.1: a roster student of the first load, with no submissions, is in the 2025 journal.</summary>
    [Fact]
    public async Task AStudentOfTheFirstLoad_IsListedForAPeriodBeforeTheFirstSynchronization()
    {
        var ct = TestContext.Current.CancellationToken;
        var s = Person(2);
        await using var host = await StartFirstRunAsync(database, r => WithCourseA(r, [s], CourseCreated), ct);
        var dean = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);

        var journal = await JournalAsync(dean, host, CourseA, "2025-09-01", "2025-12-31", ct);

        Lists(journal, s);
        DoesNotList(journal, T);
    }

    /// <summary>
    /// AC-005, FR-001 a, OD-001: someone the first load finds only through submissions — on another course's roster, so
    /// the journal can name them — counts from the course's creation: listed for November 2025, where they have no
    /// submission; their membership ends on the first run's day, off the roster.
    /// </summary>
    [Fact]
    public async Task ASubmitterFoundByTheFirstLoad_IsListedForAPeriodWithoutTheirSubmission()
    {
        var ct = TestContext.Current.CancellationToken;
        var s = Person(2);
        var l = Person(3);
        await using var host = await StartFirstRunAsync(
            database,
            r => WithCourseA(r, [s], CourseCreated)
                .WithCourse(CourseB, CourseTestData.CourseName(2), students: [l])
                .WithSubmission(CourseA, CourseWorkTestData.ItemId(1), l.GoogleUserId, CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.TurnedIn),
            ct);
        var dean = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);

        var october = await JournalAsync(dean, host, CourseA, "2025-10-01", "2025-10-31", ct);
        var november = await JournalAsync(dean, host, CourseA, "2025-11-01", "2025-11-30", ct);

        // The control: L is named in the month of their submission, so an absence below is the start, not the name.
        Lists(october, l);
        Lists(november, l);
        var membership = await host.MembershipAsync(CourseA, l.GoogleUserId, ct);
        Assert.False(membership.OnRoster);
        Assert.Equal(FirstRun, membership.LastSeenAt);
    }

    /// <summary>
    /// AC-006, FR-001 b, OD-002: without a creationTime the first load's day is the start — not listed for 2025 (a
    /// submitter of that period is, so the journal is shown), listed for October 2026. Green before implementation by
    /// design: the start of FR-001 b equals the first-seen instant the code uses today.
    /// </summary>
    [Fact]
    public async Task WithoutACreationTime_AStudentOfTheFirstLoad_CountsFromTheFirstRunsDay()
    {
        var ct = TestContext.Current.CancellationToken;
        var s = Person(2);
        var p = Person(4);
        await using var host = await StartFirstRunAsync(
            database,
            r => WithCourseA(r, [s, p], creationTime: null)
                .WithSubmission(CourseA, CourseWorkTestData.ItemId(1), p.GoogleUserId, CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.TurnedIn),
            ct);
        var dean = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);

        var year2025 = await JournalAsync(dean, host, CourseA, "2025-09-01", "2025-12-31", ct);
        var october2026 = await JournalAsync(dean, host, CourseA, "2026-10-01", "2026-10-31", ct);

        Lists(year2025, p);
        DoesNotList(year2025, s);
        Lists(october2026, s);
    }

    /// <summary>
    /// AC-007, FR-001 c, FR-002: a student who joins later and is first seen by the run of 2026-11-05 is in neither the
    /// 2025 journal nor the October 2026 one, while the first load's student is in both.
    /// </summary>
    [Fact]
    public async Task ALaterArrival_CountsFromTheDayTheyWereSeen()
    {
        var ct = TestContext.Current.CancellationToken;
        var s = Person(2);
        var n = Person(5);
        await using var first = await StartFirstRunAsync(database, r => WithCourseA(r, [s], CourseCreated), ct);
        await using var later = await RunAgainAtAsync(first, LaterRun, r => WithCourseA(r, [s, n], CourseCreated), ct);
        var dean = await JournalHostExtensions.SignInAsync(later, Actor.Dean, ct);

        var year2025 = await JournalAsync(dean, later, CourseA, "2025-09-01", "2025-12-31", ct);
        var october2026 = await JournalAsync(dean, later, CourseA, "2026-10-01", "2026-10-31", ct);

        Lists(year2025, s);
        DoesNotList(year2025, n);
        Lists(october2026, s);
        DoesNotList(october2026, n);
        Assert.Equal(LaterRun, (await later.MembershipAsync(CourseA, n.GoogleUserId, ct)).FirstSeenAt);
    }

    /// <summary>
    /// AC-008, FR-001 a, FR-002: a course created on 2026-10-20 that the installation's first run did not load is first
    /// loaded by the run of 2026-11-05; its student counts from the course's creation and is in the journal of
    /// 20.10–31.10.2026.
    /// </summary>
    [Fact]
    public async Task ACourseFirstLoadedAfterTheFirstSynchronization_CountsItsStudentsFromItsCreation()
    {
        var ct = TestContext.Current.CancellationToken;
        var s = Person(2);
        var s2 = Person(6);
        await using var first = await StartFirstRunAsync(database, r => WithCourseA(r, [s], CourseCreated), ct);
        await using var later = await RunAgainAtAsync(
            first,
            LaterRun,
            r => WithCourseA(r, [s], CourseCreated)
                .WithCourse(CourseB, CourseTestData.CourseName(2), teachers: [T], students: [s2], creationTime: CourseBCreated)
                .WithCourseWork(CourseB, CourseWorkTestData.ItemId(4), CourseWorkResource.CourseWork, CourseWorkTestData.Title(4), new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero)),
            ct);
        var dean = await JournalHostExtensions.SignInAsync(later, Actor.Dean, ct);

        var journal = await JournalAsync(dean, later, CourseB, "2026-10-20", "2026-10-31", ct);

        Lists(journal, s2);
    }

    /// <summary>
    /// AC-010, FR-004: another run leaves first seen and last seen as synchronization observed them — the first load's
    /// student first seen on 2026-10-22 and last seen on 2026-11-05, the leaver last seen on 2026-10-22 off the roster —
    /// and the leaver's expiry counts from last seen: in a course created in Google more than the retention period ago
    /// (start in 2020), the purge keeps the leaver last seen in 2026. Green before implementation by design: this Story
    /// changes neither synchronization nor the purge; the test guards that it stays so.
    /// </summary>
    [Fact]
    public async Task ObservedDatesAndTheLeaverExpiry_AreUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var createdLongAgo = new DateTimeOffset(2020, 1, 15, 8, 0, 0, TimeSpan.Zero);
        var s = Person(2);
        var leaver = Person(7);
        await using var first = await StartFirstRunAsync(database, r => WithCourseA(r, [s, leaver], createdLongAgo), ct);
        await using var later = await RunAgainAtAsync(first, LaterRun, r => WithCourseA(r, [s], createdLongAgo), ct);

        var stayed = await later.MembershipAsync(CourseA, s.GoogleUserId, ct);
        var left = await later.MembershipAsync(CourseA, leaver.GoogleUserId, ct);
        Assert.Equal((FirstRun, LaterRun, true), stayed);
        Assert.Equal((FirstRun, FirstRun, false), left);

        var purgesSoFar = (await later.PurgeAuditRowsAsync(ct)).Count;
        await using var next = await RunAgainAtAsync(later, LaterRun.AddDays(1), r => WithCourseA(r, [s], createdLongAgo), ct);
        await next.WaitForPurgeRunsAsync(purgesSoFar + 1, ct);

        Assert.Equal((FirstRun, FirstRun, false), await next.MembershipAsync(CourseA, leaver.GoogleUserId, ct));
    }

    /// <summary>
    /// AC-011, FR-002: after the erasure of one first-load student and another run, the other first-load student still
    /// counts from 2025-09-01 and the later arrival still from 2026-11-05.
    /// </summary>
    [Fact]
    public async Task SeenInTheFirstLoad_SurvivesTheErasureOfAnotherFirstLoadMember()
    {
        var ct = TestContext.Current.CancellationToken;
        var s1 = Person(2);
        var s2 = Person(3);
        var n = Person(5);
        await using var first = await StartFirstRunAsync(database, r => WithCourseA(r, [s1, s2], CourseCreated), ct);
        await using var later = await RunAgainAtAsync(first, LaterRun, r => WithCourseA(r, [s1, s2, n], CourseCreated), ct);
        var s1Id = await later.ScalarAsync<long>(
            "SELECT id FROM classroom_participant WHERE google_user_id = @id", ct, ("id", s1.GoogleUserId));
        var erased = await later.EraseAsync(await later.InsertAdminAsync(ct), s1Id, ct);
        Assert.Equal(ParticipantErasureOutcome.Erased, erased.Outcome);
        await using var next = await RunAgainAtAsync(later, LaterRun.AddDays(1), r => WithCourseA(r, [s1, s2, n], CourseCreated), ct);
        var dean = await JournalHostExtensions.SignInAsync(next, Actor.Dean, ct);

        var year2025 = await JournalAsync(dean, next, CourseA, "2025-09-01", "2025-12-31", ct);
        var october2026 = await JournalAsync(dean, next, CourseA, "2026-10-01", "2026-10-31", ct);

        Lists(year2025, s2);
        DoesNotList(year2025, s1);
        DoesNotList(year2025, n);
        Lists(october2026, s2);
        DoesNotList(october2026, n);
    }
}
