using ClassroomAgent.Tests.TestInfrastructure;
using ClassroomAgent.Web.BackgroundServices;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-013 AC-002 and OD-007: the coordinator guarantees at most one run at a time, whatever asks — the schedule
/// or the out-of-schedule request US-019's endpoint will make (spec FR-004). The seam exists here; nothing in
/// this Story calls it over HTTP.
/// </summary>
/// <remarks>
/// The guarantee is asserted on a coordinator of the test's own, not on the running host's: the host's instance
/// is shared with the service, which starts and finishes runs of its own, so a test that grabbed it would be
/// racing the production code instead of proving it. The integrated guarantee — that the row never holds two
/// runs — is asserted against the real host below.
/// </remarks>
public sealed class SyncRunCoordinationTests(PostgreSqlFixture database)
{
    /// <summary>AC-002: while a run is in progress the schedule cannot start a second one.</summary>
    [Fact]
    public void WhileARunIsInProgress_TheScheduleStartsNoSecondRun()
    {
        var coordinator = new SyncRunCoordinator();

        Assert.True(coordinator.TryStartScheduledRun());
        Assert.False(coordinator.TryStartScheduledRun());
    }

    /// <summary>AC-002, OD-007: a request arriving during a run does not start a parallel run.</summary>
    [Fact]
    public void WhileARunIsInProgress_ARequestStartsNoParallelRun()
    {
        var coordinator = new SyncRunCoordinator();
        Assert.True(coordinator.TryStartScheduledRun());

        coordinator.Request();

        Assert.False(coordinator.TryStartRequestedRun());
        Assert.False(coordinator.IsRequested);
    }

    /// <summary>AC-002: once a run reports itself finished, the next one may start.</summary>
    [Fact]
    public void AfterARunCompletes_TheNextOneMayStart()
    {
        var coordinator = new SyncRunCoordinator();
        Assert.True(coordinator.TryStartScheduledRun());

        coordinator.RunCompleted();

        Assert.True(coordinator.TryStartScheduledRun());
    }

    /// <summary>
    /// OD-007: a request made while nothing runs is accepted and is then startable — this is the entry point
    /// US-019 adds an endpoint in front of, with AD-5's rule that the request enqueues and returns.
    /// </summary>
    [Fact]
    public void ARequestMadeWhileIdle_IsAcceptedAndStartable()
    {
        var coordinator = new SyncRunCoordinator();

        Assert.True(coordinator.Request());

        Assert.True(coordinator.IsRequested);
        Assert.True(coordinator.TryStartRequestedRun());
    }

    /// <summary>OD-007: however many requests arrive during a run, one run follows — not one per request.</summary>
    [Fact]
    public void ManyRequestsDuringARun_LeaveOneRunToStart()
    {
        var coordinator = new SyncRunCoordinator();
        Assert.True(coordinator.TryStartScheduledRun());
        for (var i = 0; i < 5; i++)
        {
            coordinator.Request();
        }

        coordinator.RunCompleted();

        Assert.True(coordinator.TryStartRequestedRun());
        Assert.False(coordinator.TryStartRequestedRun());
    }

    /// <summary>AC-002: a waiter is signalled when the state changes, so the service never polls the clock.</summary>
    [Fact]
    public async Task AStateChange_SignalsTheWaiter()
    {
        var ct = TestContext.Current.CancellationToken;
        var coordinator = new SyncRunCoordinator();
        var changed = coordinator.Changed;

        coordinator.Request();

        await changed.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);
        Assert.True(changed.IsCompleted);
    }

    /// <summary>AC-002: the coordinator the host resolves is one instance for the process (spec FR-018).</summary>
    [Fact]
    public async Task TheCoordinator_IsOneInstanceForTheProcess()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);

        Assert.Same(SyncHostExtensions.CoordinatorOf(host), SyncHostExtensions.CoordinatorOf(host));
    }

    /// <summary>
    /// AC-002, AC-003: two runs never overlap in the row either — the row is never seen carrying a second
    /// run's identifier while the first is still running (OD-004, db-design §3.5).
    /// </summary>
    [Fact]
    public async Task TheRow_NeverHoldsTwoRunsAtOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, intervalMinutes: 1);
        await host.WaitForFinishedRunAsync(ct);

        for (var i = 0; i < 3; i++)
        {
            await host.Time.AdvanceWhenDueAsync(TimeSpan.FromMinutes(1), ct);
            Assert.Single(await host.SyncStatesAsync(ct));
        }
    }
}
