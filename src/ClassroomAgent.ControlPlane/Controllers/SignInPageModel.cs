namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>View model of the sign-in page: the login to refill and whether to show the common refusal (FR-008).</summary>
public sealed record SignInPageModel(string? Login, bool Refused);
