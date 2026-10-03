using ClassroomAgent.Web.BackgroundServices;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-037 AC-011, spec FR-014, OD-002: the purge and a synchronization run never overlap, a request made during a
/// purge is not lost, and the purge does not change what readiness reads (API design §3). Asserted on a coordinator
/// of the test's own, as <see cref="SyncRunCoordinationTests"/> does, so no production service races the test.
/// </summary>
public sealed class RetentionPurgeCoordinationTests
{
    [Fact]
    public void WhenNothingRuns_ThePurgeStarts()
    {
        var coordinator = new SyncRunCoordinator();

        Assert.True(coordinator.TryStartPurge());
    }

    [Fact]
    public void WhileASynchronizationRunIsInProgress_ThePurgeDoesNotStart()
    {
        var coordinator = new SyncRunCoordinator();
        Assert.True(coordinator.TryStartScheduledRun());

        Assert.False(coordinator.TryStartPurge());
    }

    [Fact]
    public void WhenTheSynchronizationRunEnds_ThePurgeStarts()
    {
        var coordinator = new SyncRunCoordinator();
        Assert.True(coordinator.TryStartScheduledRun());
        Assert.False(coordinator.TryStartPurge());

        coordinator.RunCompleted();

        Assert.True(coordinator.TryStartPurge());
    }

    [Fact]
    public void WhileThePurgeRuns_NoScheduledSynchronizationRunStarts()
    {
        var coordinator = new SyncRunCoordinator();
        Assert.True(coordinator.TryStartPurge());

        Assert.False(coordinator.TryStartScheduledRun());
    }

    [Fact]
    public void WhileThePurgeRuns_ASecondPurgeDoesNotStart()
    {
        var coordinator = new SyncRunCoordinator();
        Assert.True(coordinator.TryStartPurge());

        Assert.False(coordinator.TryStartPurge());
    }

    /// <summary>OD-002 with US-013 OD-007: a request made during a purge is remembered and starts after it.</summary>
    [Fact]
    public void ARequestMadeDuringThePurge_IsRememberedAndStartsAfterIt()
    {
        var coordinator = new SyncRunCoordinator();
        Assert.True(coordinator.TryStartPurge());

        Assert.True(coordinator.Request());
        Assert.False(coordinator.TryStartRequestedRun());

        coordinator.PurgeCompleted();

        Assert.True(coordinator.IsRequested);
        Assert.True(coordinator.TryStartRequestedRun());
    }

    [Fact]
    public void AfterThePurge_TheScheduledRunStarts()
    {
        var coordinator = new SyncRunCoordinator();
        Assert.True(coordinator.TryStartPurge());

        coordinator.PurgeCompleted();

        Assert.True(coordinator.TryStartScheduledRun());
    }

    /// <summary>Waiters are woken when the purge ends, as they are when a run ends.</summary>
    [Fact]
    public void TheEndOfThePurge_IsSignalled()
    {
        var coordinator = new SyncRunCoordinator();
        Assert.True(coordinator.TryStartPurge());
        var changed = coordinator.Changed;

        coordinator.PurgeCompleted();

        Assert.True(changed.IsCompleted);
    }

    /// <summary>API design §3: <c>IsRunning</c> still means "a synchronization run", so a purge is not one.</summary>
    [Fact]
    public void ThePurge_IsNotReportedAsASynchronizationRun()
    {
        var coordinator = new SyncRunCoordinator();
        Assert.True(coordinator.TryStartPurge());

        Assert.False(coordinator.IsRunning);
    }
}
