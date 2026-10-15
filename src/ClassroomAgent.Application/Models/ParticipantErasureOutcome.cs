namespace ClassroomAgent.Application.Models;

/// <summary>US-038 spec FR-004, FR-016: how an erasure request ended (a read-only refusal is an exception, AD-6).</summary>
public enum ParticipantErasureOutcome
{
    Erased,

    /// <summary>VR-002: the "erasure is irreversible" mark was not set; nothing changed.</summary>
    IrreversibleMarkRequired,

    /// <summary>FR-016: no participant with that id — never existed or erased meanwhile; nothing changed.</summary>
    ParticipantNotFound,
}
