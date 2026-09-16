namespace ClassroomAgent.ControlPlane.Security;

/// <summary>Claims of the Owner session principal besides the name identifier and role (api-design §6).</summary>
public static class OwnerClaimTypes
{
    public const string UiLanguage = "cp:ui_language";

    public const string SignedInAt = "cp:signed_in_at";

    public const string SecurityStamp = "cp:security_stamp";
}
