namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Direct inserts into the three tables US-014 adds, each with a default that satisfies every constraint and is
/// overridden per test — the shape <see cref="InstallationTestHost.InsertWorkspaceConnectionAsync"/> established.
/// </summary>
/// <remarks>
/// These bypass the entities on purpose: db-design §9 asks whether the <b>database</b> enforces its constraints,
/// which a test going through the domain could not tell apart from an entity guard (TC-2).
/// </remarks>
public static class CourseRows
{
    /// <summary>Inserts a course and returns its generated <c>id</c>.</summary>
    public static async Task<long> InsertCourseAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        string? googleId = null,
        string? name = null,
        string state = CourseTestData.States.Active,
        string? ownerGoogleId = null,
        DateTimeOffset? creationTime = null,
        DateTimeOffset? updateTime = null,
        string? section = null,
        string? description = null)
    {
        var stamp = host.Time.GetUtcNow();
        var id = await host.ScalarAsync<long>(
            """
            INSERT INTO course (google_id, name, section, description_heading, description, room,
                                owner_google_id, creation_time, update_time, course_state, alternate_link,
                                teacher_folder_id, teacher_folder_title, calendar_id, created_at, updated_at)
            VALUES (@googleId, @name, @section, NULL, @description, NULL,
                    @ownerGoogleId, @creationTime, @updateTime, @state, NULL,
                    NULL, NULL, NULL, @stamp, @stamp)
            RETURNING id
            """,
            cancellationToken,
            ("googleId", googleId ?? CourseTestData.CourseId(1)),
            ("name", name ?? CourseTestData.CourseName(1)),
            ("section", section),
            ("description", description),
            ("ownerGoogleId", ownerGoogleId),
            ("creationTime", creationTime),
            ("updateTime", updateTime),
            ("state", state),
            ("stamp", stamp));
        return id;
    }

    /// <summary>Inserts a participant and returns its generated <c>id</c>.</summary>
    public static async Task<long> InsertParticipantAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        string? googleUserId = null,
        string? email = null,
        string? fullName = null)
    {
        var stamp = host.Time.GetUtcNow();
        var id = await host.ScalarAsync<long>(
            """
            INSERT INTO classroom_participant (google_user_id, email, full_name, created_at, updated_at)
            VALUES (@googleUserId, @email, @fullName, @stamp, @stamp)
            RETURNING id
            """,
            cancellationToken,
            ("googleUserId", googleUserId ?? CourseTestData.UserId(1)),
            ("email", email),
            ("fullName", fullName),
            ("stamp", stamp));
        return id;
    }

    /// <summary>Inserts a membership. The default is a person seen in this very run and still on the roster.</summary>
    public static Task<int> InsertMembershipAsync(
        InstallationTestHost host,
        long courseId,
        long participantId,
        CancellationToken cancellationToken,
        string role = CourseTestData.Roles.Student,
        DateTimeOffset? firstSeenAt = null,
        DateTimeOffset? lastSeenAt = null,
        bool onRoster = true)
    {
        var stamp = host.Time.GetUtcNow();
        return host.ExecuteAsync(
            """
            INSERT INTO course_membership (course_id, participant_id, role, first_seen_at, last_seen_at,
                                           on_roster, created_at, updated_at)
            VALUES (@courseId, @participantId, @role, @firstSeenAt, @lastSeenAt, @onRoster, @stamp, @stamp)
            """,
            cancellationToken,
            ("courseId", courseId),
            ("participantId", participantId),
            ("role", role),
            ("firstSeenAt", firstSeenAt ?? stamp),
            ("lastSeenAt", lastSeenAt ?? stamp),
            ("onRoster", onRoster),
            ("stamp", stamp));
    }
}
