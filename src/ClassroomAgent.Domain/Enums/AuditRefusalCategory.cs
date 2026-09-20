namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// Why a sign-in was refused (US-008 spec FR-012; db-design §4.1). The email entered is never recorded, so the
/// category is the whole explanation an audit row carries.
/// </summary>
public enum AuditRefusalCategory
{
    /// <summary>The Control Plane answered that the email is not an approved Admin of this installation.</summary>
    NotInAllowedAdmin,

    /// <summary>The Control Plane could not be asked: unreachable, timed out, an error answer, or unparseable.</summary>
    ControlPlaneUnavailable,

    /// <summary>The Control Plane does not know this installation id.</summary>
    UnknownInstallation,

    /// <summary>The OAuth callback itself failed: state, correlation cookie, or the email Google returned.</summary>
    CallbackFailed,

    /// <summary>The account exists but is disabled (spec I-9; only US-012 disables one).</summary>
    AccountDisabled,
}
