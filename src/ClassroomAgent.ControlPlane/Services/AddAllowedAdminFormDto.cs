namespace ClassroomAgent.ControlPlane.Services;

/// <summary>The installation shown on the add-Admin form (US-003 api-design §6).</summary>
public sealed record AddAllowedAdminFormDto(Guid InstallationIdentifier, string InstallationName, string InstallationDomain);
