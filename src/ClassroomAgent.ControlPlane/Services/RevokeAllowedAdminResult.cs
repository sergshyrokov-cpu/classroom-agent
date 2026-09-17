namespace ClassroomAgent.ControlPlane.Services;

/// <summary>Result of <see cref="AllowedAdminRegistry.RevokeAsync"/> (US-003 entity model §4).</summary>
public enum RevokeAllowedAdminResult
{
    Revoked,
    NotFound,
}
