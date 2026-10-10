namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Direct SQL inserts for US-032's tests: <c>meeting_code_link</c> rows (any shape, so a test can offer the database a
/// row the domain would never build), Meet sessions with their participations, and courses dated for the retention
/// purge. They bypass the entities on purpose: db-design §10 asks whether the <b>database</b> enforces its rules (TC-2).
/// </summary>
public static class MeetLinkRows
{
    /// <summary>A bare <c>app_user</c> id: the link columns hold ids without a foreign key (PC-11).</summary>
    public const long Person = 7;

    private const string InsertLink =
        """
        INSERT INTO meeting_code_link (meeting_code, course_id, linked_automatically, linked_by_app_user_id, linked_at,
                                       confirmed_by_app_user_id, confirmed_at, marked_by_app_user_id, marked_at,
                                       concurrency_stamp, created_at, updated_at)
        VALUES (@code, @courseId, @auto, @linkedBy, @linkedAt, @confirmedBy, @confirmedAt, @markedBy, @markedAt,
                @stamp, @createdAt, @createdAt)
        RETURNING id
        """;

    /// <summary>Inserts the row exactly as given — the caller decides whether it is a legal shape.</summary>
    public static Task<long> InsertLinkAsync(InstallationTestHost host, LinkRow row, CancellationToken cancellationToken) =>
        host.ScalarAsync<long>(
            InsertLink,
            cancellationToken,
            ("code", row.Code),
            ("courseId", row.CourseId),
            ("auto", row.LinkedAutomatically),
            ("linkedBy", row.LinkedBy),
            ("linkedAt", row.LinkedAt),
            ("confirmedBy", row.ConfirmedBy),
            ("confirmedAt", row.ConfirmedAt),
            ("markedBy", row.MarkedBy),
            ("markedAt", row.MarkedAt),
            ("stamp", row.Stamp),
            ("createdAt", host.Time.GetUtcNow()));

    /// <summary>A course link made by a person (legal shape).</summary>
    public static Task<long> InsertCourseLinkAsync(
        InstallationTestHost host,
        string code,
        long courseId,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        InsertLinkAsync(host, LinkRow.ByPerson(code, courseId, at), cancellationToken);

    /// <summary>A "not a course" mark (legal shape).</summary>
    public static Task<long> InsertMarkAsync(
        InstallationTestHost host,
        string code,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        InsertLinkAsync(host, LinkRow.Marked(code, at), cancellationToken);

    /// <summary>A Meet session of <paramref name="code"/> starting at <paramref name="startedAt"/>; returns its id.</summary>
    public static Task<long> InsertSessionAsync(
        InstallationTestHost host,
        string conferenceId,
        string code,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken) =>
        host.ScalarAsync<long>(
            """
            INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
            VALUES (@conferenceId, @code, @organizer, @startedAt, @endedAt, @startedAt, @startedAt)
            RETURNING id
            """,
            cancellationToken,
            ("conferenceId", conferenceId),
            ("code", code),
            ("organizer", MeetTestData.Teacher(1)),
            ("startedAt", startedAt),
            ("endedAt", startedAt + TimeSpan.FromHours(1)));

    /// <summary>One participation per email (a null email is an "other participant") in <paramref name="sessionId"/>.</summary>
    public static async Task InsertParticipationsAsync(
        InstallationTestHost host,
        long sessionId,
        DateTimeOffset joinedAt,
        CancellationToken cancellationToken,
        params string?[] emails)
    {
        for (var i = 0; i < emails.Length; i++)
        {
            await host.ExecuteAsync(
                """
                INSERT INTO meet_participation (meet_session_id, endpoint_id, email, joined_at, duration_seconds, created_at, updated_at)
                VALUES (@sessionId, @endpointId, @email, @joinedAt, 600, @joinedAt, @joinedAt)
                """,
                cancellationToken,
                ("sessionId", sessionId),
                ("endpointId", MeetTestData.EndpointId(i + 1)),
                ("email", emails[i]),
                ("joinedAt", joinedAt));
        }
    }

    /// <summary>A session and its participations in one call; returns the session id.</summary>
    public static async Task<long> InsertMeetingAsync(
        InstallationTestHost host,
        string conferenceId,
        string code,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken,
        params string?[] emails)
    {
        var session = await InsertSessionAsync(host, conferenceId, code, startedAt, cancellationToken);
        await InsertParticipationsAsync(host, session, startedAt, cancellationToken, emails);
        return session;
    }

    /// <summary>
    /// A course whose own activity is <paramref name="update"/> (Google date), with <c>created_at</c> pushed to
    /// <paramref name="createdAt"/> so that a course seeded with a Google date is judged by that date alone.
    /// </summary>
    public static async Task<long> InsertDatedCourseAsync(
        InstallationTestHost host,
        int ordinal,
        DateTimeOffset update,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var id = await CourseRows.InsertCourseAsync(
            host,
            cancellationToken,
            googleId: CourseTestData.CourseId(ordinal),
            name: CourseTestData.CourseName(ordinal),
            updateTime: update);
        await host.ExecuteAsync(
            "UPDATE course SET created_at = @at, updated_at = @at WHERE id = @id",
            cancellationToken,
            ("at", createdAt),
            ("id", id));
        return id;
    }

    /// <summary>A <c>meeting_code_link</c> row as the database receives it.</summary>
    public sealed record LinkRow(
        string Code,
        long? CourseId,
        bool? LinkedAutomatically,
        long? LinkedBy,
        DateTimeOffset? LinkedAt,
        long? ConfirmedBy,
        DateTimeOffset? ConfirmedAt,
        long? MarkedBy,
        DateTimeOffset? MarkedAt,
        string Stamp)
    {
        /// <summary>db-design §2.2 shape 1: made by the system.</summary>
        public static LinkRow Automatic(string code, long courseId, DateTimeOffset at) =>
            new(code, courseId, true, null, at, null, null, null, null, "stamp-1");

        /// <summary>db-design §2.2 shape 1: made by a person.</summary>
        public static LinkRow ByPerson(string code, long courseId, DateTimeOffset at) =>
            new(code, courseId, false, Person, at, null, null, null, null, "stamp-1");

        /// <summary>db-design §2.2 shape 2: the "not a course" mark.</summary>
        public static LinkRow Marked(string code, DateTimeOffset at) =>
            new(code, null, null, null, null, null, null, Person, at, "stamp-1");
    }
}
