namespace ClassroomAgent.Application.Models;

/// <summary>
/// Why a sign-in was refused, as data (US-008 spec FR-010; AD-6). It carries no user-visible string: presentation
/// resolves the text from the translation files (NFR-073).
/// </summary>
public enum SignInRefusal
{
    /// <summary>The email is not an approved Admin of this installation.</summary>
    NotApproved,

    /// <summary>The Control Plane could not be asked, or does not know this installation.</summary>
    CouldNotConfirm,

    /// <summary>The OAuth callback itself failed before any identity could be trusted.</summary>
    SignInFailed,

    /// <summary>The account exists but is disabled (spec I-9).</summary>
    AccountDisabled,
}
