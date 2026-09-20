namespace ClassroomAgent.Application.Models;

/// <summary>
/// The result of saving the connection (US-009 spec FR-006). A refusal is an expected outcome returned as
/// data, never an exception (AD-9); only the read-only refusal is thrown, because US-007 already throws it.
/// </summary>
/// <param name="Refusal">Null when the connection was saved.</param>
public sealed record SaveWorkspaceConnectionOutcome(SaveWorkspaceConnectionRefusal? Refusal)
{
    public static SaveWorkspaceConnectionOutcome Saved { get; } = new((SaveWorkspaceConnectionRefusal?)null);

    public static SaveWorkspaceConnectionOutcome Refused(SaveWorkspaceConnectionRefusal refusal) => new(refusal);

    public bool IsSaved => Refusal is null;
}

/// <summary>Why a well-formed save was refused by the installation's state (US-009 api-design §2.4).</summary>
public enum SaveWorkspaceConnectionRefusal
{
    /// <summary>The domain to be written is not the <c>Installation</c> domain (BR-020).</summary>
    DomainMismatch,

    /// <summary>The technical account is outside the <c>Installation</c> domain (BR-020).</summary>
    ImpersonationDomainMismatch,

    /// <summary>No legitimacy check has ever succeeded, so the allowed domain is unknown.</summary>
    DomainNotConfirmed,
}
