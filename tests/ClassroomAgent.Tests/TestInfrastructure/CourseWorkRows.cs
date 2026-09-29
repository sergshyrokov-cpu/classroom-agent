namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Direct inserts into the two tables US-015 adds, each with a default that satisfies every constraint and is
/// overridden per test — the shape <see cref="CourseRows"/> established.
/// </summary>
/// <remarks>
/// These bypass the entities on purpose: db-design §10 asks whether the <b>database</b> enforces its constraints,
/// which a test going through the domain could not tell apart from an entity guard (TC-2).
/// </remarks>
public static class CourseWorkRows
{
    /// <summary>Inserts a <c>course_work</c> row and returns its generated <c>id</c>.</summary>
    public static async Task<long> InsertCourseWorkAsync(
        InstallationTestHost host,
        long courseId,
        CancellationToken cancellationToken,
        string? googleId = null,
        string resource = CourseWorkTestData.ResourceCodes.CourseWork,
        string? title = null,
        DateTimeOffset? itemDate = null,
        DateTimeOffset? dueAt = null,
        decimal? maxPoints = null,
        DateTimeOffset? creationTime = null,
        DateTimeOffset? updateTime = null)
    {
        var stamp = host.Time.GetUtcNow();
        var id = await host.ScalarAsync<long>(
            """
            INSERT INTO course_work (course_id, google_id, resource, title, item_date, due_at, max_points,
                                     creation_time, update_time, created_at, updated_at)
            VALUES (@courseId, @googleId, @resource, @title, @itemDate, @dueAt, @maxPoints,
                    @creationTime, @updateTime, @stamp, @stamp)
            RETURNING id
            """,
            cancellationToken,
            ("courseId", courseId),
            ("googleId", googleId ?? CourseWorkTestData.ItemId(1)),
            ("resource", resource),
            ("title", title ?? CourseWorkTestData.Title(1)),
            ("itemDate", itemDate ?? stamp),
            ("dueAt", dueAt),
            ("maxPoints", maxPoints),
            ("creationTime", creationTime),
            ("updateTime", updateTime),
            ("stamp", stamp));
        return id;
    }

    /// <summary>
    /// Inserts a <c>submission</c> row and returns its generated <c>id</c>. The default state is recognised and
    /// carries no raw string, so the biconditional of <c>ck_submission_raw_state</c> holds without a test having
    /// to think about it unless the scenario is about that very constraint.
    /// </summary>
    public static async Task<long> InsertSubmissionAsync(
        InstallationTestHost host,
        long courseWorkId,
        long participantId,
        CancellationToken cancellationToken,
        string? googleId = null,
        string state = CourseWorkTestData.StateCodes.TurnedIn,
        string? rawState = null,
        decimal? assignedGrade = null,
        decimal? draftGrade = null,
        DateTimeOffset? turnedInAt = null,
        bool late = false,
        DateTimeOffset? updateTime = null)
    {
        var stamp = host.Time.GetUtcNow();
        var id = await host.ScalarAsync<long>(
            """
            INSERT INTO submission (course_work_id, participant_id, google_id, state, raw_state, assigned_grade,
                                    draft_grade, turned_in_at, late, update_time, created_at, updated_at)
            VALUES (@courseWorkId, @participantId, @googleId, @state, @rawState, @assignedGrade,
                    @draftGrade, @turnedInAt, @late, @updateTime, @stamp, @stamp)
            RETURNING id
            """,
            cancellationToken,
            ("courseWorkId", courseWorkId),
            ("participantId", participantId),
            ("googleId", googleId ?? CourseWorkTestData.SubmissionId(1)),
            ("state", state),
            ("rawState", rawState),
            ("assignedGrade", assignedGrade),
            ("draftGrade", draftGrade),
            ("turnedInAt", turnedInAt),
            ("late", late),
            ("updateTime", updateTime),
            ("stamp", stamp));
        return id;
    }
}
