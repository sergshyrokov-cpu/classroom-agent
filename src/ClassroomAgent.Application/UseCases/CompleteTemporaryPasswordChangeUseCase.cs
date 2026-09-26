using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The forced change of a temporary password (US-012 spec FR-006). The new password obeys the policy and must
/// not equal the temporary one, which is checked by verifying it against the hash still stored — no plaintext is
/// kept anywhere to make that comparison (spec VR-003, S-10). Permitted in read-only mode (BR-026).
/// </summary>
/// <remarks>US-012 TEST_WRITING skeleton (OD-005) — IMPLEMENTATION writes the body.</remarks>
public sealed class CompleteTemporaryPasswordChangeUseCase
{
    /// <summary>US-012 TEST_WRITING skeleton (OD-005): IMPLEMENTATION turns this into a primary constructor
    /// holding the dependencies. They are listed here so the tests construct the type exactly as it will be.</summary>
    public CompleteTemporaryPasswordChangeUseCase(
        IAppUserRepository users,
        IAuditEventRepository auditEvents,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ServiceWriteScope writeScope,
        TimeProvider timeProvider)
    {
    }

    public Task<PasswordChangeOutcome> ExecuteAsync(
        long deanId,
        string newPassword,
        string? requestId,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException("US-012 IMPLEMENTATION (spec FR-006).");
}
