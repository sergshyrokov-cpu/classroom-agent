namespace ClassroomAgent.Application.Models;

/// <summary>Why a check was refused before any call (US-011 spec FR-006 step 2, I-11; api-design §2.4).</summary>
public enum AccessCheckRefusal
{
    /// <summary>No connection has been saved.</summary>
    NotConfigured,

    /// <summary>The saved connection is for a domain that is no longer the <c>Installation</c> domain (US-009 OD-002).</summary>
    DomainMismatch,
}
