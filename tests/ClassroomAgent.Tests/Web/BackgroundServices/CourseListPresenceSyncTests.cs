using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-059 AC-010, spec FR-001, FR-005, FR-006, FR-007, SC-10, db-design §5, §6: synchronization runs through the real
/// host, its background service and PostgreSQL (TC-2), the Classroom port substituted (TC-4). A real run stores a
/// course's mark as its code with the run's instant and the sightings; reads the account-change instant from the
/// database; sets the empty-list flag; and writes one log line per run counting the course marks — naming nothing.
/// Every value and identity is synthetic.
/// </summary>
public sealed class CourseListPresenceSyncTests(PostgreSqlFixture database)
{
    /// <summary>
    /// The run-log events that existed before this Story (spec §1.1: 5121 … 5136, 5129 unused). FR-007.4 leaves the new
    /// events' ids and names to IMPLEMENTATION, so a line is found as a synchronization event of the run outside this set.
    /// </summary>
    private static readonly HashSet<int> EventsBeforeThisStory = [5121, 5122, 5123, 5124, 5125, 5126, 5127, 5128, 5130, 5131, 5132, 5133, 5134, 5135, 5136];

    /// <summary>A grade no other number of these runs can be mistaken for in a log (AC-010).</summary>
    private const decimal Grade = 7.25m;

    private const string RenamedCourse = "Course renamed in Classroom";

    private static readonly DateTimeOffset ItemDate = InstallationTestHost.DefaultStart - TimeSpan.FromDays(10);

    private static RosterEntry Student(int n) =>
        new(CourseTestData.UserId(n), CourseTestData.Email($"person{n}"), CourseTestData.Name(n));

    /// <summary>Course <paramref name="n"/> with one student, one item and the student's graded submission.</summary>
    private static void Course(FakeClassroomReader reader, int n, string? name = null) =>
        reader
            .WithCourse(CourseTestData.CourseId(n), name ?? CourseTestData.CourseName(n), students: [Student(n)])
            .WithCourseWork(CourseTestData.CourseId(n), CourseWorkTestData.ItemId(n), CourseWorkResource.CourseWork, CourseWorkTestData.Title(n), ItemDate, maxPoints: 10m)
            .WithSubmission(CourseTestData.CourseId(n), CourseWorkTestData.ItemId(n), CourseTestData.UserId(n), CourseWorkTestData.SubmissionId(n), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: Grade);

    private static Action<FakeClassroomReader> Courses(params int[] courses) =>
        reader =>
        {
            foreach (var n in courses)
            {
                Course(reader, n);
            }
        };

    private static Task<InstallationTestHost> StartAsync(PostgreSqlFixture database, Action<FakeClassroomReader> classroom, CancellationToken ct) =>
        SyncHostExtensions.StartAsync(database, ct, intervalMinutes: 1, classroom: classroom);

    private static async Task<SyncHostExtensions.SyncStateRow> NextRunAsync(
        InstallationTestHost host,
        SyncHostExtensions.SyncStateRow previous,
        CancellationToken ct)
    {
        await host.WaitForSecondRunAsync(previous, ct);
        return Assert.Single(await host.SyncStatesAsync(ct));
    }

    /// <summary>A course's mark, its instant and its last sighting as stored.</summary>
    private static async Task<(string? Mark, DateTimeOffset? Since, DateTimeOffset? Listed)> MarkAsync(
        InstallationTestHost host,
        int course,
        CancellationToken ct) =>
        Assert.Single(await host.QueryAsync(
            "SELECT absence_mark, absent_since, last_listed_at FROM course WHERE google_id = @id",
            r => (r.IsDBNull(0) ? null : r.GetString(0),
                  r.IsDBNull(1) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(1),
                  r.IsDBNull(2) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(2)),
            ct,
            ("id", CourseTestData.CourseId(course))));

    /// <summary>The run's synchronization events this Story added (FR-007.4).</summary>
    private static List<LogEvent> NewSyncEventsOf(IEnumerable<LogEvent> events, Guid runId) =>
        events
            .Where(e => e.Property("RunId") == runId.ToString()
                        && e.Json.TryGetProperty("EventId", out var id)
                        && id.ValueKind == JsonValueKind.Object
                        && id.TryGetProperty("Id", out var number)
                        && number.GetInt32() is >= 5100 and < 5200
                        && !EventsBeforeThisStory.Contains(number.GetInt32()))
            .ToList();

    /// <summary>Waits (real time, bounded) until a new line of the run is in the log; the line's place in the run is not fixed.</summary>
    private static async Task<(LogEvent Line, IReadOnlyList<string> Files)> WaitForNewLineAsync(
        InstallationTestHost host,
        Guid runId,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + ManualTimeProvider.RealTimeLimit;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var files = await host.ReadLogFilesWhileRunningAsync(ct);
                if (NewSyncEventsOf(LogEvent.Parse(files), runId) is [var line, ..])
                {
                    return (line, files);
                }
            }
            catch (JsonException)
            {
                // A line can be torn while the host is writing it; the next read sees it whole.
            }

            await Task.Delay(25, ct);
        }

        Assert.Fail($"No new synchronization log line of run {runId} appeared within {ManualTimeProvider.RealTimeLimit}.");
        throw new InvalidOperationException("unreachable");
    }

    /// <summary>The numbers a log line carries as structured properties, sorted.</summary>
    private static int[] NumbersOf(LogEvent line) =>
        line.Json.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.Number)
            .Select(p => p.Value.GetInt32())
            .Order()
            .ToArray();

    /// <summary>
    /// AC-010, FR-007.1, FR-007.3, I-3, SC-10: run 2 marks course 3; run 3 marks course 2 and brings course 3 back under a
    /// new name. Run 2's line carries the counts 1, 0, 0 and run 3's — its one new line — 1, 0, 1 (1 marked "not in
    /// Classroom", 0 "not visible", 1 unmarked; the three as a multiset, their names being IMPLEMENTATION's). No line of
    /// any run carries a course's name or Google id, an item, a person or a grade.
    /// </summary>
    [Fact]
    public async Task EachRun_LogsOneLineCountingCourseMarks_AndNamesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, Courses(1, 2, 3), ct);
        var first = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, first.Status);

        host.Classroom.WithoutCourse(CourseTestData.CourseId(3));
        var second = await NextRunAsync(host, first, ct);
        Assert.Equal(SyncTestData.Status.Completed, second.Status);
        var (secondLine, _) = await WaitForNewLineAsync(host, second.RunId, ct);

        host.Classroom.WithoutCourse(CourseTestData.CourseId(2));
        host.Classroom.WithCourse(CourseTestData.CourseId(3), RenamedCourse, students: [Student(3)]);
        var third = await NextRunAsync(host, second, ct);
        Assert.Equal(SyncTestData.Status.Completed, third.Status);
        var (thirdLine, files) = await WaitForNewLineAsync(host, third.RunId, ct);

        Assert.Equal([0, 0, 1], NumbersOf(secondLine));
        Assert.Single(NewSyncEventsOf(LogEvent.Parse(files), third.RunId));
        Assert.Equal([0, 1, 1], NumbersOf(thirdLine));
        Assert.Equal("not_in_classroom", (await MarkAsync(host, 2, ct)).Mark);
        Assert.Null((await MarkAsync(host, 3, ct)).Mark);
        Assert.Equal(RenamedCourse, await host.ScalarAsync<string>("SELECT name FROM course WHERE google_id = @id", ct, ("id", CourseTestData.CourseId(3))));

        var all = string.Join('\n', files);
        foreach (var forbidden in new[]
                 {
                     CourseTestData.CourseName(2), CourseTestData.CourseName(3), RenamedCourse,
                     CourseTestData.CourseId(2), CourseTestData.CourseId(3),
                     CourseWorkTestData.Title(2), CourseWorkTestData.Title(3), CourseWorkTestData.ItemId(2), CourseWorkTestData.ItemId(3),
                     CourseTestData.Name(2), CourseTestData.Name(3), CourseTestData.Email("person2"), CourseTestData.Email("person3"),
                     CourseTestData.UserId(2), CourseTestData.UserId(3),
                     "7.25",
                 })
        {
            Assert.DoesNotContain(forbidden, all, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// FR-005.2, FR-007.2, AC-006: the list comes back empty while two courses are stored. The run completes, marks
    /// nothing, stores the flag, and writes one line carrying the number of stored courses and nothing else.
    /// </summary>
    [Fact]
    public async Task AnEmptyList_SetsTheFlag_MarksNothing_AndLogsTheStoredCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, Courses(1, 2), ct);
        var first = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, first.Status);
        Assert.False(await host.ScalarAsync<bool>("SELECT last_course_list_empty FROM sync_state", ct));

        host.Classroom.WithoutCourse(CourseTestData.CourseId(1)).WithoutCourse(CourseTestData.CourseId(2));
        var second = await NextRunAsync(host, first, ct);
        Assert.Equal(SyncTestData.Status.Completed, second.Status);
        var (line, files) = await WaitForNewLineAsync(host, second.RunId, ct);

        Assert.True(await host.ScalarAsync<bool>("SELECT last_course_list_empty FROM sync_state", ct));
        Assert.Equal(0, await host.CountAsync("course", ct, "absence_mark IS NOT NULL"));
        Assert.Single(NewSyncEventsOf(LogEvent.Parse(files), second.RunId));
        Assert.Equal([2], NumbersOf(line));
    }

    /// <summary>
    /// FR-001.2, FR-003.1, VR-004, db-design §2: a real run stores the absent course's mark as the code
    /// <c>not_in_classroom</c> with the run's instant, and that same instant as the listed course's last sighting —
    /// within the run's own start and finish.
    /// </summary>
    [Fact]
    public async Task ARealRun_StoresTheMarkCode_TheRunInstant_AndTheSighting()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, Courses(1, 2), ct);
        var first = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, first.Status);

        host.Classroom.WithoutCourse(CourseTestData.CourseId(2));
        var second = await NextRunAsync(host, first, ct);
        Assert.Equal(SyncTestData.Status.Completed, second.Status);

        var listed = await MarkAsync(host, 1, ct);
        var absent = await MarkAsync(host, 2, ct);
        Assert.Null(listed.Mark);
        Assert.Equal("not_in_classroom", absent.Mark);
        Assert.NotNull(listed.Listed);
        Assert.Equal(listed.Listed, absent.Since);
        Assert.InRange(absent.Since!.Value, first.FinishedAt!.Value, second.FinishedAt!.Value);
    }

    /// <summary>
    /// FR-006.1, db-design §6 R2: the account-change instant stored in <c>workspace_connection</c> after a course was last
    /// seen makes a real run mark the lost course <c>not_visible_to_technical_account</c>.
    /// </summary>
    [Fact]
    public async Task ARealRun_AfterTheAccountChanged_MarksALostCourseNotVisible()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, Courses(1, 2), ct);
        var first = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, first.Status);
        var seen = (await MarkAsync(host, 2, ct)).Listed;
        Assert.NotNull(seen);
        await host.ExecuteAsync(
            "UPDATE workspace_connection SET technical_account_changed_at = @at",
            ct,
            ("at", seen.Value + TimeSpan.FromSeconds(1)));

        host.Classroom.WithoutCourse(CourseTestData.CourseId(2));
        var second = await NextRunAsync(host, first, ct);
        Assert.Equal(SyncTestData.Status.Completed, second.Status);

        Assert.Equal("not_visible_to_technical_account", (await MarkAsync(host, 2, ct)).Mark);
        Assert.Null((await MarkAsync(host, 1, ct)).Mark);
    }
}
