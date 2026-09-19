namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>View model of the registration form: the values to refill as typed (VR-004; US-006 FR-002).</summary>
public sealed record RegisterInstallationPageModel(string? Name, string? Domain, string? ClientId, string? PushAddress);
