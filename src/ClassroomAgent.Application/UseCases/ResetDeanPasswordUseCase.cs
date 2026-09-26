using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// An Admin resets a Dean's password to a new temporary one (US-012 spec FR-009). It clears the lockout and
/// rotates the security stamp, and it never re-enables a disabled account (spec S-08).
/// </summary>
/// <remarks>US-012 TEST_WRITING skeleton (OD-005) — IMPLEMENTATION writes the body.</remarks>
public sealed class ResetDeanPasswordUseCase
{
    /// <summary>US-012 TEST_WRITING skeleton (OD-005): IMPLEMENTATION turns this into a primary constructor
    /// holding the dependencies. They are listed here so the tests construct the type exactly as it will be.</summary>
    public ResetDeanPasswordUseCase(
        IReadOnlyModeGuard readOnlyMode,
        IAppUserRepository users,
        IAuditEventRepository auditEvents,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ServiceWriteScope writeScope,
        TimeProvider timeProvider)
    {
    }

    public const string Operation = "DeanAccount.ResetPassword";

    public Task<DeanAccountActionOutcome> ExecuteAsync(
        long adminId,
        long deanId,
        string temporaryPassword,
        string? requestId,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException("US-012 IMPLEMENTATION (spec FR-009).");
}
