using System.Net;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-033 host and port seeding: one October world (a course with a teacher and three students, another course with its
/// own teacher and a student, linked, marked and unlinked codes, meetings in and around the period) inserted by SQL so
/// the tests do not depend on the production code under test (TC-2). Every name, email and code is invented (TC-4).
/// </summary>
public static class MeetReportsHostExtensions
{
    public const string CourseName = "Test Course Reports";

    public const string OtherCourseName = "Test Course Elsewhere";

    public const string ArchivedCourseName = "Test Course Archived Reports";

    public const string TeacherName = "Test Teacher One";

    public const string OtherTeacherName = "Test Teacher Two";

    public const string StudentOneName = "Test Student One";

    public const string StudentTwoName = "Test Student Two";

    public const string StudentThreeName = "Test Student Three";

    public const string ElsewhereName = "Test Person Elsewhere";

    public static string TeacherEmail => MeetReportsTestData.Email("teacher.one");

    public static string OtherTeacherEmail => MeetReportsTestData.Email("teacher.two");

    public static string StudentOneEmail => MeetReportsTestData.Email("student.one");

    public static string StudentTwoEmail => MeetReportsTestData.Email("student.two");

    public static string StudentThreeEmail => MeetReportsTestData.Email("student.three");

    public static string ElsewhereEmail => MeetReportsTestData.Email("person.elsewhere");

    /// <summary>A domain account with no membership: a class account, an "other participant".</summary>
    public static string ClassAccountEmail => MeetReportsTestData.Email("class.five");

    /// <summary>Every name, email and code the world holds — none may reach a log line (SC-10).</summary>
    public static IReadOnlyList<string> SensitiveValues { get; } =
    [
        CourseName, OtherCourseName, TeacherName, OtherTeacherName, StudentOneName, StudentTwoName, StudentThreeName,
        ElsewhereName, "teacher.one@", "teacher.two@", "student.one@", "student.two@", "student.three@",
        "person.elsewhere@", "class.five@", Codes.Linked, Codes.Confirmed, Codes.Marked, Codes.Unlinked, Codes.Elsewhere,
    ];

    public static class Codes
    {
        /// <summary>Linked to the course automatically, not confirmed.</summary>
        public const string Linked = "aaa-0001-xyz";

        /// <summary>Linked to the course and confirmed.</summary>
        public const string Confirmed = "bbb-0002-xyz";

        /// <summary>Marked "not a course" (course id null).</summary>
        public const string Marked = "ccc-0003-xyz";

        /// <summary>No link row.</summary>
        public const string Unlinked = "ddd-0004-xyz";

        /// <summary>Linked to the other course.</summary>
        public const string Elsewhere = "eee-0005-xyz";
    }

    /// <summary>One connection to insert: an email (null for a guest) and its stored duration.</summary>
    public sealed record Connection(string? Email, int Seconds);

    /// <summary>The ids the world holds; <c>MeetingA</c> and <c>MeetingB</c> are the meetings of the course in October.</summary>
    public sealed record SeededWorld(
        long CourseId,
        long OtherCourseId,
        long MeetingA,
        long MeetingB,
        long MeetingMarked,
        long MeetingUnlinked,
        long MeetingOutside,
        long MeetingElsewhere);

    /// <summary>The names the language-aware host starts with.</summary>
    public static async Task<(InstallationTestHost Host, FormClient Client)> StartAsync(
        PostgreSqlFixture database,
        Actor actor,
        string language,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, cancellationToken, cause);
        if (language != UiLanguageTestData.Ukrainian)
        {
            var choice = await client.ChooseLanguageAsync(
                SignInTestData.LandingPath, language, cancellationToken);
            Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        }

        return (host, client);
    }

    /// <summary>
    /// A course, its roster and the other course: teacher One and students One, Two, Three on the course (first seen
    /// 2026-09-01), teacher Two and the person elsewhere on the other course; then the meetings below.
    /// </summary>
    /// <remarks>
    /// A1 (code linked, 2026-10-05 10:00 Kyiv, 45 min, organizer teacher One): organizer 2700 s, student One on two
    /// devices 2400 s each, student Two 1200 s, the person elsewhere 600 s, a class account 300 s, a guest 300 s.
    /// B1 (code confirmed, 2026-10-20 09:00, 30 min, organizer teacher Two): student One 1800 s.
    /// Marked, unlinked, outside-the-period and other-course meetings carry student Three or student One and must never
    /// show on the course's pages.
    /// </remarks>
    public static async Task<SeededWorld> SeedWorldAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        // The retention purge runs at every host start (US-037) and deletes a participant that has no membership yet.
        await host.WaitForPurgeRunsAsync(1, cancellationToken);

        var course = await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: CourseTestData.CourseId(1), name: CourseName);
        var other = await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: CourseTestData.CourseId(2), name: OtherCourseName);
        await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: CourseTestData.CourseId(3), name: ArchivedCourseName,
            state: CourseTestData.States.Archived);

        await AddMemberAsync(host, course, 1, TeacherEmail, TeacherName, CourseTestData.Roles.Teacher, cancellationToken);
        await AddMemberAsync(host, course, 2, StudentOneEmail, StudentOneName, CourseTestData.Roles.Student, cancellationToken);
        await AddMemberAsync(host, course, 3, StudentTwoEmail, StudentTwoName, CourseTestData.Roles.Student, cancellationToken);
        await AddMemberAsync(host, course, 4, StudentThreeEmail, StudentThreeName, CourseTestData.Roles.Student, cancellationToken);
        await AddMemberAsync(host, other, 5, OtherTeacherEmail, OtherTeacherName, CourseTestData.Roles.Teacher, cancellationToken);
        await AddMemberAsync(host, other, 6, ElsewhereEmail, ElsewhereName, CourseTestData.Roles.Student, cancellationToken);

        var linkedAt = new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);
        await MeetLinkRows.InsertLinkAsync(host, MeetLinkRows.LinkRow.Automatic(Codes.Linked, course, linkedAt), cancellationToken);
        await MeetLinkRows.InsertLinkAsync(
            host,
            MeetLinkRows.LinkRow.Automatic(Codes.Confirmed, course, linkedAt) with
            {
                ConfirmedBy = MeetLinkRows.Person,
                ConfirmedAt = linkedAt.AddHours(1),
            },
            cancellationToken);
        await MeetLinkRows.InsertMarkAsync(host, Codes.Marked, linkedAt, cancellationToken);
        await MeetLinkRows.InsertLinkAsync(host, MeetLinkRows.LinkRow.Automatic(Codes.Elsewhere, other, linkedAt), cancellationToken);

        var a1 = await InsertMeetingAsync(
            host, Codes.Linked, TeacherEmail, MeetReportsTestData.Period.Local(10, 5, 10), TimeSpan.FromMinutes(45), cancellationToken,
            new Connection(TeacherEmail, 2700),
            new Connection(StudentOneEmail, 2400),
            new Connection(StudentOneEmail, 2400),
            new Connection(StudentTwoEmail, 1200),
            new Connection(ElsewhereEmail, 600),
            new Connection(ClassAccountEmail, 300),
            new Connection(null, 300));
        var b1 = await InsertMeetingAsync(
            host, Codes.Confirmed, OtherTeacherEmail, MeetReportsTestData.Period.Local(10, 20, 9), TimeSpan.FromMinutes(30), cancellationToken,
            new Connection(StudentOneEmail, 1800));
        var marked = await InsertMeetingAsync(
            host, Codes.Marked, TeacherEmail, MeetReportsTestData.Period.Local(10, 6, 10), TimeSpan.FromMinutes(60), cancellationToken,
            new Connection(StudentThreeEmail, 600));
        var unlinked = await InsertMeetingAsync(
            host, Codes.Unlinked, TeacherEmail, MeetReportsTestData.Period.Local(10, 7, 10), TimeSpan.FromMinutes(60), cancellationToken,
            new Connection(StudentThreeEmail, 600));
        var outside = await InsertMeetingAsync(
            host, Codes.Linked, TeacherEmail, MeetReportsTestData.Period.Local(11, 1, 0, 10), TimeSpan.FromMinutes(30), cancellationToken,
            new Connection(StudentOneEmail, 600));
        var elsewhere = await InsertMeetingAsync(
            host, Codes.Elsewhere, OtherTeacherEmail, MeetReportsTestData.Period.Local(10, 8, 10), TimeSpan.FromMinutes(40), cancellationToken,
            new Connection(StudentOneEmail, 600));

        return new SeededWorld(course, other, a1, b1, marked, unlinked, outside, elsewhere);
    }

    /// <summary>A participant (ordinal-numbered Google id) and its membership, first seen on 2026-09-01.</summary>
    public static async Task<long> AddMemberAsync(
        InstallationTestHost host,
        long courseId,
        int ordinal,
        string? email,
        string? fullName,
        string role,
        CancellationToken cancellationToken,
        bool onRoster = true,
        DateTimeOffset? firstSeenAt = null,
        DateTimeOffset? lastSeenAt = null)
    {
        var participant = await CourseRows.InsertParticipantAsync(
            host, cancellationToken, googleUserId: CourseTestData.UserId(ordinal), email: email, fullName: fullName);
        await CourseRows.InsertMembershipAsync(
            host,
            courseId,
            participant,
            cancellationToken,
            role: role,
            firstSeenAt: firstSeenAt ?? new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            lastSeenAt: lastSeenAt,
            onRoster: onRoster);
        return participant;
    }

    /// <summary>A stored meeting with one connection per <paramref name="connections"/> item (stored durations as given).</summary>
    public static async Task<long> InsertMeetingAsync(
        InstallationTestHost host,
        string code,
        string organizer,
        DateTimeOffset startedAt,
        TimeSpan duration,
        CancellationToken cancellationToken,
        params Connection[] connections)
    {
        var session = await host.ScalarAsync<long>(
            """
            INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
            VALUES (@conferenceId, @code, @organizer, @startedAt, @endedAt, @startedAt, @startedAt)
            RETURNING id
            """,
            cancellationToken,
            ("conferenceId", "conf-" + Guid.NewGuid().ToString("N")),
            ("code", code),
            ("organizer", organizer),
            ("startedAt", startedAt),
            ("endedAt", startedAt + duration));
        for (var i = 0; i < connections.Length; i++)
        {
            await host.ExecuteAsync(
                """
                INSERT INTO meet_participation (meet_session_id, endpoint_id, email, joined_at, duration_seconds, created_at, updated_at)
                VALUES (@session, @endpoint, @email, @at, @seconds, @at, @at)
                """,
                cancellationToken,
                ("session", session),
                ("endpoint", MeetTestData.EndpointId(i + 1)),
                ("email", connections[i].Email),
                ("at", startedAt),
                ("seconds", connections[i].Seconds));
        }

        return session;
    }

    /// <summary>A <c>completed</c> sync_state row carrying the Meet watermark (null = never loaded).</summary>
    public static Task SeedWatermarkAsync(
        this InstallationTestHost host, DateTimeOffset? meetLoadedUpTo, CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            """
            INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count,
                                    last_error, last_successful_run_at, meet_loaded_up_to, failed_step,
                                    created_at, updated_at)
            VALUES (true, 'completed', @runId, @startedAt, @finishedAt, 0, NULL, @finishedAt,
                    @meetLoadedUpTo, NULL, @stamp, @stamp)
            """,
            cancellationToken,
            ("runId", Guid.NewGuid()),
            ("startedAt", LastSynchronizationTestData.Started),
            ("finishedAt", LastSynchronizationTestData.Finished),
            ("meetLoadedUpTo", meetLoadedUpTo),
            ("stamp", host.Time.GetUtcNow()));

    /// <summary>The row counts and the link fingerprint that a read-only page must leave as they were (FR-015).</summary>
    public static async Task<string> WriteFingerprintAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        var audit = await host.ScalarAsync<long>("SELECT count(*) FROM audit_event", cancellationToken);
        var sync = await host.ScalarAsync<long>("SELECT count(*) FROM sync_state", cancellationToken);
        var links = await host.McLinksFingerprintAsync(cancellationToken);
        var sessions = await host.ScalarAsync<long>("SELECT count(*) FROM meet_session", cancellationToken);
        var participations = await host.ScalarAsync<long>("SELECT count(*) FROM meet_participation", cancellationToken);
        return $"audit={audit};sync={sync};sessions={sessions};participations={participations};links={links}";
    }

    /// <summary>The number of calls the two substituted Google readers have recorded (TC-4, AC-017).</summary>
    public static int GoogleCallCount(this InstallationTestHost host) =>
        host.Classroom.ImpersonatedAs.Count + host.Meet.Calls.Count;

    /// <summary>Runs <paramref name="body"/> with the real <see cref="MeetReportSource"/> over the host's database.</summary>
    public static async Task WithMeetReportSourceAsync(
        this InstallationTestHost host, Func<IMeetReportSource, Task> body)
    {
        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();
        await body(new MeetReportSource(db));
    }
}
