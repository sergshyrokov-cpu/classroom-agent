namespace ClassroomAgent.Application.Models;

/// <summary>
/// The state of the super-admin instruction (US-010 spec FR-002). Exactly two members: read-only mode is
/// **not** one of them — an installation can be read-only and its instruction complete, and the two are
/// reported independently (api-design §2.3, §2.4).
/// </summary>
public enum ConnectionInstructionState
{
    /// <summary>A legitimacy check has succeeded and both the domain and the client ID are known.</summary>
    Complete,

    /// <summary>
    /// No check has ever succeeded, or a stored value is empty. The school-specific parts are replaced by a
    /// statement; the scope list and the technical-account requirements stay complete (spec AC-008).
    /// </summary>
    InstallationNotConfirmed,
}
