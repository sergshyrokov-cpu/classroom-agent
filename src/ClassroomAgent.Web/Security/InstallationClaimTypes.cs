namespace ClassroomAgent.Web.Security;

/// <summary>Claims of the installation session principal besides the name identifier, email and role (US-008 api-design §4).</summary>
public static class InstallationClaimTypes
{
    public const string UiLanguage = "ca:ui_language";

    public const string SignedInAt = "ca:signed_in_at";

    /// <summary>Google's <c>email_verified</c> flag, carried from the callback to the decision (VR-005, spec I-6).</summary>
    public const string EmailVerified = "ca:email_verified";

    /// <summary>
    /// The account's security stamp, compared with the stored one on every request so a signed-out cookie stops
    /// authenticating even when someone kept a copy of it (US-008 AC-014; the Owner session of US-001 does the same).
    /// </summary>
    public const string SecurityStamp = "ca:security_stamp";
}
