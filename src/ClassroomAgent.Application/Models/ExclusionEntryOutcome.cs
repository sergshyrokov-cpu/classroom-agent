namespace ClassroomAgent.Application.Models;

/// <summary>US-065 spec FR-002: how an entry request ended (a read-only refusal is an exception, AD-6).</summary>
public enum ExclusionEntryOutcome
{
    /// <summary>The exclusion was stored (spec FR-003).</summary>
    Entered,

    /// <summary>A stored participant has the email; nothing changed (spec FR-004).</summary>
    AlreadyLoaded,

    /// <summary>An exclusion has the email's fingerprint; nothing changed (spec FR-005).</summary>
    AlreadyExcluded,

    /// <summary>VR-001 failed; the field error says how; nothing changed.</summary>
    Invalid,
}
