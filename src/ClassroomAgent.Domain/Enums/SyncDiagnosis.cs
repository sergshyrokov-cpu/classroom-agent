namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// Why the last synchronization failed — the closed list of US-017 spec FR-006. The first six mirror the access
/// check's configuration outcomes of the same names (OD-006); the domain does not reference that Application type
/// (AD-3). Stored in <c>sync_state.last_error</c> by name (US-017 db-design §2).
/// </summary>
public enum SyncDiagnosis
{
    ScopeNotAuthorized,
    TechnicalAccountUnknown,
    TechnicalAccountCannotRead,
    ApiNotEnabled,
    KeyUnavailable,
    KeyRejected,
    GoogleUnavailable,
    Unexpected,
}
