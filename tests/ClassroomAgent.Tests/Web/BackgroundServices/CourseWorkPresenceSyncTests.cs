using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-058 AC-006, AC-007, AC-012, AC-013 (spec FR-001.5, FR-004, FR-005, FR-008, SC-10): synchronization runs through
/// the real host, its background service and PostgreSQL (TC-2), the Classroom port substituted (TC-4). A failure inside
/// a course's transaction rolls its marks back with the rest of it; one log line per run counts the items newly marked
/// and unmarked and names nothing; a skip record and a last-read instant are written by a real run. Every value and
/// identity is synthetic.
/// </summary>
public sealed class CourseWorkPresenceSyncTests(PostgreSqlFixture database)
{
    private const string RejectSubmissionConstraint = "us058_test_reject_submission";

    /// <summary>
    /// The run-log events that existed before this Story (spec §1.1: 5121 … 5135, 5129 unused). FR-004.4 leaves the new
    /// event's id and name to IMPLEMENTATION, so the line is found as the run's one synchronization event outside this set.
    /// </summary>
    private static readonly HashSet<int> EventsBeforeThisStory = [5121, 5122, 5123, 5124, 5125, 5126, 5127, 5128, 5130, 5131, 5132, 5133, 5134, 5135];

    /// <summary>A grade no other number of these runs can be mistaken for in a log (AC-012).</summary>
    private const decimal Grade = 7.25m;

    private static readonly DateTimeOffset ItemDate = InstallationTestHost.DefaultStart - TimeSpan.FromDays(10);

    private static RosterEntry Student =>
        new(CourseTestData.UserId(1), CourseTestData.Email("person1"), CourseTestData.Name(1));

    private static void Item(FakeClassroomReader reader, int item, string? title = null) =>
        reader
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(item), CourseWorkResource.CourseWork, title ?? CourseWorkTestData.Title(item), ItemDate, maxPoints: 10m)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(item), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(item), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: Grade);

    /// <summary>Course 1 with its one student and the given items, each with the student's graded submission.</summary>
    private static Action<FakeClassroomReader> CourseWithItems(params int[] items) =>
        reader =>
        {
            reader.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Student]);
            foreach (var item in items)
            {
                Item(reader, item);
            }
        };

    private static Task<InstallationTestHost> StartAsync(PostgreSqlFixture database, Action<FakeClassroomReader> classroom, CancellationToken ct) =>
        SyncHostExtensions.StartAsync(database, ct, intervalMinutes: 1, classroom: classroom);

    /// <summary>The mark of one item as stored; null while the item is in Classroom.</summary>
    private static async Task<DateTimeOffset?> MarkAsync(InstallationTestHost host, int item, CancellationToken ct) =>
        Assert.Single(await host.QueryAsync(
            "SELECT not_in_classroom_since FROM course_work WHERE google_id = @id",
            r => r.IsDBNull(0) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(0),
            ct,
            ("id", CourseWorkTestData.ItemId(item))));

    private static async Task<SyncHostExtensions.SyncStateRow> NextRunAsync(
        InstallationTestHost host,
        SyncHostExtensions.SyncStateRow previous,
        CancellationToken ct)
    {
        await host.WaitForSecondRunAsync(previous, ct);
        return Assert.Single(await host.SyncStatesAsync(ct));
    }

    /// <summary>
    /// The run's synchronization events this Story added (FR-004.4): those carrying its run id whose event id is in the
    /// synchronization range and not one of <see cref="EventsBeforeThisStory"/>.
    /// </summary>
    private static List<LogEvent> NewSyncEventsOf(IEnumerable<LogEvent> events, Guid runId) =>
        events
            .Where(e => e.Property("RunId") == runId.ToString()
                        && e.Json.TryGetProperty("EventId", out var id)
                        && id.ValueKind == JsonValueKind.Object
                        && id.TryGetProperty("Id", out var number)
                        && number.GetInt32() is >= 5100 and < 5200
                        && !EventsBeforeThisStory.Contains(number.GetInt32()))
            .ToList();

    /// <summary>Waits (real time, bounded) until the run's presence line is in the log; the line's place in the run is not fixed.</summary>
    private static async Task<(LogEvent Line, IReadOnlyList<string> Files)> WaitForPresenceLineAsync(
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

    /// <summary>The numbers a log line carries as structured properties, sorted — FR-004.1: exactly the two counts.</summary>
    private static int[] NumbersOf(LogEvent line) =>
        line.Json.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.Number)
            .Select(p => p.Value.GetInt32())
            .Order()
            .ToArray();

    /// <summary>
    /// AC-012, FR-004, I-4, SC-10: run 2 marks items 2 and 4; run 3 brings item 2 back under a new title, marks item 3 and
    /// finds item 4 still absent. Run 3 writes one line counting 1 marked and 1 unmarked (item 4, already marked, is not
    /// counted), run 2's line counts 2 and 0, and no line of any run carries an item's title or id, a person or a grade.
    /// </summary>
    [Fact]
    public async Task EachRun_LogsOneLineCountingMarkedAndUnmarkedItems_AndNamesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, CourseWithItems(1, 2, 3, 4), ct);
        var first = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, first.Status);

        host.Classroom.WithoutCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(2));
        host.Classroom.WithoutCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(4));
        var second = await NextRunAsync(host, first, ct);
        Assert.Equal(SyncTestData.Status.Completed, second.Status);
        var (secondLine, _) = await WaitForPresenceLineAsync(host, second.RunId, ct);

        host.Classroom.WithoutCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(3));
        host.Classroom.WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(2), CourseWorkResource.CourseWork, "Back with a new title", ItemDate, maxPoints: 10m);
        var third = await NextRunAsync(host, second, ct);
        Assert.Equal(SyncTestData.Status.Completed, third.Status);
        var (thirdLine, files) = await WaitForPresenceLineAsync(host, third.RunId, ct);

        Assert.Equal([0, 2], NumbersOf(secondLine));
        Assert.Single(NewSyncEventsOf(LogEvent.Parse(files), third.RunId));
        Assert.Equal([1, 1], NumbersOf(thirdLine));
        Assert.DoesNotContain(CourseTestData.CourseId(1), thirdLine.Line, StringComparison.Ordinal);
        Assert.Null(await MarkAsync(host, 2, ct));
        Assert.NotNull(await MarkAsync(host, 3, ct));
        Assert.NotNull(await MarkAsync(host, 4, ct));

        var all = string.Join('\n', files);
        foreach (var forbidden in new[]
                 {
                     CourseWorkTestData.Title(1), CourseWorkTestData.Title(2), CourseWorkTestData.Title(3), CourseWorkTestData.Title(4),
                     "Back with a new title",
                     CourseWorkTestData.ItemId(1), CourseWorkTestData.ItemId(2), CourseWorkTestData.ItemId(3), CourseWorkTestData.ItemId(4),
                     CourseTestData.Name(1), CourseTestData.Email("person1"), CourseTestData.UserId(1),
                     "7.25",
                 })
        {
            Assert.DoesNotContain(forbidden, all, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC-006, FR-001.5, SPEC_REVIEW m-4: the course starts with item 2 marked. In run 3 Google returns item 2 again (it
    /// would lose its mark) and no longer returns item 1 (it would gain one), and the database then rejects a new
    /// submission of the course — a failure <b>inside</b> the course's transaction, after its items were written. The run
    /// stops, and both marks are exactly as before it.
    /// </summary>
    [Fact]
    public async Task AFailureInsideTheCourseTransaction_RollsItsMarksBack()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, CourseWithItems(1, 2), ct);
        var first = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, first.Status);

        host.Classroom.WithoutCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(2));
        var second = await NextRunAsync(host, first, ct);
        Assert.Equal(SyncTestData.Status.Completed, second.Status);
        var marked = await MarkAsync(host, 2, ct);
        Assert.NotNull(marked);

        host.Classroom.WithoutCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1));
        host.Classroom.WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(2), CourseWorkResource.CourseWork, CourseWorkTestData.Title(2), ItemDate, maxPoints: 10m);
        host.Classroom.WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(2), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(9), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: Grade);
        await host.ExecuteAsync(
            $"ALTER TABLE submission ADD CONSTRAINT {RejectSubmissionConstraint} CHECK (google_id <> '{CourseWorkTestData.SubmissionId(9)}')",
            ct);
        var third = await NextRunAsync(host, second, ct);

        Assert.Equal(SyncTestData.Status.Failed, third.Status);
        Assert.Equal(marked, await MarkAsync(host, 2, ct));
        Assert.Null(await MarkAsync(host, 1, ct));
    }

    /// <summary>
    /// AC-007, AC-013, FR-005, FR-008, VR-004: a real run records a course of a state the program does not know in
    /// <c>sync_skipped_course</c> with its id, list name, reason code and the run's instant, and stores that instant as
    /// the last successful read of the course it read in full.
    /// </summary>
    [Fact]
    public async Task ARealRun_RecordsTheSkippedCourse_AndTheLastReadInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        var startedAt = InstallationTestHost.DefaultStart;
        await using var host = await StartAsync(
            database,
            reader =>
            {
                CourseWithItems(1)(reader);
                reader.WithCourse(CourseTestData.CourseId(2), "Unknown state course", "COURSE_STATE_FROM_THE_FUTURE");
            },
            ct);

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        var record = Assert.Single(await host.QueryAsync(
            "SELECT course_google_id, course_name, reason, skipped_since FROM sync_skipped_course",
            r => (r.GetString(0), r.GetString(1), r.GetString(2), r.GetFieldValue<DateTimeOffset>(3)),
            ct));
        Assert.Equal((CourseTestData.CourseId(2), "Unknown state course", "unknown_course_state"), (record.Item1, record.Item2, record.Item3));
        var lastRead = Assert.Single(await host.QueryAsync(
            "SELECT last_complete_read_at FROM course WHERE google_id = @id",
            r => r.GetFieldValue<DateTimeOffset>(0),
            ct,
            ("id", CourseTestData.CourseId(1))));
        Assert.Equal(record.Item4, lastRead);
        Assert.InRange(lastRead, startedAt, run.FinishedAt!.Value);
    }
}
