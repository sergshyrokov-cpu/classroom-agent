using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The Control Plane channel (AD-4; US-005 api-design §6, US-008 api-design §2.1). One port for one external
/// system: the legitimacy check and the Admin login check share the base address, the registration, the timeout
/// and the classification of an HTTP outcome into a typed reply, so a second port would only invite the two
/// copies to drift (US-008 spec I-7).
/// </summary>
public interface IControlPlaneClient
{
    Task<ControlPlaneCheckReply> CheckAsync(
        Guid installationId,
        string applicationVersion,
        int contractVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asks whether <paramref name="email"/> is an approved Admin of this installation (US-008 spec FR-008).
    /// Asked on every sign-in; nothing is cached and no earlier answer is reused (SC-3, BR-012).
    /// </summary>
    Task<AdminLoginCheckReply> CheckAdminLoginAsync(
        Guid installationId,
        string email,
        CancellationToken cancellationToken);
}
