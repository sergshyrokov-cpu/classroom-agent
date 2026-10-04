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
/// US-027 AC-012, FR-004, FR-020, db-design §5.1: the report's journal-field reads over real PostgreSQL (TC-2) — the
/// lesson date (scheduled time, else creation time, else item date), the half-open period on it, the submissions
/// that follow the lessons of the period, both roles of the members, and one command per port method.
/// </summary>
public sealed class JournalFieldSourceTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset Start = JournalTestData.Period.StartUtc;

    private static readonly DateTimeOffset End = JournalTestData.Period.EndUtc;

    [Fact]
    public async Task TheLessonDate_IsScheduledThenCreationThenItemDate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var scheduled = new DateTimeOffset(2026, 9, 10, 6, 0, 0, TimeSpan.Zero);
        var created = new DateTimeOffset(2026, 9, 12, 7, 0, 0, TimeSpan.Zero);
        var itemOnly = new DateTimeOffset(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);
        var viaScheduled = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(1), itemDate: itemOnly,
            scheduledTime: scheduled, creationTime: new DateTimeOffset(2026, 8, 1, 7, 0, 0, TimeSpan.Zero));
        var viaCreation = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(2), itemDate: new DateTimeOffset(2026, 8, 2, 8, 0, 0, TimeSpan.Zero),
            creationTime: created);
        var viaItemDate = await host.InsertLessonAsync(course, ct, CourseWorkTestData.ItemId(3), itemDate: itemOnly);

        await host.WithJournalFieldSourceAsync(async (source, _) =>
        {
            var lessons = await source.GetLessonsAsync(course, Start, End, ct);

            Assert.Equal(3, lessons.Count);
            Assert.Equal(scheduled, Assert.Single(lessons, l => l.Id == viaScheduled).LessonDate);
            Assert.Equal(created, Assert.Single(lessons, l => l.Id == viaCreation).LessonDate);
            Assert.Equal(itemOnly, Assert.Single(lessons, l => l.Id == viaItemDate).LessonDate);
        });
    }

    [Fact]
    public async Task ThePeriod_IsHalfOpen_ByLessonDate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var other = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(2));
        var atStart = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(1), itemDate: Start.AddDays(-20), scheduledTime: Start);
        var atEnd = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(2), itemDate: Start.AddDays(5), creationTime: End);
        var itemDateInsideOnly = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(3), itemDate: JournalTestData.Period.Early, scheduledTime: End.AddDays(3));
        var justBefore = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(4), itemDate: Start.AddDays(5), scheduledTime: Start.AddTicks(-1));
        var lessonDateInsideOnly = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(5), itemDate: End.AddDays(9), creationTime: JournalTestData.Period.Late);
        var elsewhere = await host.InsertLessonAsync(
            other, ct, CourseWorkTestData.ItemId(6), itemDate: JournalTestData.Period.Early);

        await host.WithJournalFieldSourceAsync(async (source, _) =>
        {
            var lessons = await source.GetLessonsAsync(course, Start, End, ct);

            Assert.Equal([atStart, lessonDateInsideOnly], lessons.Select(l => l.Id).Order().ToArray());
            Assert.DoesNotContain(lessons, l => l.Id == atEnd || l.Id == itemDateInsideOnly || l.Id == justBefore || l.Id == elsewhere);
        });
    }

    [Fact]
    public async Task TheExamplesOfAc012_HoldInKyiv()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var octoberStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 10, 1), JournalTestData.Kyiv), TimeSpan.Zero);
        var octoberEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, 1), JournalTestData.Kyiv), TimeSpan.Zero);
        var published = new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);
        var dueNextMonth = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(1), itemDate: published, creationTime: published,
            dueAt: new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero), maxPoints: 10m);
        var afterMidnightKyiv = new DateTimeOffset(2026, 8, 31, 21, 30, 0, TimeSpan.Zero); // 2026-09-01 00:30 in Kyiv
        var firstMinutes = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(2), itemDate: afterMidnightKyiv, creationTime: afterMidnightKyiv);
        var scheduledNextMonth = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(3), itemDate: new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero),
            creationTime: new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero),
            scheduledTime: new DateTimeOffset(2026, 10, 1, 6, 0, 0, TimeSpan.Zero)); // 2026-10-01 09:00 in Kyiv

        await host.WithJournalFieldSourceAsync(async (source, _) =>
        {
            var september = (await source.GetLessonsAsync(course, Start, End, ct)).Select(l => l.Id).Order().ToArray();
            var october = (await source.GetLessonsAsync(course, octoberStart, octoberEnd, ct)).Select(l => l.Id).Order().ToArray();

            Assert.Equal([dueNextMonth, firstMinutes], september);
            Assert.Equal([scheduledNextMonth], october);
        });
    }

    [Fact]
    public async Task Submissions_FollowTheLessonsOfThePeriod()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var other = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(2));
        var student = await CourseRows.InsertParticipantAsync(host, ct);
        var inByScheduled = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(1), itemDate: Start.AddDays(-40), scheduledTime: JournalTestData.Period.Early);
        var outByScheduled = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(2), itemDate: JournalTestData.Period.Early, scheduledTime: End.AddDays(2));
        var atEnd = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(3), itemDate: JournalTestData.Period.Early, creationTime: End);
        var elsewhere = await host.InsertLessonAsync(
            other, ct, CourseWorkTestData.ItemId(4), itemDate: JournalTestData.Period.Early);
        var wanted = await CourseWorkRows.InsertSubmissionAsync(
            host, inByScheduled, student, ct, googleId: CourseWorkTestData.SubmissionId(1));
        await CourseWorkRows.InsertSubmissionAsync(host, outByScheduled, student, ct, googleId: CourseWorkTestData.SubmissionId(2));
        await CourseWorkRows.InsertSubmissionAsync(host, atEnd, student, ct, googleId: CourseWorkTestData.SubmissionId(3));
        await CourseWorkRows.InsertSubmissionAsync(host, elsewhere, student, ct, googleId: CourseWorkTestData.SubmissionId(4));

        await host.WithJournalFieldSourceAsync(async (source, _) =>
        {
            var lessons = await source.GetLessonsAsync(course, Start, End, ct);
            var submissions = await source.GetLessonSubmissionsAsync(course, Start, End, ct);

            Assert.Equal([inByScheduled], lessons.Select(l => l.Id).ToArray());
            var only = Assert.Single(submissions);
            Assert.Equal(wanted, only.Id);
            Assert.Equal(inByScheduled, only.ItemId);
            Assert.Equal(student, only.ParticipantId);
        });
    }

    [Fact]
    public async Task Members_ComeWithTheirRole()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var other = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(2));
        var student = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(1), email: CourseTestData.Email("student.one"), fullName: "Test Student One");
        var teacher = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(2), email: CourseTestData.Email("teacher.one"), fullName: null);
        var elsewhere = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(3), email: CourseTestData.Email("student.three"), fullName: "Test Student Three");
        var first = Start.AddDays(-30);
        var last = Start.AddDays(3);
        await CourseRows.InsertMembershipAsync(host, course, student, ct, firstSeenAt: first, lastSeenAt: last);
        await CourseRows.InsertMembershipAsync(
            host, course, teacher, ct, role: CourseTestData.Roles.Teacher, firstSeenAt: first, lastSeenAt: last, onRoster: false);
        await CourseRows.InsertMembershipAsync(host, other, elsewhere, ct);

        await host.WithJournalFieldSourceAsync(async (source, _) =>
        {
            var members = await source.GetMembersAsync(course, ct);

            Assert.Equal([student, teacher], members.Select(m => m.ParticipantId).Order().ToArray());
            var s = Assert.Single(members, m => m.ParticipantId == student);
            Assert.Equal(ClassroomRole.Student, s.Role);
            Assert.True(s.OnRoster);
            Assert.Equal("Test Student One", s.FullName);
            Assert.Equal(CourseTestData.Email("student.one"), s.Email);
            Assert.Equal(first, s.FirstSeenAt);
            Assert.Equal(last, s.LastSeenAt);
            var t = Assert.Single(members, m => m.ParticipantId == teacher);
            Assert.Equal(ClassroomRole.Teacher, t.Role);
            Assert.False(t.OnRoster);
            Assert.Null(t.FullName);
            Assert.Equal(CourseTestData.Email("teacher.one"), t.Email);
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
        var item = await host.InsertLessonAsync(course, ct, CourseWorkTestData.ItemId(1), itemDate: JournalTestData.Period.Early);
        await CourseWorkRows.InsertSubmissionAsync(host, item, student, ct);

        var counter = new CountingInterceptor();
        var builder = new DbContextOptionsBuilder<ClassroomAgentDbContext>();
        ClassroomAgentDbContextOptions.Configure(builder, host.ConnectionString);
        builder.AddInterceptors(counter);
        await using var ctx = new ClassroomAgentDbContext(builder.Options);
        IJournalFieldSource source = new JournalFieldSource(ctx);

        var before = counter.Count;
        Assert.NotEmpty(await source.GetCoursesAsync(ct));
        Assert.Equal(before + 1, counter.Count);

        before = counter.Count;
        Assert.NotEmpty(await source.GetLessonsAsync(course, Start, End, ct));
        Assert.Equal(before + 1, counter.Count);

        before = counter.Count;
        Assert.NotEmpty(await source.GetMembersAsync(course, ct));
        Assert.Equal(before + 1, counter.Count);

        before = counter.Count;
        Assert.NotEmpty(await source.GetLessonSubmissionsAsync(course, Start, End, ct));
        Assert.Equal(before + 1, counter.Count);
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
