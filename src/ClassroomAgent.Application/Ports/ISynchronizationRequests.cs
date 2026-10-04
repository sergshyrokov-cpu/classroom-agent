using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// US-019 spec FR-002: the out-of-schedule synchronization request, seen from <c>Application</c>. The adapter wraps
/// the host's <c>SyncRunCoordinator</c>; a request is always accepted (US-013 OD-007, US-037 API design §3).
/// </summary>
public interface ISynchronizationRequests
{
    /// <summary>Requests a run and says whether other work was in progress at that moment.</summary>
    Task<SynchronizationRequestTiming> RequestAsync(CancellationToken cancellationToken);
}
