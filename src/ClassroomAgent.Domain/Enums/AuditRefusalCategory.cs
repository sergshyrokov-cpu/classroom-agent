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

    /// <summary>US-009: the domain to be written differs from the <c>Installation</c> domain (BR-020).</summary>
    DomainMismatch,

    /// <summary>US-009: the technical account's email domain differs from the <c>Installation</c> domain (BR-020).</summary>
    ImpersonationDomainMismatch,

    /// <summary>US-009: no legitimacy check has ever succeeded, so the allowed domain is unknown.</summary>
    DomainNotConfirmed,

    /// <summary>US-009: the installation is in read-only mode, where connection settings may not be saved (BR-026).</summary>
    ReadOnlyMode,
}
