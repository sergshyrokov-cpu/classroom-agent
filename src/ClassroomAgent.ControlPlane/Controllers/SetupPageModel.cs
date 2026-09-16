namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// View model of the setup page. Carries only the login to refill — never the code or
/// either password (FR-004) — and whether the account already exists (OD-003).
/// </summary>
public sealed record SetupPageModel(string? Login, bool AlreadyCreated);
