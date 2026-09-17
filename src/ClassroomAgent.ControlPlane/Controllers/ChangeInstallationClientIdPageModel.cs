namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>View model of the client ID change form: the installation's identifier and the client ID to show.</summary>
public sealed record ChangeInstallationClientIdPageModel(Guid Identifier, string? ClientId);
