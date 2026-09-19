namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>View model of the push address form: the installation's identifier and the value to show as typed.</summary>
public sealed record ChangeInstallationPushAddressPageModel(Guid Identifier, string? PushAddress);
