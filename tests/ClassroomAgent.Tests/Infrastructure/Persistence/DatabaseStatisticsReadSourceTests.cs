using System.Data.Common;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Infrastructure.Persistence.Repositories;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ds = ClassroomAgent.Tests.TestInfrastructure.DatabaseStatisticsHostExtensions;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-024 db-design §2–§3, Q1 and Q2 over real PostgreSQL with the migrations applied (TC-2): each of the eleven
/// counters against seeded rows, the empty school, agreement with the Meet codes page's Unassigned count, a fixed
/// number of database commands whatever the size of the school, and nothing written (AC-002 … AC-004, AC-006,
/// AC-015, AC-017, AC-018). Rows are inserted by SQL.
/// </summary>
public sealed class DatabaseStatisticsReadSourceTests(PostgreSqlFixture database)
{
    private async Task<InstallationTestHost> StartEmptyAsync(CancellationToken ct)
    {
        var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        return host;
    }

    /// <summary>
    /// AC-002 … AC-004, FR-002 … FR-004, I-4: the seeded world gives every counter its expected value — five states,
    /// four teachers, three students, coursework and submissions of an archived course and of a leaver counted.
    /// </summary>
    [Fact]
    public async Task EveryCounter_HasItsSeededValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.SeedStatisticsWorldAsync(ct);

        DatabaseCounts? counts = null;
        await host.WithDatabaseStatisticsSourceAsync(async source => counts = await source.GetCountsAsync(ct));

        Assert.Equal(
            new DatabaseCounts(
                Ds.Expected.Active,
                Ds.Expected.Archived,
                Ds.Expected.Provisioned,
                Ds.Expected.Declined,
                Ds.Expected.Suspended,
                Ds.Expected.Teachers,
                Ds.Expected.Students,
                Ds.Expected.CourseWorkItems,
                Ds.Expected.Submissions,
                Ds.Expected.MeetMeetings,
                Ds.Expected.MeetCodesAwaitingDecision),
            counts);
    }

    /// <summary>AC-006, FR-002 … FR-004: an empty school — every counter is zero.</summary>
    [Fact]
    public async Task AnEmptySchool_CountsZeroEverywhere()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);

        DatabaseCounts? counts = null;
        await host.WithDatabaseStatisticsSourceAsync(async source => counts = await source.GetCountsAsync(ct));

        Assert.Equal(new DatabaseCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), counts);
    }

    /// <summary>
    /// AC-003, FR-003: a teacher, a student, a teacher-and-student and a leaver of every roster — teachers are the 1st
    /// and 3rd, students the 2nd and 3rd, the 4th in neither.
    /// </summary>
    [Fact]
    public async Task TheFourPeopleOfAc003_AreCountedByTheCurrentRoster()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var first = await host.InsertCourseAsync(1, "Курс 1", ct);
        var second = await host.InsertCourseAsync(2, "Курс 2", ct);
        var teacher = await host.InsertPersonAsync(1, "Шевченко", "Тарас", null, ct);
        var student = await host.InsertPersonAsync(2, "Мельник", "Петро", null, ct);
        var both = await host.InsertPersonAsync(3, "Українка", "Леся", null, ct);
        var leaver = await host.InsertPersonAsync(4, "Франко", "Іван", null, ct);
        await host.AddTeacherAsync(first, teacher, ct);
        await host.AddStudentAsync(first, student, ct);
        await host.AddTeacherAsync(first, both, ct);
        await host.AddStudentAsync(second, both, ct);
        await host.AddTeacherAsync(first, leaver, ct, onRoster: false);
        await host.AddStudentAsync(second, leaver, ct, onRoster: false);

        DatabaseCounts? counts = null;
        await host.WithDatabaseStatisticsSourceAsync(async source => counts = await source.GetCountsAsync(ct));

        Assert.NotNull(counts);
        Assert.Equal(2, counts.Teachers);
        Assert.Equal(2, counts.Students);
    }

    /// <summary>
    /// AC-015, FR-003: two current teacher memberships (one of an ARCHIVED course) and one current student membership —
    /// once a teacher, once a student.
    /// </summary>
    [Fact]
    public async Task APersonInSeveralCourses_IsCountedOncePerCounter_AndAnArchivedCourseCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var active = await host.InsertCourseAsync(1, "Курс 1", ct);
        var archived = await host.InsertCourseAsync(2, "Курс 2", ct, CourseTestData.States.Archived);
        var third = await host.InsertCourseAsync(3, "Курс 3", ct);
        var person = await host.InsertPersonAsync(1, "Коваль", "Олена", null, ct);
        await host.AddTeacherAsync(active, person, ct);
        await host.AddTeacherAsync(archived, person, ct);
        await host.AddStudentAsync(third, person, ct);

        DatabaseCounts? counts = null;
        await host.WithDatabaseStatisticsSourceAsync(async source => counts = await source.GetCountsAsync(ct));

        Assert.NotNull(counts);
        Assert.Equal(1, counts.Teachers);
        Assert.Equal(1, counts.Students);
    }

    /// <summary>
    /// FR-003, OD-003: a teacher of only a DECLINED or SUSPENDED course is still counted ("any state"); a participant
    /// with no membership at all is not.
    /// </summary>
    [Fact]
    public async Task TeachersOfAnyState_AreCounted_AndAParticipantWithoutMembership_IsNot()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var declined = await host.InsertCourseAsync(1, "Курс 1", ct, CourseTestData.States.Declined);
        var suspended = await host.InsertCourseAsync(2, "Курс 2", ct, CourseTestData.States.Suspended);
        await host.AddTeacherAsync(declined, await host.InsertPersonAsync(1, "Бондар", "Анна", null, ct), ct);
        await host.AddTeacherAsync(suspended, await host.InsertPersonAsync(2, "Гнатюк", "Олег", null, ct), ct);
        await host.InsertPersonAsync(3, "Нікого", "Немає", null, ct);

        DatabaseCounts? counts = null;
        await host.WithDatabaseStatisticsSourceAsync(async source => counts = await source.GetCountsAsync(ct));

        Assert.NotNull(counts);
        Assert.Equal(2, counts.Teachers);
        Assert.Equal(0, counts.Students);
    }

    /// <summary>
    /// AC-018, FR-004, db-design §3.1: the codes awaiting a decision equal the Meet codes page's Unassigned count on the
    /// same data — a code with two meetings once, linked and marked codes not at all.
    /// </summary>
    [Fact]
    public async Task TheCodesAwaitingDecision_EqualTheMeetCodesPagesUnassignedCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.SeedStatisticsWorldAsync(ct);

        DatabaseCounts? counts = null;
        MeetCodeListCounts? page = null;
        await host.WithDatabaseStatisticsSourceAsync(async source => counts = await source.GetCountsAsync(ct));
        await host.WithMeetCodesReadSourceAsync(async source => page = await source.GetCountsAsync(ct));

        Assert.NotNull(counts);
        Assert.NotNull(page);
        Assert.Equal(Ds.Expected.MeetCodesAwaitingDecision, counts.MeetCodesAwaitingDecision);
        Assert.Equal(page.Unassigned, counts.MeetCodesAwaitingDecision);
    }

    /// <summary>
    /// AC-018: linking the last two codes — one to a course, one as "not a course" — brings both numbers to zero
    /// together; the meetings are still counted.
    /// </summary>
    [Fact]
    public async Task OnceEveryCodeIsDecided_NoneAwaits_AndTheMeetingsStillCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedStatisticsWorldAsync(ct);
        var at = new DateTimeOffset(2026, 9, 8, 8, 0, 0, TimeSpan.Zero);
        await host.SeedAutoLinkAsync(Ds.TwiceMetCode, world.FirstActiveCourse, at, ct);
        await host.SeedMarkAsync(Ds.OnceMetCode, 1, at, ct);

        DatabaseCounts? counts = null;
        MeetCodeListCounts? page = null;
        await host.WithDatabaseStatisticsSourceAsync(async source => counts = await source.GetCountsAsync(ct));
        await host.WithMeetCodesReadSourceAsync(async source => page = await source.GetCountsAsync(ct));

        Assert.NotNull(counts);
        Assert.Equal(0, counts.MeetCodesAwaitingDecision);
        Assert.Equal(0, page!.Unassigned);
        Assert.Equal(Ds.Expected.MeetMeetings, counts.MeetMeetings);
    }

    /// <summary>
    /// AC-017, FR-011: the counters are one database command, and the whole page — counters and state through the
    /// production query — is the same number of commands with 1 and with 200 courses.
    /// </summary>
    [Fact]
    public async Task ThePage_IsAFixedNumberOfCommands_WithOneAndWithTwoHundredCourses()
    {
        var ct = TestContext.Current.CancellationToken;
        var small = await CommandsForAPageAsync(1, ct);
        var large = await CommandsForAPageAsync(200, ct);

        Assert.Equal(1, small.Counters);
        Assert.Equal(1, large.Counters);
        Assert.Equal(small.Page, large.Page);
        Assert.InRange(large.Page, 1, 2);
    }

    /// <summary>AC-017: the sized school is counted right, so the fixed command count is not an empty answer.</summary>
    [Fact]
    public async Task TheSizedSchool_IsCountedInFull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.SeedSizedSchoolAsync(200, ct);

        DatabaseCounts? counts = null;
        await host.WithDatabaseStatisticsSourceAsync(async source => counts = await source.GetCountsAsync(ct));

        Assert.Equal(new DatabaseCounts(200, 0, 0, 0, 0, 200, 200, 200, 200, 200, 200), counts);
    }

    /// <summary>FR-008, FR-011, S-04: reading the counters writes nothing.</summary>
    [Fact]
    public async Task Reading_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.SeedStatisticsWorldAsync(ct);
        await host.SeedSyncStateAsync("completed", ct, lastSuccessfulRunAt: new DateTimeOffset(2026, 3, 2, 6, 3, 0, TimeSpan.Zero));
        var before = await host.DatabaseStatisticsFingerprintAsync(ct);

        await host.WithDatabaseStatisticsSourceAsync(source => source.GetCountsAsync(ct));

        Assert.Equal(before, await host.DatabaseStatisticsFingerprintAsync(ct));
    }

    private async Task<(int Counters, int Page)> CommandsForAPageAsync(int courses, CancellationToken ct)
    {
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.SeedSizedSchoolAsync(courses, ct);
        await host.SeedSyncStateAsync("completed", ct, lastSuccessfulRunAt: new DateTimeOffset(2026, 3, 2, 6, 3, 0, TimeSpan.Zero));

        var counter = new CommandCounter();
        var options = ClassroomAgentDbContextOptions
            .Configure(new DbContextOptionsBuilder<ClassroomAgentDbContext>(), host.ConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(counter)
            .Options;
        await using var db = new ClassroomAgentDbContext((DbContextOptions<ClassroomAgentDbContext>)options);
        var source = new DatabaseStatisticsReadSource(db);

        await source.GetCountsAsync(ct);
        var counters = counter.Count;

        var query = new GetDatabaseStatisticsQuery(source, new SyncStateRepository(db), new SchoolTimeZone(JournalTestData.Kyiv));
        var before = counter.Count;
        await query.ExecuteAsync(AppRole.Dean, ct);

        return (counters, counter.Count - before);
    }

    /// <summary>Counts the database commands a context executes.</summary>
    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
