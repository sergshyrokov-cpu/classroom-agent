using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The first real Google port (US-011 spec FR-004): the eight steps of "Проверить доступ" and of the startup
/// self-check. Marked <see cref="IGoogleDataPort"/>, so a use case holding it must take the read-only guard and
/// call it first (US-007 FR-007). No Google SDK type crosses it (AD-4).
/// </summary>
/// <remarks>
/// Every method answers with an outcome of the closed list of spec FR-005 and never throws for an answer Google
/// gave; cancellation is the only exception it lets through. Created as a compile-only skeleton at TEST_WRITING
/// (US-011 OD-006).
/// </remarks>
public interface IGoogleAccessProbe : IGoogleDataPort
{
    /// <summary>
    /// Requests a delegated token for exactly one <paramref name="scope"/>, impersonating
    /// <paramref name="technicalAccount"/> (spec FR-002).
    /// </summary>
    Task<DelegationAttempt> RequestDelegatedTokenAsync(
        string technicalAccount,
        string scope,
        CancellationToken cancellationToken);

    /// <summary>The Classroom read: the course list, at most one course (spec FR-003, I-3).</summary>
    Task<AccessCheckStepOutcome> ReadCoursesAsync(DelegatedToken token, CancellationToken cancellationToken);

    /// <summary>The Admin Reports read: Meet activity for all users, at most one event (spec FR-003, I-3).</summary>
    Task<AccessCheckStepOutcome> ReadMeetActivityAsync(DelegatedToken token, CancellationToken cancellationToken);
}
