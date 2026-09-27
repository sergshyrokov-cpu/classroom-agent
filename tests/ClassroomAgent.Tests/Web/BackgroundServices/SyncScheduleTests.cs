using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-013 AC-002 and AC-008: a run starts on the configured interval counted from the completion of the previous
/// one, only one runs at a time, the first run waits for the first legitimacy determination, and a host stop
/// starts no new run and is not recorded as a failure (spec FR-003, FR-010, FR-011; OD-003, OD-006). Time is the
/// injected clock — nothing here sleeps.
/// </summary>
public sealed class SyncScheduleTests(PostgreSqlFixture database)
{
    /// <summary>AC-002, I-6: without the setting the interval is one hour (spec FR-013).</summary>
    [Fact]
    public async Task WithoutTheSetting_TheIntervalIsOneHour()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);
        var first = await host.WaitForFinishedRunAsync(ct);

        await host.Time.WaitForTimerAtAsync(first.FinishedAt!.Value + SyncTestData.DefaultInterval, ct);

        Assert.Contains(first.FinishedAt!.Value + SyncTestData.DefaultInterval, host.Time.PendingDueTimes);
    }

    /// <summary>AC-002: the configured interval is the one used (spec FR-013).</summary>
    [Fact]
    public async Task WithTheSetting_TheConfiguredIntervalIsUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, intervalMinutes: 15);
        var first = await host.WaitForFinishedRunAsync(ct);

        await host.Time.WaitForTimerAtAsync(first.FinishedAt!.Value + TimeSpan.FromMinutes(15), ct);

        Assert.DoesNotContain(first.FinishedAt!.Value + SyncTestData.DefaultInterval, host.Time.PendingDueTimes);
    }

    /// <summary>
    /// AC-002: the interval is counted from the completion of the previous run. With the empty pipeline of this
    /// Story a run takes no virtual time, so completion and start coincide and the two readings are not
    /// distinguishable here — what is asserted is that the timer sits at completion plus the interval, which is
    /// the reading the code must implement. US-014, whose step takes time, is where the distinction becomes
    /// observable (see the test-generation report).
    /// </summary>
    [Fact]
    public async Task TheIntervalIsMeasuredFromTheCompletionOfThePreviousRun()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, intervalMinutes: 30);
        var first = await host.WaitForFinishedRunAsync(ct);

        await host.Time.WaitForTimerAtAsync(first.FinishedAt!.Value + TimeSpan.FromMinutes(30), ct);

        Assert.Contains(first.FinishedAt!.Value + TimeSpan.FromMinutes(30), host.Time.PendingDueTimes);
    }

    /// <summary>AC-002: the second run happens when the interval elapses, and it reuses the one row (OD-004).</summary>
    [Fact]
    public async Task WhenTheIntervalElapses_ASecondRunHappens()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, intervalMinutes: 30);
        var first = await host.WaitForFinishedRunAsync(ct);

        await host.Time.AdvanceWhenDueAsync(TimeSpan.FromMinutes(30), ct);
        await host.Time.WaitUntilAsync(
            () => host.SyncStatesAsync(ct).GetAwaiter().GetResult() is [{ } row] && row.RunId != first.RunId,
            "a second run to replace the first in the one row",
            ct);

        var rows = await host.SyncStatesAsync(ct);
        Assert.Single(rows);
        Assert.NotEqual(first.RunId, rows[0].RunId);
    }

    /// <summary>
    /// AC-002, OD-003: the interval after a failed run is the same as after a successful one — hurrying a
    /// failure is US-017's business, not this Story's.
    /// </summary>
    [Fact]
    public async Task AfterAFailedRun_TheIntervalIsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, intervalMinutes: 30);
        var first = await host.WaitForFinishedRunAsync(ct);
        await host.ExecuteAsync(
            "UPDATE sync_state SET status = 'failed', last_error = @error",
            ct,
            ("error", SyncTestData.SyntheticError));

        await host.Time.WaitForTimerAtAsync(first.FinishedAt!.Value + TimeSpan.FromMinutes(30), ct);

        Assert.Contains(first.FinishedAt!.Value + TimeSpan.FromMinutes(30), host.Time.PendingDueTimes);
    }

    /// <summary>
    /// AC-002, OD-006: the first run waits for the first legitimacy determination — a fresh installation whose
    /// check has not answered yet is read-only by BR-025, so a run at the instant of start would always be
    /// skipped. Nothing is written until the determination arrives.
    /// </summary>
    [Fact]
    public async Task TheFirstRun_WaitsForTheFirstLegitimacyDetermination()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await AccessCheckHostExtensions.SeedConnectionAsync(host, SeededConnection.Usable, ct);
        var hanging = host.ControlPlane.ReplyLater();

        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        Assert.Empty(await host.SyncStatesAsync(ct));

        hanging.SetResult(FakeControlPlaneClient.Answer());
        await host.WaitForFinishedRunAsync(ct);
    }

    /// <summary>AC-008: a host stop starts no new run; the stop itself completes.</summary>
    [Fact]
    public async Task HostStop_StartsNoNewRun()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await SyncHostExtensions.StartAsync(database, ct, intervalMinutes: 30);
        await using var _host = host;
        var first = await host.WaitForFinishedRunAsync(ct);

        await host.StopAsync().WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        var rows = await host.SyncStatesAsync(ct);
        Assert.Equal(first.RunId, Assert.Single(rows).RunId);
    }

    /// <summary>
    /// AC-008: a stop is not a failed run — the row the stop leaves behind carries no error, whatever its
    /// status (spec FR-010, I-3).
    /// </summary>
    [Fact]
    public async Task HostStop_IsNotRecordedAsAFailedRun()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await SyncHostExtensions.StartAsync(database, ct, intervalMinutes: 30);
        await using var _host = host;
        await host.WaitForFinishedRunAsync(ct);

        await host.StopAsync().WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        var row = Assert.Single(await host.SyncStatesAsync(ct));
        Assert.NotEqual(SyncTestData.Status.Failed, row.Status);
        Assert.Null(row.LastError);
    }

    /// <summary>AC-002, AC-004: in read-only mode the schedule keeps running and writes nothing at all.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheScheduleKeepsRunning_AndWritesNothing(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, cause, intervalMinutes: 30);

        await HostLogs.WaitForEventAsync(host.LogDirectory, SyncTestData.LogEvents.RunSkipped, 1, ct);
        await host.Time.WaitUntilAsync(
            () => host.Time.PendingDueTimes.Count > 0,
            "the next run to be scheduled although this one was skipped",
            ct);

        Assert.Empty(await host.SyncStatesAsync(ct));
    }
}
