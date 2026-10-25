using ClassroomAgent.Application.MeetLinking;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-056 FR-001, FR-003, FR-005 (db-design §1, §3; entity model §3) over real PostgreSQL (TC-2): every reader that feeds a
/// member-in-period or member-on-date decision carries the membership's start — the course's <c>creation_time</c> for a
/// membership first seen no later than the course row's <c>created_at</c>, else its first-seen instant — and the raw-SQL
/// candidate test of the unassigned Meet codes list agrees with <see cref="MembershipStart"/> and
/// <see cref="RosterOnDate"/> on the same rows. Rows are seeded directly, as a run would have stored them.
/// </summary>
public sealed class MembershipStartReadersTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset CreationTime = new(2025, 9, 1, 6, 0, 0, TimeSpan.Zero);

    /// <summary>The course row's <c>created_at</c>: the save instant of its first committed load.</summary>
    private static readonly DateTimeOffset Saved = new(2026, 9, 10, 7, 0, 0, TimeSpan.Zero);

    /// <summary>The first-loading run's <c>observedAt</c>, a few seconds before the save.</summary>
    private static readonly DateTimeOffset FirstLoad = Saved.AddSeconds(-5);

    /// <summary>A later run's <c>observedAt</c>.</summary>
    private static readonly DateTimeOffset LaterRun = new(2026, 9, 12, 7, 0, 0, TimeSpan.Zero);

    private sealed record Seeded(long Course, long NoCreationCourse, long FirstLoadStudent, long LaterStudent, long NoCreationStudent, long FirstLoadTeacher);

    private static async Task<(InstallationTestHost Host, Seeded Rows)> SeedAsync(PostgreSqlFixture database, CancellationToken ct)
    {
        var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct, creationTime: CreationTime, createdAt: Saved);
        var bare = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(2), createdAt: Saved);
        var firstLoad = await PersonAsync(host, 1, ct);
        var later = await PersonAsync(host, 2, ct);
        var noCreation = await PersonAsync(host, 3, ct);
        var teacher = await PersonAsync(host, 4, ct);
        await CourseRows.InsertMembershipAsync(host, course, firstLoad, ct, firstSeenAt: FirstLoad);
        await CourseRows.InsertMembershipAsync(host, course, later, ct, firstSeenAt: LaterRun);
        await CourseRows.InsertMembershipAsync(host, course, teacher, ct, role: CourseTestData.Roles.Teacher, firstSeenAt: FirstLoad);
        await CourseRows.InsertMembershipAsync(host, bare, noCreation, ct, firstSeenAt: FirstLoad);
        return (host, new Seeded(course, bare, firstLoad, later, noCreation, teacher));
    }

    private static Task<long> PersonAsync(InstallationTestHost host, int ordinal, CancellationToken ct) =>
        CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(ordinal), email: CourseTestData.Email($"person{ordinal}"),
            fullName: CourseTestData.Name(ordinal));

    /// <summary>FR-003.1, FR-005: the journal's student read carries the start (a for the first load, c for a later arrival, b without creationTime).</summary>
    [Fact]
    public async Task TheJournalStudentRead_CarriesTheStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, rows) = await SeedAsync(database, ct);
        await using var _ = host;

        await host.WithJournalSourceAsync(async (source, _) =>
        {
            var members = await source.GetStudentMembersAsync(rows.Course, ct);
            var bare = await source.GetStudentMembersAsync(rows.NoCreationCourse, ct);

            Assert.Equal(CreationTime, Assert.Single(members, m => m.ParticipantId == rows.FirstLoadStudent).StartsAt);
            Assert.Equal(LaterRun, Assert.Single(members, m => m.ParticipantId == rows.LaterStudent).StartsAt);
            Assert.Equal(FirstLoad, Assert.Single(bare).StartsAt);
        });
    }

    /// <summary>FR-003.1, FR-003.2: the journal field / report member read carries the start, teachers included.</summary>
    [Fact]
    public async Task TheJournalFieldMemberRead_CarriesTheStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, rows) = await SeedAsync(database, ct);
        await using var _ = host;

        await host.WithJournalFieldSourceAsync(async (source, _) =>
        {
            var members = await source.GetMembersAsync(rows.Course, ct);
            var bare = await source.GetMembersAsync(rows.NoCreationCourse, ct);

            Assert.Equal(CreationTime, Assert.Single(members, m => m.ParticipantId == rows.FirstLoadStudent).StartsAt);
            Assert.Equal(CreationTime, Assert.Single(members, m => m.ParticipantId == rows.FirstLoadTeacher).StartsAt);
            Assert.Equal(LaterRun, Assert.Single(members, m => m.ParticipantId == rows.LaterStudent).StartsAt);
            Assert.Equal(FirstLoad, Assert.Single(bare).StartsAt);
        });
    }

    /// <summary>FR-003.3: the Meet report member read carries the start.</summary>
    [Fact]
    public async Task TheMeetReportMemberRead_CarriesTheStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, rows) = await SeedAsync(database, ct);
        await using var _ = host;

        await host.WithMeetReportSourceAsync(async source =>
        {
            var members = await source.GetCourseMembersAsync(rows.Course, ct);
            var bare = await source.GetCourseMembersAsync(rows.NoCreationCourse, ct);

            Assert.Equal(CreationTime, Assert.Single(members, m => m.ParticipantId == rows.FirstLoadStudent).StartsAt);
            Assert.Equal(CreationTime, Assert.Single(members, m => m.ParticipantId == rows.FirstLoadTeacher).StartsAt);
            Assert.Equal(LaterRun, Assert.Single(members, m => m.ParticipantId == rows.LaterStudent).StartsAt);
            Assert.Equal(FirstLoad, Assert.Single(bare).StartsAt);
        });
    }

    /// <summary>FR-003.4: the Meet code scoring read carries the start of every membership of the code's people.</summary>
    [Fact]
    public async Task TheMeetCodeScoringRead_CarriesTheStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, rows) = await SeedAsync(database, ct);
        await using var _ = host;
        var code = MeetTestData.MeetingCode(1);
        await host.SeedMeetingAsync(
            code, CourseTestData.Email("person4"), new DateTimeOffset(2025, 10, 15, 9, 0, 0, TimeSpan.Zero), ct,
            [CourseTestData.Email("person1"), CourseTestData.Email("person2"), CourseTestData.Email("person3")]);

        using var scope = host.CreateScope();
        var source = new MeetCodeScoringSource(scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>());
        var input = Assert.Single(await source.GetScoringInputsAsync([code], ct));

        RosterMembership Of(string local) =>
            Assert.Single(input.Memberships, m => string.Equals(m.Email, CourseTestData.Email(local), StringComparison.OrdinalIgnoreCase));
        Assert.Equal(CreationTime, Of("person4").StartsAt);
        Assert.Equal(CreationTime, Of("person1").StartsAt);
        Assert.Equal(LaterRun, Of("person2").StartsAt);
        Assert.Equal(FirstLoad, Of("person3").StartsAt);
    }

    /// <summary>
    /// FR-003.5, AC-004 second paragraph (db-design §3): the raw-SQL candidate test orders the unassigned codes exactly as
    /// <see cref="MembershipStart"/> with <see cref="RosterOnDate"/> decide on the same rows — the first load's teacher's
    /// code of 2025 has a candidate; a later arrival's code before they were seen, a first-load teacher's code before the
    /// first run of a course without creationTime, and a non-member's code have none.
    /// </summary>
    [Fact]
    public async Task TheUnassignedCodesOrder_AgreesWithTheStartRule()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, rows) = await SeedAsync(database, ct);
        await using var _ = host;
        var laterTeacher = await PersonAsync(host, 5, ct);
        var bareTeacher = await PersonAsync(host, 6, ct);
        await CourseRows.InsertMembershipAsync(host, rows.Course, laterTeacher, ct, role: CourseTestData.Roles.Teacher, firstSeenAt: LaterRun);
        await CourseRows.InsertMembershipAsync(host, rows.NoCreationCourse, bareTeacher, ct, role: CourseTestData.Roles.Teacher, firstSeenAt: FirstLoad);
        var meetings = new (string Code, string Organizer, DateTimeOffset StartsAt, long? Course, DateTimeOffset FirstSeen, DateTimeOffset? Creation)[]
        {
            (MeetTestData.MeetingCode(1), CourseTestData.Email("person4"), new(2025, 10, 15, 9, 0, 0, TimeSpan.Zero), rows.Course, FirstLoad, CreationTime),
            (MeetTestData.MeetingCode(2), CourseTestData.Email("person5"), new(2026, 9, 11, 9, 0, 0, TimeSpan.Zero), rows.Course, LaterRun, CreationTime),
            (MeetTestData.MeetingCode(3), CourseTestData.Email("person6"), new(2026, 9, 9, 9, 0, 0, TimeSpan.Zero), rows.NoCreationCourse, FirstLoad, null),
            (MeetTestData.MeetingCode(4), CourseTestData.Email("nobody"), new(2026, 9, 8, 9, 0, 0, TimeSpan.Zero), null, default, null),
        };
        foreach (var m in meetings)
        {
            await host.SeedMeetingAsync(m.Code, m.Organizer, m.StartsAt, ct);
        }

        var zone = JournalTestData.Kyiv;
        var expected = meetings
            .OrderByDescending(m => m.Course is { } course && RosterOnDate.Covers(
                new RosterMembership(course, ClassroomRole.Teacher, m.Organizer, MembershipStart.Of(m.FirstSeen, Saved, m.Creation), host.Time.GetUtcNow(), true),
                RosterOnDate.LocalDate(m.StartsAt, zone),
                zone))
            .ThenByDescending(m => m.StartsAt)
            .Select(m => m.Code)
            .ToList();

        using var scope = host.CreateScope();
        var source = new MeetCodesReadSource(scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>());
        var page = await source.GetUnassignedPageAsync(0, 10, zone, ct);

        Assert.Equal(
            [MeetTestData.MeetingCode(1), MeetTestData.MeetingCode(2), MeetTestData.MeetingCode(3), MeetTestData.MeetingCode(4)],
            expected);
        Assert.Equal(expected, page.Rows.Select(r => r.MeetingCode).ToList());
    }
}
