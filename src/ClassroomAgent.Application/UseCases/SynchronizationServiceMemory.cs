namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Whether the synchronization background service is running (US-013 spec FR-014; DC-11), read by
/// <see cref="GetReadinessQuery"/>. It lives in Application, not Web, because Application cannot depend on Web
/// (AD-3) and the readiness query needs this state.
/// </summary>
/// <remarks>
/// The service marks itself running when its execution begins and stopped when it returns or throws, so a
/// crashed service is reported as an outage rather than as silence — readiness, not <c>SyncState</c>, is what
/// tells the Owner the service is down (spec I-3).
/// </remarks>
public sealed class SynchronizationServiceMemory
{
    private volatile bool _running;

    public bool IsRunning => _running;

    public void MarkRunning() => _running = true;

    public void MarkStopped() => _running = false;
}
