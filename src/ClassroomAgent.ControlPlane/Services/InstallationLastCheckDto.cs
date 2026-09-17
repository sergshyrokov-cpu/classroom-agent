using ClassroomAgent.ControlPlane.Persistence;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>The last legitimacy check shown on the installation detail page (US-005 api-design §9, §11).</summary>
public sealed record InstallationLastCheckDto(
    DateTimeOffset AnsweredAt,
    string ApplicationVersion,
    int ContractVersion,
    InstallationStatus Status,
    CompatibilityState Compatibility);
