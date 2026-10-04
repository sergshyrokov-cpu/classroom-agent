using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Infrastructure.Persistence.Repositories;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-025 db-design §2 and §7: the four journal reads over real PostgreSQL (TC-2) — scope, the half-open period,
/// duplicates, the kind mapping, the UTC-bound guard, one command per method, no tracking, no schema change.
/// </summary>
public sealed class JournalSourceTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset NonUtcStart = new(2026, 9, 1, 0, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public async Task GetCourses_ReturnsEveryCourseIncludingArchived_WithNameAndSection()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var active = await CourseRows.InsertCourseAsync(host, ct, name: "Test Course Active", section: "Test Section X");
        var archived = await CourseRows.InsertCourseAsync(
            host, ct, googleId: CourseTestData.CourseId(2), name: "Test Course Archived", state: CourseTestData.States.Archived);

        await host.WithJournalSourceAsync(async (source, _) =>
        {
            var courses = await source.GetCoursesAsync(ct);

            Assert.Equal(2, courses.Count);
            var a = Assert.Single(courses, c => c.Id == active);
            Assert.Equal("Test Course Active", a.Name);
            Assert.Equal("Test Section X", a.Section);
            var b = Assert.Single(courses, c => c.Id == archived);
            Assert.Equal("Test Course Archived", b.Name);
            Assert.Null(b.Section);
        });
    }

    [Fact]
    public async Task GetItems_ReturnsOnlyItemsOfTheCourseInTheHalfOpenPeriod()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var other = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(2));
        var atStart = await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(1), itemDate: JournalTestData.Period.StartUtc);
        var inside = await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(2), itemDate: JournalTestData.Period.Early);
        await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(3), itemDate: JournalTestData.Period.EndUtc);
        await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(4), itemDate: JournalTestData.Period.StartUtc.AddTicks(-1));
        await CourseWorkRows.InsertCourseWorkAsync(
            host, other, ct, googleId: CourseWorkTestData.ItemId(5), itemDate: JournalTestData.Period.Early);

        await host.WithJournalSourceAsync(async (source, _) =>
        {
            var items = await source.GetItemsAsync(course, JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc, ct);

            Assert.Equal([atStart, inside], items.Select(i => i.Id).Order().ToArray());
        });
    }

    [Fact]
    public async Task GetItems_MapsKindAndCarriesTheColumnFields()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var graded = await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(1), title: "Test Graded",
            itemDate: JournalTestData.Period.Early, dueAt: JournalTestData.Period.Late, maxPoints: 10m);
        var ungraded = await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(2), title: "Test Ungraded", itemDate: JournalTestData.Period.Early);
        var material = await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(3), title: "Test Material",
            resource: CourseWorkTestData.ResourceCodes.CourseWorkMaterial, itemDate: JournalTestData.Period.Early);

        await host.WithJournalSourceAsync(async (source, _) =>
        {
            var items = await source.GetItemsAsync(course, JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc, ct);

            Assert.Equal(3, items.Count);
            var g = Assert.Single(items, i => i.Id == graded);
            Assert.Equal(CourseWorkKind.GradedWork, g.Kind);
            Assert.Equal("Test Graded", g.Title);
            Assert.Equal(JournalTestData.Period.Early, g.ItemDate);
            Assert.Equal(JournalTestData.Period.Late, g.DueAt);
            Assert.Equal(10m, g.MaxPoints);
            var u = Assert.Single(items, i => i.Id == ungraded);
            Assert.Equal(CourseWorkKind.UngradedWork, u.Kind);
            Assert.Null(u.MaxPoints);
            Assert.Null(u.DueAt);
            Assert.Equal(CourseWorkKind.Material, Assert.Single(items, i => i.Id == material).Kind);
        });
    }

    [Fact]
    public async Task GetStudentMembers_ReturnsStudentsOfTheCourse_LeaversIncluded_TeachersAndOtherCoursesExcluded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var other = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(2));
        var onRoster = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(1), email: CourseTestData.Email("student.one"), fullName: "Test Student One");
        var leaver = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(2), email: CourseTestData.Email("student.two"), fullName: null);
        var teacher = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(3), email: CourseTestData.Email("teacher.one"), fullName: "Test Teacher One");
        var elsewhere = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(4), email: CourseTestData.Email("student.four"), fullName: "Test Student Four");
        var first = JournalTestData.Period.StartUtc.AddDays(-30);
        var last = JournalTestData.Period.StartUtc.AddDays(3);
        await CourseRows.InsertMembershipAsync(host, course, onRoster, ct, firstSeenAt: first, lastSeenAt: last);
        await CourseRows.InsertMembershipAsync(host, course, leaver, ct, firstSeenAt: first, lastSeenAt: last, onRoster: false);
        await CourseRows.InsertMembershipAsync(host, course, teacher, ct, role: CourseTestData.Roles.Teacher);
        await CourseRows.InsertMembershipAsync(host, other, elsewhere, ct);

        await host.WithJournalSourceAsync(async (source, _) =>
        {
            var members = await source.GetStudentMembersAsync(course, ct);

            Assert.Equal([onRoster, leaver], members.Select(m => m.ParticipantId).Order().ToArray());
            var a = Assert.Single(members, m => m.ParticipantId == onRoster);
            Assert.True(a.OnRoster);
            Assert.Equal("Test Student One", a.FullName);
            Assert.Equal(CourseTestData.Email("student.one"), a.Email);
            Assert.Equal(first, a.FirstSeenAt);
            Assert.Equal(last, a.LastSeenAt);
            var b = Assert.Single(members, m => m.ParticipantId == leaver);
            Assert.False(b.OnRoster);
            Assert.Null(b.FullName);
            Assert.Equal(CourseTestData.Email("student.two"), b.Email);
        });
    }

    [Fact]
    public async Task GetSubmissions_ReturnsOnlySubmissionsToItemsOfTheCourseInTheHalfOpenPeriod()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var other = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(2));
        var student = await CourseRows.InsertParticipantAsync(host, ct);
        var atStart = await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(1), itemDate: JournalTestData.Period.StartUtc);
        var atEnd = await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(2), itemDate: JournalTestData.Period.EndUtc);
        var before = await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(3), itemDate: JournalTestData.Period.StartUtc.AddTicks(-1));
        var elsewhere = await CourseWorkRows.InsertCourseWorkAsync(
            host, other, ct, googleId: CourseWorkTestData.ItemId(4), itemDate: JournalTestData.Period.Early);
        var wanted = await CourseWorkRows.InsertSubmissionAsync(
            host, atStart, student, ct, googleId: CourseWorkTestData.SubmissionId(1));
        await CourseWorkRows.InsertSubmissionAsync(host, atEnd, student, ct, googleId: CourseWorkTestData.SubmissionId(2));
        await CourseWorkRows.InsertSubmissionAsync(host, before, student, ct, googleId: CourseWorkTestData.SubmissionId(3));
        await CourseWorkRows.InsertSubmissionAsync(host, elsewhere, student, ct, googleId: CourseWorkTestData.SubmissionId(4));

        await host.WithJournalSourceAsync(async (source, _) =>
        {
            var submissions = await source.GetSubmissionsAsync(course, JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc, ct);

            var only = Assert.Single(submissions);
            Assert.Equal(wanted, only.Id);
            Assert.Equal(atStart, only.ItemId);
            Assert.Equal(student, only.ParticipantId);
        });
    }

    [Fact]
    public async Task GetSubmissions_CarriesTheCellFields_AndTheRawStateOfAnUnrecognisedOne()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var student = await CourseRows.InsertParticipantAsync(host, ct);
        var item = await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, itemDate: JournalTestData.Period.Early, maxPoints: 10m);
        var other = await CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(2), itemDate: JournalTestData.Period.Early, maxPoints: 10m);
        var graded = await CourseWorkRows.InsertSubmissionAsync(
            host, item, student, ct, googleId: CourseWorkTestData.SubmissionId(1), state: CourseWorkTestData.StateCodes.TurnedIn,
            assignedGrade: 8.5m, draftGrade: 6.25m, turnedInAt: JournalTestData.Period.Early.AddHours(2), late: true);
        var unrecognised = await CourseWorkRows.InsertSubmissionAsync(
            host, other, student, ct, googleId: CourseWorkTestData.SubmissionId(2), state: CourseWorkTestData.StateCodes.Unrecognised,
            rawState: CourseWorkTestData.UnrecognisedState);

        await host.WithJournalSourceAsync(async (source, _) =>
        {
            var submissions = await source.GetSubmissionsAsync(course, JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc, ct);

            var g = Assert.Single(submissions, s => s.Id == graded);
            Assert.Equal(SubmissionState.TurnedIn, g.State);
            Assert.Null(g.RawState);
            Assert.Equal(8.5m, g.AssignedGrade);
            Assert.Equal(6.25m, g.DraftGrade);
            Assert.Equal(JournalTestData.Period.Early.AddHours(2), g.TurnedInAt);
            Assert.True(g.Late);
            var u = Assert.Single(submissions, s => s.Id == unrecognised);
            Assert.Equal(SubmissionState.Unrecognised, u.State);
            Assert.Equal(CourseWorkTestData.UnrecognisedState, u.RawState);
        });
    }

    [Fact]
    public async Task GetSubmissions_ReturnsBothSubmissionsOfOneStudentAndItem_WithIdAndUpdateTime()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var student = await CourseRows.InsertParticipantAsync(host, ct);
        var item = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, itemDate: JournalTestData.Period.Early);
        var older = JournalTestData.Period.Early.AddHours(1);
        var newer = JournalTestData.Period.Early.AddHours(5);
        var first = await CourseWorkRows.InsertSubmissionAsync(
            host, item, student, ct, googleId: CourseWorkTestData.SubmissionId(1), updateTime: older);
        var second = await CourseWorkRows.InsertSubmissionAsync(
            host, item, student, ct, googleId: CourseWorkTestData.SubmissionId(2), updateTime: newer);

        await host.WithJournalSourceAsync(async (source, _) =>
        {
            var submissions = await source.GetSubmissionsAsync(course, JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc, ct);

            Assert.Equal(2, submissions.Count);
            Assert.Equal(older, Assert.Single(submissions, s => s.Id == first).UpdateTime);
            Assert.Equal(newer, Assert.Single(submissions, s => s.Id == second).UpdateTime);
        });
    }

    [Fact]
    public async Task ABoundWithANonZeroOffset_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership

        await host.WithJournalSourceAsync(async (source, _) =>
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => source.GetItemsAsync(1, NonUtcStart, JournalTestData.Period.EndUtc, ct));
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => source.GetSubmissionsAsync(1, NonUtcStart, JournalTestData.Period.EndUtc, ct));
        });
    }

    [Fact]
    public async Task AnEmptyOrInvertedPeriod_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var start = JournalTestData.Period.StartUtc;

        await host.WithJournalSourceAsync(async (source, _) =>
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() => source.GetItemsAsync(1, start, start, ct));
            await Assert.ThrowsAnyAsync<ArgumentException>(() => source.GetSubmissionsAsync(1, start, start, ct));
            await Assert.ThrowsAnyAsync<ArgumentException>(() => source.GetItemsAsync(1, JournalTestData.Period.EndUtc, start, ct));
            await Assert.ThrowsAnyAsync<ArgumentException>(() => source.GetSubmissionsAsync(1, JournalTestData.Period.EndUtc, start, ct));
        });
    }

    [Fact]
    public async Task NothingIsTracked_AfterAllFourReads()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var student = await CourseRows.InsertParticipantAsync(host, ct);
        await CourseRows.InsertMembershipAsync(host, course, student, ct);
        var item = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, itemDate: JournalTestData.Period.Early);
        await CourseWorkRows.InsertSubmissionAsync(host, item, student, ct);

        await host.WithJournalSourceAsync(async (source, db) =>
        {
            Assert.NotEmpty(await source.GetCoursesAsync(ct));
            Assert.NotEmpty(await source.GetItemsAsync(course, JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc, ct));
            Assert.NotEmpty(await source.GetStudentMembersAsync(course, ct));
            Assert.NotEmpty(await source.GetSubmissionsAsync(course, JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc, ct));

            Assert.Empty(db.ChangeTracker.Entries());
        });
    }

    [Fact]
    public async Task EachPortMethod_IssuesExactlyOneDatabaseCommand()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var student = await CourseRows.InsertParticipantAsync(host, ct);
        await CourseRows.InsertMembershipAsync(host, course, student, ct);
        var item = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, itemDate: JournalTestData.Period.Early);
        await CourseWorkRows.InsertSubmissionAsync(host, item, student, ct);

        var counter = new CountingInterceptor();
        var builder = new DbContextOptionsBuilder<ClassroomAgentDbContext>();
        ClassroomAgentDbContextOptions.Configure(builder, host.ConnectionString);
        builder.AddInterceptors(counter);
        await using var ctx = new ClassroomAgentDbContext(builder.Options);
        IJournalSource source = new JournalSource(ctx);

        var before = counter.Count;
        Assert.NotEmpty(await source.GetCoursesAsync(ct));
        Assert.Equal(before + 1, counter.Count);

        before = counter.Count;
        Assert.NotEmpty(await source.GetItemsAsync(course, JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc, ct));
        Assert.Equal(before + 1, counter.Count);

        before = counter.Count;
        Assert.NotEmpty(await source.GetStudentMembersAsync(course, ct));
        Assert.Equal(before + 1, counter.Count);

        before = counter.Count;
        Assert.NotEmpty(await source.GetSubmissionsAsync(course, JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc, ct));
        Assert.Equal(before + 1, counter.Count);
    }

    [Fact]
    public async Task TheModelHasNoPendingChanges_SoNoMigrationIsNeeded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership

        Assert.False(host.HasPendingModelChanges());
    }

    private sealed class CountingInterceptor : DbCommandInterceptor
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Interlocked.Increment(ref _count);
            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Interlocked.Increment(ref _count);
            return base.NonQueryExecuting(command, eventData, result);
        }
    }
}
