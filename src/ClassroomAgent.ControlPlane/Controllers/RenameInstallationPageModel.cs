namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>View model of the name correction form: the installation's identifier and the name to show.</summary>
public sealed record RenameInstallationPageModel(Guid Identifier, string? Name);
