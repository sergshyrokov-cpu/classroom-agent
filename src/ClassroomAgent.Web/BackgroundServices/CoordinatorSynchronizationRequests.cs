using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Web.BackgroundServices;

/// <summary>
/// US-019 api-design §2.7: <see cref="ISynchronizationRequests"/> over <see cref="SyncRunCoordinator"/>. It lives in
/// the host next to the coordinator — an in-process seam, not a Google port.
/// </summary>
/// <remarks>
/// Whether other work is in progress is read immediately before the request (spec I-1): the answer is true of the
/// moment of the press, and the request itself is always accepted (US-013 OD-007, US-037 API design §3).
/// </remarks>
public sealed class CoordinatorSynchronizationRequests(SyncRunCoordinator coordinator) : ISynchronizationRequests
{
    public Task<SynchronizationRequestTiming> RequestAsync(CancellationToken cancellationToken)
    {
        var busy = coordinator.IsRunning || coordinator.IsPurging;
        coordinator.Request();
        return Task.FromResult(busy ? SynchronizationRequestTiming.AfterCurrentWork : SynchronizationRequestTiming.StartsNow);
    }
}
