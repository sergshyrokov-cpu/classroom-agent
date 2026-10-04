using ClassroomAgent.Application.Models;
using ClassroomAgent.Web.BackgroundServices;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-019 api-design §2.7: the adapter that turns a press into a request on <see cref="SyncRunCoordinator"/> and
/// answers whether other work was in progress (spec FR-002, FR-003, FR-004; AC-003, AC-004). Pure unit: no host,
/// no database.
/// </summary>
public sealed class CoordinatorSynchronizationRequestsTests
{
    /// <summary>AC-003: nothing in progress, so the request starts at once.</summary>
    [Fact]
    public async Task WhenIdle_AnswersStartsNow_AndTheRequestIsStartable()
    {
        var ct = TestContext.Current.CancellationToken;
        var coordinator = new SyncRunCoordinator();
        var requests = new CoordinatorSynchronizationRequests(coordinator);

        var timing = await requests.RequestAsync(ct);

        Assert.Equal(SynchronizationRequestTiming.StartsNow, timing);
        Assert.True(coordinator.IsRequested);
        Assert.True(coordinator.TryStartRequestedRun());
    }

    /// <summary>AC-003: a run is in progress; the request waits and starts when the run ends.</summary>
    [Fact]
    public async Task DuringARun_AnswersAfterCurrentWork_AndStartsWhenTheRunEnds()
    {
        var ct = TestContext.Current.CancellationToken;
        var coordinator = new SyncRunCoordinator();
        var requests = new CoordinatorSynchronizationRequests(coordinator);
        Assert.True(coordinator.TryStartScheduledRun());

        var timing = await requests.RequestAsync(ct);

        Assert.Equal(SynchronizationRequestTiming.AfterCurrentWork, timing);
        Assert.False(coordinator.TryStartRequestedRun());

        coordinator.RunCompleted();

        Assert.True(coordinator.IsRequested);
        Assert.True(coordinator.TryStartRequestedRun());
    }

    /// <summary>AC-003, US-037 FR-014: a purge counts as other work, and the request is remembered across it.</summary>
    [Fact]
    public async Task DuringAPurge_AnswersAfterCurrentWork_AndStartsWhenThePurgeEnds()
    {
        var ct = TestContext.Current.CancellationToken;
        var coordinator = new SyncRunCoordinator();
        var requests = new CoordinatorSynchronizationRequests(coordinator);
        Assert.True(coordinator.TryStartPurge());

        var timing = await requests.RequestAsync(ct);

        Assert.Equal(SynchronizationRequestTiming.AfterCurrentWork, timing);
        Assert.False(coordinator.TryStartRequestedRun());

        coordinator.PurgeCompleted();

        Assert.True(coordinator.IsRequested);
        Assert.True(coordinator.TryStartRequestedRun());
    }

    /// <summary>AC-004: however many requests arrive during a run, exactly one run follows it.</summary>
    [Fact]
    public async Task SeveralRequestsDuringARun_StartExactlyOneRunAfterIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var coordinator = new SyncRunCoordinator();
        var requests = new CoordinatorSynchronizationRequests(coordinator);
        Assert.True(coordinator.TryStartScheduledRun());

        await requests.RequestAsync(ct);
        await requests.RequestAsync(ct);
        await requests.RequestAsync(ct);
        coordinator.RunCompleted();

        Assert.True(coordinator.TryStartRequestedRun());
        coordinator.RunCompleted();
        Assert.False(coordinator.TryStartRequestedRun());
    }
}
