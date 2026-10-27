using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-059 AC-009 (spec FR-008, VR-005, SC-10; BR-043 v100): a synchronization run through the real host, its background
/// service, PostgreSQL (TC-2) and the <b>real</b> Google Classroom adapter over a scripted transport
/// (<see cref="GoogleClassroomScript"/>, TC-4), so a negative grade, draft grade or maximum arrives as the JSON number
/// Google would send. Each such course is skipped like one with a value beyond the range — the run no longer stops on
/// the database's check constraint. Every value and identity is synthetic (the <c>ValueOutOfRangeSyncTests</c>
/// precedent).
/// </summary>
public sealed class NegativeValueSyncTests(PostgreSqlFixture database)
{
    private const string ValueOutOfRangeEvent = "SyncCourseValueOutOfRange";

    /// <summary>The identities only the three skipped courses carry, so their absence from the log is meaningful.</summary>
    private const int Assigned = 61;

    private const int Draft = 62;

    private const int Maximum = 63;

    /// <summary>Exact binary fractions no timestamp or counter of the run is likely to print (AC-009: no values in the log).</summary>
    private static readonly string[] ValueTexts = ["3.0625", "2.4375", "11.8125"];

    private static string Email(int ordinal) => CourseTestData.Email($"person{ordinal}");

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

    /// <summary>
    /// Everything stored of one course, as text: the course row and its items, memberships and submissions. The course
    /// row's <c>updated_at</c> is left out: US-059 FR-003.1 records a listed course's sighting on that row even when the
    /// run skips it, which moves the timestamp but no mirrored value.
    /// </summary>
    private static Task<string?> FingerprintAsync(InstallationTestHost host, string courseGoogleId, CancellationToken ct) =>
        host.ScalarAsync<string>(
            """
            SELECT concat_ws(' | ',
                concat_ws(',', c.name, c.course_state, c.update_time, c.complete_read_update_time),
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
    /// AC-009, FR-008.1: course 1 with a negative assigned grade, course 2 with a negative draft grade, course 3 — stored by
    /// an earlier run — with a negative maximum, course 4 with ordinary values. The run completes; course 4 is loaded;
    /// courses 1 and 2 are not; course 3 is exactly as stored; each of the three has a skip record with reason
    /// <c>value_out_of_range</c>; the log has one <c>SyncCourseValueOutOfRange</c> line per course naming its id; and no
    /// line carries a value, an item, a person or a submission of them.
    /// </summary>
    [Fact]
    public async Task CoursesWithNegativeValues_AreSkippedAndRecorded_AndTheRunCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = new GoogleClassroomScript();
        WithCourse(script, 1, Assigned, assigned: -3.0625);
        WithCourse(script, 2, Draft, assigned: null, draft: -2.4375);
        WithCourse(script, 3, Maximum, maxPoints: -11.8125, name: "Test Course 3 renamed in Google");
        WithCourse(script, 4, 4);
        string? before = null;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            seed: async (h, token) =>
            {
                var course = await CourseRows.InsertCourseAsync(h, token, googleId: CourseTestData.CourseId(3), name: "Test Course 3 as stored");
                var person = await CourseRows.InsertParticipantAsync(
                    h, token, googleUserId: CourseTestData.UserId(Maximum), email: Email(Maximum), fullName: CourseTestData.Name(Maximum));
                await CourseRows.InsertMembershipAsync(h, course, person, token);
                var item = await CourseWorkRows.InsertCourseWorkAsync(
                    h, course, token, googleId: CourseWorkTestData.ItemId(Maximum), title: CourseWorkTestData.Title(Maximum), maxPoints: 10m);
                await CourseWorkRows.InsertSubmissionAsync(
                    h, item, person, token, googleId: CourseWorkTestData.SubmissionId(Maximum), assignedGrade: 7m);
                before = await FingerprintAsync(h, CourseTestData.CourseId(3), token);
                script.InstallIn(h);
            });

        var run = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Null(run.LastError);
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);
        var files = await host.ReadLogFilesAsync(ct);

        Assert.Equal(1, run.ProcessedCount);
        Assert.Equal(1, await host.CountAsync("course", ct, "google_id = @id", ("id", CourseTestData.CourseId(4))));
        Assert.Equal(0, await host.CountAsync("course", ct, "google_id = @id", ("id", CourseTestData.CourseId(1))));
        Assert.Equal(0, await host.CountAsync("course", ct, "google_id = @id", ("id", CourseTestData.CourseId(2))));
        Assert.NotNull(before);
        Assert.Contains("Test Course 3 as stored", before, StringComparison.Ordinal);
        Assert.Equal(before, await FingerprintAsync(host, CourseTestData.CourseId(3), ct));

        Assert.Equal(
            new[] { CourseTestData.CourseId(1), CourseTestData.CourseId(2), CourseTestData.CourseId(3) },
            await host.QueryAsync(
                "SELECT course_google_id FROM sync_skipped_course WHERE reason = 'value_out_of_range' ORDER BY course_google_id",
                r => r.GetString(0),
                ct));

        var lines = LogEvent.Parse(files).Where(e => e.EventName == ValueOutOfRangeEvent).ToList();
        Assert.Equal(3, lines.Count);
        foreach (var n in new[] { 1, 2, 3 })
        {
            Assert.Contains(
                lines,
                line => line.Property("RunId") == run.RunId.ToString()
                        && line.Json.EnumerateObject().Any(p => p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == CourseTestData.CourseId(n)));
        }

        var all = string.Join('\n', files);
        foreach (var identity in new[] { Assigned, Draft, Maximum })
        {
            foreach (var forbidden in new[]
                     {
                         CourseWorkTestData.ItemId(identity), CourseWorkTestData.Title(identity), CourseTestData.Name(identity),
                         Email(identity), CourseTestData.UserId(identity), CourseWorkTestData.SubmissionId(identity),
                     })
            {
                Assert.DoesNotContain(forbidden, all, StringComparison.Ordinal);
            }
        }

        foreach (var value in ValueTexts)
        {
            Assert.DoesNotContain(value, all, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// FR-008.3: a zero maximum, assigned grade and draft grade are values — the course is loaded and the zeros stored.
    /// Green before the change by design: it pins that the check is strictly below zero.
    /// </summary>
    [Fact]
    public async Task ZeroValues_AreStored_AndTheCourseIsNotSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = new GoogleClassroomScript();
        WithCourse(script, 1, 1, maxPoints: 0, assigned: 0, draft: 0);
        await using var host = await SyncHostExtensions.StartAsync(database, ct, seed: (h, _) =>
        {
            script.InstallIn(h);
            return Task.CompletedTask;
        });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(1, run.ProcessedCount);
        Assert.Equal(0m, await host.ScalarAsync<decimal?>("SELECT max_points FROM course_work WHERE google_id = @id", ct, ("id", CourseWorkTestData.ItemId(1))));
        Assert.Equal(
            (0m, 0m),
            Assert.Single(await host.QueryAsync(
                "SELECT assigned_grade, draft_grade FROM submission WHERE google_id = @id",
                r => (r.GetDecimal(0), r.GetDecimal(1)),
                ct,
                ("id", CourseWorkTestData.SubmissionId(1)))));
        Assert.Equal(0, await host.CountAsync("sync_skipped_course", ct));
    }
}
