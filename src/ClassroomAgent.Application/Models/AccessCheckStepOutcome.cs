namespace ClassroomAgent.Application.Models;

/// <summary>
/// How one step of the access check ended — the closed list of US-011 spec FR-005. The Google adapter maps every
/// answer onto it; an answer it cannot classify is <see cref="GoogleUnavailable"/>, never a configuration diagnosis.
/// </summary>
public enum AccessCheckStepOutcome
{
    /// <summary>The token was issued, or the read answered.</summary>
    Succeeded,

    /// <summary>The scope is not authorised in domain-wide delegation for this client ID — the super-admin acts.</summary>
    ScopeNotAuthorized,

    /// <summary>The impersonated address does not exist or cannot be impersonated — run-wide.</summary>
    TechnicalAccountUnknown,

    /// <summary>The token was issued but the API refuses the read for this account.</summary>
    TechnicalAccountCannotRead,

    /// <summary>The API is not enabled in the Owner's Cloud project — the Owner acts.</summary>
    ApiNotEnabled,

    /// <summary>No reference, nothing under it, or not a usable service-account key — run-wide, the Owner acts.</summary>
    KeyUnavailable,

    /// <summary>Google rejects the key's signature — run-wide, the Owner acts.</summary>
    KeyRejected,

    /// <summary>No answer in time, a network failure, a 5xx or 429, or an answer that cannot be classified.</summary>
    GoogleUnavailable,

    /// <summary>An earlier step made this one impossible.</summary>
    NotAttempted,
}
