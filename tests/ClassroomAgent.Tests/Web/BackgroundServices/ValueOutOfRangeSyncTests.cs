using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-057 AC-001 … AC-004, AC-009, AC-010 (spec FR-001 … FR-003, §7): a synchronization run through the real host, its
/// background service, PostgreSQL (TC-2) and the <b>real</b> Google Classroom adapter over a scripted transport
/// (<see cref="GoogleClassroomScript"/>, TC-4), so a point value beyond the <c>decimal</c> range arrives as the JSON
/// number Google would send. Every value and identity is synthetic.
/// </summary>
public sealed class ValueOutOfRangeSyncTests(PostgreSqlFixture database)
{
    private const string ValueOutOfRangeEvent = "SyncCourseValueOutOfRange";

    private const int ValueOutOfRangeEventId = 5135;

    /// <summary>The identities only the skipped course carries, so their absence from the log is meaningful.</summary>
    private const int Hidden = 52;

    /// <summary>Formats a log could print 1·10³⁰ in (AC-004).</summary>
    private static readonly string[] ValueTexts = ["1E+30", "1e+30", "1E30", "1000000000000000000000000000000"];

    private static string Email(int ordinal) => CourseTestData.Email($"person{ordinal}");

    /// <summary>
    /// Course <paramref name="n"/> with one student, one item and the student's submission; the identities are
    /// <paramref name="identity"/>'s, the points as given.
    /// </summary>
    private static GoogleClassroomScript WithCourse(
        GoogleClassroomScript script,
        int n,
        int identity,
        double? maxPoints = 10,
        double? assigned = 7,
        double? draft = null,
        string? name = null)
    {
        var course = CourseTestData.CourseId(n);
        return script
            .Course(course, name ?? CourseTestData.CourseName(n))
            .Student(course, CourseTestData.UserId(identity), CourseTestData.Name(identity), Email(identity))
            .Item(course, CourseWorkTestData.ItemId(identity), CourseWorkTestData.Title(identity), maxPoints)
            .Submission(course, CourseWorkTestData.ItemId(identity), CourseWorkTestData.SubmissionId(identity), CourseTestData.UserId(identity), assigned, draft);
    }

    /// <summary>AC-001's world: three courses, the second with an item whose <c>maxPoints</c> is 1·10³⁰.</summary>
    private static GoogleClassroomScript ThreeCoursesSecondOutOfRange()
    {
        var script = new GoogleClassroomScript();
        WithCourse(script, 1, 1);
        WithCourse(script, 2, Hidden, maxPoints: 1e30);
        WithCourse(script, 3, 3);
        return script;
    }

    private static Task<InstallationTestHost> StartAsync(
        PostgreSqlFixture database,
        GoogleClassroomScript script,
        CancellationToken ct,
        Func<InstallationTestHost, CancellationToken, Task>? seed = null) =>
        SyncHostExtensions.StartAsync(
            database,
            ct,
            seed: async (host, token) =>
            {
                if (seed is not null)
                {
                    await seed(host, token);
                }

                script.InstallIn(host);
            });

    private static Task<long> CountAsync(InstallationTestHost host, string table, string googleId, CancellationToken ct) =>
        host.CountAsync(table, ct, "google_id = @id", ("id", googleId));

    /// <summary>Everything stored of one course, as text: the course row and its items, memberships and submissions.</summary>
    private static Task<string?> FingerprintAsync(InstallationTestHost host, string courseGoogleId, CancellationToken ct) =>
        host.ScalarAsync<string>(
            """
            SELECT concat_ws(' | ',
                concat_ws(',', c.name, c.course_state, c.update_time, c.complete_read_update_time, c.updated_at),
                (SELECT string_agg(concat_ws(',', w.google_id, w.title, w.max_points, w.updated_at), ';' ORDER BY w.id)
                 FROM course_work w WHERE w.course_id = c.id),
                (SELECT string_agg(concat_ws(',', m.participant_id, m.role, m.on_roster, m.first_seen_at, m.last_seen_at, m.updated_at), ';' ORDER BY m.participant_id)
                 FROM course_membership m WHERE m.course_id = c.id),
                (SELECT string_agg(concat_ws(',', s.google_id, s.state, s.assigned_grade, s.draft_grade, s.updated_at), ';' ORDER BY s.id)
                 FROM submission s JOIN course_work w ON w.id = s.course_work_id WHERE w.course_id = c.id))
            FROM course c WHERE c.google_id = @id
            """,
            ct,
            ("id", courseGoogleId));

    /// <summary>
    /// AC-001, FR-001, FR-002: courses 1 and 3 and their items are stored, nothing of course 2 is written, the Meet step
    /// ran, the run is <c>completed</c> and counts the two stored courses only.
    /// </summary>
    [Fact]
    public async Task ACourseWithMaxPointsBeyondTheRange_IsSkipped_AndTheRunCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ThreeCoursesSecondOutOfRange(), ct);

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Null(run.LastError);
        Assert.Equal(2, run.ProcessedCount);
        foreach (var n in new[] { 1, 3 })
        {
            Assert.Equal(1, await CountAsync(host, "course", CourseTestData.CourseId(n), ct));
            Assert.Equal(1, await CountAsync(host, "course_work", CourseWorkTestData.ItemId(n), ct));
        }

        Assert.Equal(0, await CountAsync(host, "course", CourseTestData.CourseId(2), ct));
        Assert.Equal(0, await CountAsync(host, "course_work", CourseWorkTestData.ItemId(Hidden), ct));
        Assert.Equal(0, await CountAsync(host, "submission", CourseWorkTestData.SubmissionId(Hidden), ct));
        Assert.Equal(0, await host.CountAsync("classroom_participant", ct, "google_user_id = @id", ("id", CourseTestData.UserId(Hidden))));
        Assert.NotEmpty(host.Meet.Calls);
        Assert.NotNull(await host.ScalarAsync<object>("SELECT meet_loaded_up_to FROM sync_state", ct));
    }

    /// <summary>
    /// AC-002, FR-001, FR-002, spec I-1: a course with an assigned grade of 1·10³⁰ and one with a draft grade of −1·10³⁰
    /// are both skipped; the third course is stored and the run completes.
    /// </summary>
    [Fact]
    public async Task CoursesWithGradesBeyondTheRange_AreSkipped_InBothDirections()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = new GoogleClassroomScript();
        WithCourse(script, 1, 41, assigned: 1e30);
        WithCourse(script, 2, 42, assigned: null, draft: -1e30);
        WithCourse(script, 3, 3);
        await using var host = await StartAsync(database, script, ct);

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Null(run.LastError);
        Assert.Equal(1, run.ProcessedCount);
        Assert.Equal(0, await CountAsync(host, "course", CourseTestData.CourseId(1), ct));
        Assert.Equal(0, await CountAsync(host, "course", CourseTestData.CourseId(2), ct));
        Assert.Equal(1, await CountAsync(host, "course", CourseTestData.CourseId(3), ct));
        Assert.Equal(0, await CountAsync(host, "submission", CourseWorkTestData.SubmissionId(41), ct));
        Assert.Equal(0, await CountAsync(host, "submission", CourseWorkTestData.SubmissionId(42), ct));
    }

    /// <summary>
    /// AC-003, FR-002.1: course 2 stored by an earlier run (seeded rows), for which Google now returns a renamed course
    /// and an item with <c>maxPoints</c> 1·10³⁰ — after the run its course row, items, memberships and submissions are
    /// exactly as before, while course 1 of the same run is stored (the control that the run was effective).
    /// </summary>
    [Fact]
    public async Task AStoredCourse_WithMaxPointsBeyondTheRange_IsLeftExactlyAsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = new GoogleClassroomScript();
        WithCourse(script, 1, 1);
        WithCourse(script, 2, Hidden, maxPoints: 1e30, name: "Test Course 2 renamed in Google");
        string? before = null;
        await using var host = await StartAsync(
            database,
            script,
            ct,
            seed: async (h, token) =>
            {
                var course = await CourseRows.InsertCourseAsync(h, token, googleId: CourseTestData.CourseId(2), name: "Test Course 2 as stored");
                var person = await CourseRows.InsertParticipantAsync(
                    h, token, googleUserId: CourseTestData.UserId(Hidden), email: Email(Hidden), fullName: CourseTestData.Name(Hidden));
                await CourseRows.InsertMembershipAsync(h, course, person, token);
                var item = await CourseWorkRows.InsertCourseWorkAsync(
                    h, course, token, googleId: CourseWorkTestData.ItemId(Hidden), title: CourseWorkTestData.Title(Hidden), maxPoints: 10m);
                await CourseWorkRows.InsertSubmissionAsync(
                    h, item, person, token, googleId: CourseWorkTestData.SubmissionId(Hidden), assignedGrade: 7m);
                before = await FingerprintAsync(h, CourseTestData.CourseId(2), token);
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(1, run.ProcessedCount);
        Assert.NotNull(before);
        Assert.Contains("Test Course 2 as stored", before, StringComparison.Ordinal);
        Assert.Equal(before, await FingerprintAsync(host, CourseTestData.CourseId(2), ct));
        Assert.Equal(1, await CountAsync(host, "course", CourseTestData.CourseId(1), ct));
    }

    /// <summary>
    /// AC-004, FR-003, §7, SC-10: with AC-001's data the run log has exactly one <c>SyncCourseValueOutOfRange</c>
    /// Warning, event 5135, naming the run and course 2; no line of the log carries the value in any of the checked
    /// formats, the item's id or title, the student's name, address or id, or the submission id. The completion is
    /// asserted first — "nothing in the log" is free when the run stopped early.
    /// </summary>
    [Fact]
    public async Task TheSkip_IsLoggedOnce_WithoutTheValueOrAnyIdentityOfTheCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ThreeCoursesSecondOutOfRange(), ct);

        var run = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);
        var files = await host.ReadLogFilesAsync(ct);
        var events = LogEvent.Parse(files);

        var line = Assert.Single(events, e => e.EventName == ValueOutOfRangeEvent);
        Assert.Equal("Warning", line.Level);
        Assert.Equal(ValueOutOfRangeEventId, line.Json.GetProperty("EventId").GetProperty("Id").GetInt32());
        Assert.Equal(run.RunId.ToString(), line.Property("RunId"));
        Assert.Contains(
            line.Json.EnumerateObject(),
            p => p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == CourseTestData.CourseId(2));

        // Ordinal: the upper-case exponent forms cannot occur by chance in the lower-case GUIDs every line carries.
        var all = string.Join('\n', files);
        foreach (var forbidden in ValueTexts.Concat(
                 [
                     CourseWorkTestData.ItemId(Hidden), CourseWorkTestData.Title(Hidden), CourseTestData.Name(Hidden),
                     Email(Hidden), CourseTestData.UserId(Hidden), CourseWorkTestData.SubmissionId(Hidden),
                 ]))
        {
            Assert.DoesNotContain(forbidden, all, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC-009, FR-002.2: course 1 is skipped for an out-of-range value and course 2's roster then answers 403 — the run
    /// fails with that failure's diagnosis (<c>TechnicalAccountCannotRead</c>), not <c>Unexpected</c>, and the skipped
    /// course is still reported by its own log line.
    /// </summary>
    [Fact]
    public async Task ASkipFollowedByAnotherFailure_IsStillReported_AndTheRunFailsWithThatFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = new GoogleClassroomScript();
        WithCourse(script, 1, Hidden, maxPoints: 1e30);
        WithCourse(script, 2, 2);
        script.ForbidRoster(CourseTestData.CourseId(2));
        await using var host = await StartAsync(database, script, ct);

        var run = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Failed, run.Status);
        Assert.Equal("TechnicalAccountCannotRead", run.LastError);
        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunFailed, ct);

        var line = Assert.Single(events, e => e.EventName == ValueOutOfRangeEvent);
        Assert.Equal(run.RunId.ToString(), line.Property("RunId"));
        Assert.Contains(
            line.Json.EnumerateObject(),
            p => p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == CourseTestData.CourseId(1));
    }

    /// <summary>
    /// AC-010, FR-001: a maximum, an assigned grade and a draft grade of 7.9·10²⁸ — just inside the range — are stored as
    /// today's conversion gives them and the course is not skipped. Green before the change by design.
    /// </summary>
    [Fact]
    public async Task ValuesJustInsideTheRange_AreStored_AndTheCourseIsNotSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = new GoogleClassroomScript();
        WithCourse(script, 1, 1, maxPoints: 7.9e28, assigned: 7.9e28, draft: 7.9e28);
        await using var host = await StartAsync(database, script, ct);

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(1, run.ProcessedCount);
        var expected = (decimal)7.9e28;
        Assert.Equal(
            expected,
            await host.ScalarAsync<decimal?>("SELECT max_points FROM course_work WHERE google_id = @id", ct, ("id", CourseWorkTestData.ItemId(1))));
        var grades = await host.QueryAsync(
            "SELECT assigned_grade, draft_grade FROM submission WHERE google_id = @id",
            r => (r.GetDecimal(0), r.GetDecimal(1)),
            ct,
            ("id", CourseWorkTestData.SubmissionId(1)));
        Assert.Equal((expected, expected), Assert.Single(grades));
    }
}
