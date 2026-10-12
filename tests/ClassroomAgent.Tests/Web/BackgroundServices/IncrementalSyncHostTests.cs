using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-018 FR-001, FR-005, FR-007, FR-009: incremental synchronization in a real host over real PostgreSQL (TC-2).
/// The Classroom port is substituted (TC-4). A run started by the "Synchronize" button is the same run as the
/// scheduled one, so it skips an unchanged archived course exactly as the schedule does.
/// </summary>
public sealed class IncrementalSyncHostTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    private async Task<(InstallationTestHost Host, FakeClassroomReader Reader)> StartWithOneCourseAsync(
        string state,
        CancellationToken ct)
    {
        FakeClassroomReader? captured = null;
        var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader =>
            {
                captured = reader.WithCourse(
                    CourseTestData.CourseId(1),
                    CourseTestData.CourseName(1),
                    state,
                    students: [Person(1)],
                    updateTime: T);
            });
        return (host, captured!);
    }

    private static async Task<IReadOnlyList<LogEvent>> RunTwiceAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct, 1);
        SyncHostExtensions.CoordinatorOf(host).Request();
        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct, 2);
        return events.Where(e => e.EventName == SyncTestData.LogEvents.RunCompleted).ToList();
    }

    /// <summary>
    /// AC-007, FR-007: a button run is the same run as a scheduled one — it skips the unchanged archived course and
    /// makes no roster, coursework or submissions read for it.
    /// </summary>
    [Fact]
    public async Task AButtonRun_SkipsTheUnchangedArchive_AsAScheduledRunWould()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, reader) = await StartWithOneCourseAsync(CourseTestData.States.Archived, ct);
        await using var _ = host;

        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct, 1);
        Assert.Single(reader.RostersRead, id => id == CourseTestData.CourseId(1));

        SyncHostExtensions.CoordinatorOf(host).Request();
        var completed = (await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct, 2))
            .Where(e => e.EventName == SyncTestData.LogEvents.RunCompleted).ToList();

        Assert.Equal("1", completed[1].Property("SkippedUnchanged"));
        Assert.Single(reader.RostersRead, id => id == CourseTestData.CourseId(1));
        Assert.Single(reader.CourseWorkRead, id => id == CourseTestData.CourseId(1));
        Assert.Single(reader.SubmissionsRead, id => id == CourseTestData.CourseId(1));
    }

    /// <summary>
    /// AC-007, AC-003: a button run reads an active course again, as a scheduled run would — only an archived
    /// course is ever skipped.
    /// </summary>
    [Fact]
    public async Task AButtonRun_ReadsAnActiveCourse_AsAScheduledRunWould()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, reader) = await StartWithOneCourseAsync(CourseTestData.States.Active, ct);
        await using var _ = host;

        var completed = await RunTwiceAsync(host, ct);

        Assert.Equal("0", completed[1].Property("SkippedUnchanged"));
        Assert.Equal(2, reader.RostersRead.Count(id => id == CourseTestData.CourseId(1)));
    }

    /// <summary>
    /// AC-008, FR-009, spec I-1: the completion line carries the count of courses skipped as unchanged — 0 on the
    /// full first run, 1 on the next — and a skipped course is not counted as processed.
    /// </summary>
    [Fact]
    public async Task TheCompletionLine_CarriesTheSkippedCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, _) = await StartWithOneCourseAsync(CourseTestData.States.Archived, ct);
        await using var _ = host;

        var completed = await RunTwiceAsync(host, ct);

        Assert.Equal("0", completed[0].Property("SkippedUnchanged"));
        Assert.Equal("1", completed[1].Property("SkippedUnchanged"));
        Assert.Equal("0", completed[1].Property("ProcessedCount"));
    }

    /// <summary>
    /// AC-008, SC-10: the log names no skipped course — only the count is written.
    /// </summary>
    [Fact]
    public async Task TheLog_NamesNoSkippedCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, _) = await StartWithOneCourseAsync(CourseTestData.States.Archived, ct);
        await using var _ = host;

        var completed = await RunTwiceAsync(host, ct);
        var all = string.Join('\n', await host.ReadLogFilesWhileRunningAsync(ct));

        // The control: a course really was skipped, so the absences below are not the absence of a skip.
        Assert.Equal("1", completed[1].Property("SkippedUnchanged"));
        Assert.DoesNotContain(CourseTestData.CourseId(1), all, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.CourseName(1), all, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC-008, spec I-2: the failure line carries the skipped count too — 0 when the run failed before any course
    /// could be skipped.
    /// </summary>
    [Fact]
    public async Task TheFailureLine_CarriesTheSkippedCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncFailureHostExtensions.StartFailingAsync(
            database,
            ct,
            failures: reader => reader.CourseListingFailure =
                new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.ScopeNotAuthorized));

        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunFailed, ct);

        var line = Assert.Single(events, e => e.EventName == SyncTestData.LogEvents.RunFailed);
        Assert.Equal("0", line.Property("SkippedUnchanged"));
    }
}
