using ClassroomAgent.ControlPlane.Persistence;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>The suspend and resume confirmation pages (US-004 api-design §7).</summary>
public sealed record InstallationStatusConfirmationDto(
    Guid Identifier,
    string Name,
    string Domain,
    InstallationStatus Status);
