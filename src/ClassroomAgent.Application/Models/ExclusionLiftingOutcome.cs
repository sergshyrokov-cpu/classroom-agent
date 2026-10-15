namespace ClassroomAgent.Application.Models;

/// <summary>US-038 spec FR-009: how a lifting request ended (a read-only refusal is an exception, AD-6).</summary>
public enum ExclusionLiftingOutcome
{
    /// <summary>At least one exclusion was removed.</summary>
    Lifted,

    /// <summary>The email is valid but no exclusion matches it; nothing changed (spec §8: not an error).</summary>
    NoExclusionForEmail,

    /// <summary>VR-003 failed; the field error says how; nothing changed.</summary>
    InvalidEmail,
}
