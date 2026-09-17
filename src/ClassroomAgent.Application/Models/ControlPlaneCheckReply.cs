using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// What <see cref="Ports.IControlPlaneClient"/> reports: an answer or a failure category (US-005 api-design §11).
/// Compile-only skeleton created at TEST_WRITING (OD-002); IMPLEMENTATION owns it.
/// </summary>
public abstract record ControlPlaneCheckReply
{
    private ControlPlaneCheckReply()
    {
    }

    public sealed record Answer(InstallationStatus Status, CompatibilityState Compatibility, string Domain, string ClientId)
        : ControlPlaneCheckReply;

    public sealed record Failure(CheckFailureCategory Category) : ControlPlaneCheckReply;
}
