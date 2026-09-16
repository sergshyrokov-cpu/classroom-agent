namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// What a successful setup or sign-in hands to the session cookie (entity model §5).
/// Never carries the login, password hash or lockout state; the security stamp goes only
/// into the encrypted cookie.
/// </summary>
public sealed record OwnerSessionDto(long OwnerId, string UiLanguage, string SecurityStamp);
