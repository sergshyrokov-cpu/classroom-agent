using ClassroomAgent.Application.Models;
using static ClassroomAgent.Tests.TestInfrastructure.MeetTestData;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The synthetic world of US-032's linking-step tests (TC-4): two courses taught by one teacher, rosters seeded
/// directly in SQL with a <c>first_seen_at</c> before the meetings (a synchronization would stamp them with the run's
/// time, FR-002, I-1), and Meet events of one meeting code. The run's Classroom step reads an empty port, so it leaves
/// the seeded courses and memberships exactly as they are. Nothing here comes from a real domain.
/// </summary>
public static class MeetLinkingTestData
{
    /// <summary>The meeting code of every scenario.</summary>
    public static readonly string Code = MeetingCode(1);

    /// <summary>The organizer (a teacher of both courses) of every scenario.</summary>
    public static readonly string Organizer = Teacher(1);

    /// <summary>The date of the first meeting: two days before the host's start.</summary>
    public static readonly DateTimeOffset FirstMeeting = InstallationTestHost.DefaultStart - TimeSpan.FromDays(2);

    /// <summary>A later meeting of the same code, one day before the host's start.</summary>
    public static readonly DateTimeOffset LaterMeeting = InstallationTestHost.DefaultStart - TimeSpan.FromDays(1);

    /// <summary>Rosters observed long before the meetings.</summary>
    public static readonly DateTimeOffset LongAgo = InstallationTestHost.DefaultStart - TimeSpan.FromDays(30);

    /// <summary>The log event of the linking step (spec FR-006, §9).</summary>
    public const string LinkingStepCompleted = "SyncLinkingStepCompleted";

    public const string CodesScored = "CodesScored";

    public const string LinksCreated = "LinksCreated";

    public const string AutoLinkedAction = "meet_code_auto_linked";

    /// <summary>The two seeded courses, by their internal ids.</summary>
    public sealed record Courses(long A, long B);

    /// <summary>The emails of students <paramref name="from"/> … <paramref name="to"/>, inclusive.</summary>
    public static List<string> Students(int from, int to) =>
        Enumerable.Range(from, to - from + 1).Select(Student).ToList();

    /// <summary>Seeds courses A and B, with <see cref="Organizer"/> as a teacher of both from <paramref name="teacherSince"/>.</summary>
    public static async Task<Courses> SeedCoursesAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        DateTimeOffset? teacherSince = null)
    {
        var updated = InstallationTestHost.DefaultStart - TimeSpan.FromDays(10);
        var a = await CourseRows.InsertCourseAsync(
            host,
            cancellationToken,
            googleId: CourseTestData.CourseId(1),
            name: CourseTestData.CourseName(1),
            creationTime: updated,
            updateTime: updated);
        var b = await CourseRows.InsertCourseAsync(
            host,
            cancellationToken,
            googleId: CourseTestData.CourseId(2),
            name: CourseTestData.CourseName(2),
            creationTime: updated,
            updateTime: updated);
        await SeedRosterAsync(host, a, CourseTestData.Roles.Teacher, [Organizer], cancellationToken, teacherSince);
        await SeedRosterAsync(host, b, CourseTestData.Roles.Teacher, [Organizer], cancellationToken, teacherSince);
        return new Courses(a, b);
    }

    /// <summary>Seeds memberships of the given role for the given emails, on the roster, first seen at <paramref name="firstSeenAt"/>.</summary>
    public static async Task SeedRosterAsync(
        InstallationTestHost host,
        long courseId,
        string role,
        IEnumerable<string> emails,
        CancellationToken cancellationToken,
        DateTimeOffset? firstSeenAt = null)
    {
        foreach (var email in emails)
        {
            var participantId = await EnsureParticipantAsync(host, email, cancellationToken);
            await CourseRows.InsertMembershipAsync(
                host,
                courseId,
                participantId,
                cancellationToken,
                role,
                firstSeenAt ?? LongAgo,
                InstallationTestHost.DefaultStart,
                onRoster: true);
        }
    }

    /// <summary>The participant with that email, created when missing — one person can be in both courses.</summary>
    public static async Task<long> EnsureParticipantAsync(InstallationTestHost host, string email, CancellationToken cancellationToken)
    {
        var existing = await host.ScalarAsync<long?>(
            "SELECT id FROM classroom_participant WHERE email = @email",
            cancellationToken,
            ("email", email));
        if (existing is { } id)
        {
            return id;
        }

        var ordinal = await host.ScalarAsync<long>("SELECT count(*) + 1 FROM classroom_participant", cancellationToken);
        return await CourseRows.InsertParticipantAsync(
            host,
            cancellationToken,
            googleUserId: CourseTestData.UserId((int)ordinal),
            email: email,
            fullName: CourseTestData.Name((int)ordinal));
    }

    /// <summary>
    /// The 85 % / 20 % world of AC-001: 17 students of A (1 … 17), 4 of B (15 … 18); 20 students (1 … 20) will join.
    /// </summary>
    public static async Task<Courses> SeedClearRostersAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        DateTimeOffset? firstSeenAt = null)
    {
        var courses = await SeedCoursesAsync(host, cancellationToken, firstSeenAt);
        await SeedRosterAsync(host, courses.A, CourseTestData.Roles.Student, Students(1, 17), cancellationToken, firstSeenAt);
        await SeedRosterAsync(host, courses.B, CourseTestData.Roles.Student, Students(15, 18), cancellationToken, firstSeenAt);
        return courses;
    }

    /// <summary>
    /// The 70 % / 55 % world of AC-003, AC-004, AC-006: 14 students of A (1 … 14), 11 of B (10 … 20); 20 students
    /// (1 … 20) will join.
    /// </summary>
    public static async Task<Courses> SeedCloseRostersAsync(InstallationTestHost host, CancellationToken cancellationToken)
    {
        var courses = await SeedCoursesAsync(host, cancellationToken);
        await SeedRosterAsync(host, courses.A, CourseTestData.Roles.Student, Students(1, 14), cancellationToken);
        await SeedRosterAsync(host, courses.B, CourseTestData.Roles.Student, Students(10, 20), cancellationToken);
        return courses;
    }

    /// <summary>One meeting of <see cref="Code"/>: the organizer connects first, then every participant once.</summary>
    public static MeetCallEndedEvent[] Meeting(int conference, IEnumerable<string> participants, DateTimeOffset leftAt)
    {
        var events = new List<MeetCallEndedEvent> { Event(conference, 1, leftAt, 600, Organizer, Organizer, meetingCode: Code) };
        var endpoint = 2;
        foreach (var participant in participants)
        {
            events.Add(Event(conference, endpoint++, leftAt, 600, Organizer, participant, meetingCode: Code));
        }

        return [.. events];
    }

    /// <summary>The first meeting: students 1 … 20 attend.</summary>
    public static MeetCallEndedEvent[] FirstMeetingEvents() => Meeting(1, Students(1, 20), FirstMeeting);

    /// <summary>Stores a meeting and its connections directly, as a previous run would have (AC-007).</summary>
    public static async Task SeedStoredMeetingAsync(
        InstallationTestHost host,
        int conference,
        IEnumerable<string> participants,
        DateTimeOffset leftAt,
        CancellationToken cancellationToken)
    {
        var stamp = host.Time.GetUtcNow();
        var started = leftAt - TimeSpan.FromSeconds(600);
        var sessionId = await host.ScalarAsync<long>(
            """
            INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
            VALUES (@conference, @code, @organizer, @started, @ended, @stamp, @stamp)
            RETURNING id
            """,
            cancellationToken,
            ("conference", ConferenceId(conference)),
            ("code", Code),
            ("organizer", Organizer),
            ("started", started),
            ("ended", leftAt),
            ("stamp", stamp));
        var endpoint = 1;
        foreach (var email in participants.Prepend(Organizer))
        {
            await host.ExecuteAsync(
                """
                INSERT INTO meet_participation (meet_session_id, endpoint_id, email, joined_at, duration_seconds, created_at, updated_at)
                VALUES (@session, @endpointId, @email, @started, 600, @stamp, @stamp)
                """,
                cancellationToken,
                ("session", sessionId),
                ("endpointId", EndpointId(endpoint++)),
                ("email", email),
                ("started", started),
                ("stamp", stamp));
        }
    }

    /// <summary>A "not a course" mark made by a person (db-design §2.2: no course, marker and time set).</summary>
    public static Task<int> InsertMarkAsync(InstallationTestHost host, DateTimeOffset markedAt, CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            """
            INSERT INTO meeting_code_link (meeting_code, course_id, linked_automatically, linked_by_app_user_id, linked_at,
                                           confirmed_by_app_user_id, confirmed_at, marked_by_app_user_id, marked_at,
                                           concurrency_stamp, created_at, updated_at)
            VALUES (@code, NULL, NULL, NULL, NULL, NULL, NULL, 1, @at, 'x', @at, @at)
            """,
            cancellationToken,
            ("code", Code),
            ("at", markedAt));

    /// <summary>A link made by a person (not automatic, unconfirmed).</summary>
    public static Task<int> InsertPersonLinkAsync(
        InstallationTestHost host,
        long courseId,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            """
            INSERT INTO meeting_code_link (meeting_code, course_id, linked_automatically, linked_by_app_user_id, linked_at,
                                           confirmed_by_app_user_id, confirmed_at, marked_by_app_user_id, marked_at,
                                           concurrency_stamp, created_at, updated_at)
            VALUES (@code, @course, false, 1, @at, NULL, NULL, NULL, NULL, 'x', @at, @at)
            """,
            cancellationToken,
            ("code", Code),
            ("course", courseId),
            ("at", linkedAt));

    /// <summary>Every link row as JSON — two snapshots equal means the row (stamp and times included) did not change.</summary>
    public static Task<IReadOnlyList<string>> LinkSnapshotAsync(InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync("SELECT row_to_json(l)::text FROM meeting_code_link l ORDER BY id", r => r.GetString(0), cancellationToken);

    /// <summary>The link rows, column by column (db-design §2.1).</summary>
    public static Task<IReadOnlyList<LinkRow>> LinksAsync(InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync(
            """
            SELECT meeting_code, course_id, linked_automatically, linked_by_app_user_id, linked_at,
                   confirmed_by_app_user_id, confirmed_at, marked_by_app_user_id, marked_at
            FROM meeting_code_link ORDER BY id
            """,
            r => new LinkRow(
                r.GetString(0),
                r.IsDBNull(1) ? null : r.GetInt64(1),
                r.IsDBNull(2) ? null : r.GetBoolean(2),
                r.IsDBNull(3) ? null : r.GetInt64(3),
                r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4),
                r.IsDBNull(5) ? null : r.GetInt64(5),
                r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6),
                r.IsDBNull(7) ? null : r.GetInt64(7),
                r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8)),
            cancellationToken);

    /// <summary>The automatic-link audit rows (db-design §3).</summary>
    public static Task<IReadOnlyList<AutoLinkAuditRow>> AutoLinkAuditRowsAsync(InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync(
            $"""
            SELECT actor_type, target_type, target_id, outcome, request_id, meet_code, meet_previous_course_id
            FROM audit_event WHERE action = '{AutoLinkedAction}' ORDER BY id
            """,
            r => new AutoLinkAuditRow(
                r.GetString(0),
                r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetInt64(2),
                r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetInt64(6)),
            cancellationToken);

    /// <summary>Makes every insert into <c>meeting_code_link</c> fail inside PostgreSQL — a real database failure (spec FR-006).</summary>
    public static Task FailLinkInsertsAsync(InstallationTestHost host, CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            """
            CREATE OR REPLACE FUNCTION linking_test_fail_insert() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN RAISE EXCEPTION 'injected linking test failure'; END $$;
            CREATE TRIGGER linking_test_fail_insert BEFORE INSERT ON meeting_code_link
            FOR EACH ROW EXECUTE FUNCTION linking_test_fail_insert();
            """,
            cancellationToken);

    /// <summary>
    /// Lets the next scheduled run start (interval 1 minute) and waits for a finished run other than <paramref name="first"/>,
    /// as <c>MeetPullHostTests</c> does.
    /// </summary>
    public static async Task WaitForSecondRunAsync(
        this InstallationTestHost host,
        SyncHostExtensions.SyncStateRow first,
        CancellationToken cancellationToken)
    {
        await host.Time.AdvanceWhenDueAsync(TimeSpan.FromMinutes(1), cancellationToken);
        await host.Time.WaitUntilAsync(
            () => host.SyncStatesAsync(cancellationToken).GetAwaiter().GetResult() is [{ } row]
                  && row.RunId != first.RunId
                  && row.Status != SyncTestData.Status.Running,
            "a second finished synchronization run",
            cancellationToken);
    }

    /// <summary>A <c>meeting_code_link</c> row.</summary>
    public sealed record LinkRow(
        string MeetingCode,
        long? CourseId,
        bool? LinkedAutomatically,
        long? LinkedBy,
        DateTimeOffset? LinkedAt,
        long? ConfirmedBy,
        DateTimeOffset? ConfirmedAt,
        long? MarkedBy,
        DateTimeOffset? MarkedAt);

    /// <summary>An automatic-link audit row.</summary>
    public sealed record AutoLinkAuditRow(
        string ActorType,
        string? TargetType,
        long? TargetId,
        string Outcome,
        string? RequestId,
        string? MeetCode,
        long? PreviousCourseId);
}
