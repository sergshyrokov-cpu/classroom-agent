namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// The revoke confirmation page (US-003 api-design §6). <see cref="LeavesFewerThanTwo"/> is true
/// while the installation has two entries or fewer (spec I-10).
/// </summary>
public sealed record RevokeAllowedAdminConfirmationDto(
    Guid InstallationIdentifier,
    string InstallationName,
    Guid AdminIdentifier,
    string Email,
    bool LeavesFewerThanTwo);
