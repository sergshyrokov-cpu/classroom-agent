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

    /// <summary>
    /// US-011: the check was refused because the connection is not usable — none saved, or saved for a domain that
    /// is no longer the <c>Installation</c> domain (US-009 OD-002; db-design §3.2).
    /// </summary>
    ConnectionNotUsable,

    /// <summary>US-012 step 1 of the sign-in sequence: no account with that normalized email. The typed login is
    /// never recorded, so the category is the whole explanation (spec FR-012, SC-11).</summary>
    UnknownLogin,

    /// <summary>US-012 step 3: the password does not match the stored hash (spec FR-012).</summary>
    WrongPassword,

    /// <summary>US-012 step 2: a sign-in lockout is in force, so the password was not checked (spec FR-012, FR-013).</summary>
    LockedOut,
}
