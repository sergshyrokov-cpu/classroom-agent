namespace ClassroomAgent.Application.Models;

/// <summary>
/// How one step of the access check ended — the closed list of US-011 spec FR-005.
/// </summary>
/// <remarks>Compile-only skeleton created at TEST_WRITING under US-011 OD-006.</remarks>
public enum AccessCheckStepOutcome
{
    Succeeded,
    ScopeNotAuthorized,
    TechnicalAccountUnknown,
    TechnicalAccountCannotRead,
    ApiNotEnabled,
    KeyUnavailable,
    KeyRejected,
    GoogleUnavailable,
    NotAttempted,
}
