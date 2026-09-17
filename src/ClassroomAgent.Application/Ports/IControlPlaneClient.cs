using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>The legitimacy check call to the Control Plane (AD-4; US-005 api-design §6).</summary>
public interface IControlPlaneClient
{
    Task<ControlPlaneCheckReply> CheckAsync(
        Guid installationId,
        string applicationVersion,
        int contractVersion,
        CancellationToken cancellationToken);
}
