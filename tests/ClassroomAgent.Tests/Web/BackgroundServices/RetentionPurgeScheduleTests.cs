using ClassroomAgent.Tests.TestInfrastructure;
using ClassroomAgent.Web.BackgroundServices;
using static ClassroomAgent.Tests.TestInfrastructure.RetentionPurgeTestData;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-037 AC-011 and AC-012, spec FR-013 … FR-015, OD-001, OD-002, I-1, I-2: the production host purges once shortly
/// after start, then every 24 hours counted from the previous start, never while a synchronization run holds the
/// gate, and stops promptly. Time is the injected clock — nothing here sleeps (TC-4).
/// </summary>
public sealed class RetentionPurgeScheduleTests(PostgreSqlFixture database)
{
    /// <summary>AC-012, I-2: the first run needs no time to pass, and it deletes what is expired.</summary>
    [Fact]
    public async Task TheFirstPurge_RunsShortlyAfterStart()
    {
        var ct = TestContext.Current.CancellationToken;
        long courseId = 0;
        await using var host = await RetentionPurgeHost.StartWithPurgeServiceAsync(
            database,
            ct,
            async (h, token) =>
            {
                courseId = await CourseRows.InsertCourseAsync(h, token, updateTime: Old);
            });

        await host.WaitForPurgeRunsAsync(1, ct);

        var row = Assert.Single(await host.PurgeAuditRowsAsync(ct));
        Assert.Equal(Now, row.OccurredAt);
        Assert.Equal(0L, await host.CountAsync("course", ct, "id = @id", ("id", courseId)));
    }

    /// <summary>AC-012, OD-001, I-1: the next run is due 24 hours after the previous one started.</summary>
    [Fact]
    public async Task TheNextPurge_IsDue24HoursLater()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithPurgeServiceAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);

        await host.Time.WaitForTimerAtAsync(Now + RetentionPurgeBackgroundService.Interval, ct);
        await host.Time.AdvanceWhenDueAsync(RetentionPurgeBackgroundService.Interval, ct);
        await host.WaitForPurgeRunsAsync(2, ct);

        var rows = await host.PurgeAuditRowsAsync(ct);
        Assert.Equal(2, rows.Count);
        Assert.Equal(Now + RetentionPurgeBackgroundService.Interval, rows[1].OccurredAt);
    }

    /// <summary>AC-012: before 24 hours have passed there is no second run.</summary>
    [Fact]
    public async Task BeforeTheInterval_ThereIsNoSecondPurge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithPurgeServiceAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        await host.Time.WaitForTimerAtAsync(Now + RetentionPurgeBackgroundService.Interval, ct);

        host.Time.Advance(RetentionPurgeBackgroundService.Interval - TimeSpan.FromMinutes(1));

        Assert.Single(await host.PurgeAuditRowsAsync(ct));
        Assert.Contains(Now + RetentionPurgeBackgroundService.Interval, host.Time.PendingDueTimes);
    }

    /// <summary>
    /// AC-011, OD-002: a purge that falls due while a synchronization run holds the gate waits for it, says so in the
    /// log, and runs when the run ends.
    /// </summary>
    [Fact]
    public async Task APurgeDueDuringASynchronizationRun_WaitsForItsEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithPurgeServiceAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        await host.Time.WaitForTimerAtAsync(Now + RetentionPurgeBackgroundService.Interval, ct);
        var coordinator = SyncHostExtensions.CoordinatorOf(host);
        await host.Time.WaitUntilAsync(coordinator.TryStartScheduledRun, "the gate to be free for a synchronization run", ct);

        host.Time.Advance(RetentionPurgeBackgroundService.Interval);
        await host.WaitForLogEventAsync(Events.WaitingForSync, ct);
        Assert.Single(await host.PurgeAuditRowsAsync(ct));

        coordinator.RunCompleted();
        await host.WaitForPurgeRunsAsync(2, ct);
    }

    /// <summary>AC-012, FR-015: the host stops promptly while the purge service is waiting for its next run.</summary>
    [Fact]
    public async Task TheHost_StopsPromptly()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await RetentionPurgeHost.StartWithPurgeServiceAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);

        var stop = host.StopAsync();
        var finished = await Task.WhenAny(stop, Task.Delay(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Same(stop, finished);
        await stop;
        await host.DisposeAsync();
    }
}
