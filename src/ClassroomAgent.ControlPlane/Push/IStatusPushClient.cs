namespace ClassroomAgent.ControlPlane.Push;

/// <summary>
/// One HTTP attempt to deliver a status-change push (US-006 entity model §4). The seam the Control Plane
/// tests substitute, so no test needs a real installation (TC-4).
/// </summary>
public interface IStatusPushClient
{
    /// <summary>Posts the installation id to the receiver under <paramref name="address"/> and classifies the answer.</summary>
    Task<StatusPushAttempt> SendAsync(string address, Guid identifier, CancellationToken cancellationToken);
}
