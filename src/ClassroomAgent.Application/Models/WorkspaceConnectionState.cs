namespace ClassroomAgent.Application.Models;

/// <summary>
/// The state of the installation's connection (US-009 spec FR-002). Every consumer reads this value instead
/// of deriving its own: the settings page now, synchronization (EPIC-1) and "check access" (US-011) later.
/// </summary>
public enum WorkspaceConnectionState
{
    /// <summary>No connection has ever been saved.</summary>
    NotConfigured,

    /// <summary>A connection is saved and its domain is the <c>Installation</c> domain.</summary>
    Configured,

    /// <summary>
    /// A connection is saved for a domain that is no longer the <c>Installation</c> domain (OD-002). BR-021
    /// says this cannot legitimately happen; when it does, the connection is reported and never used.
    /// </summary>
    DomainMismatch,

    /// <summary>No legitimacy check has ever succeeded, so no domain is known and no connection can be judged.</summary>
    DomainUnknown,
}
