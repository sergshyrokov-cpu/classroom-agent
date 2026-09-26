using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// A Dean changes their own password (US-012 spec FR-014). It succeeds only when the current password verifies
/// and the new one obeys the policy; no rule forbids a new password equal to the current one, and none is added
/// (spec I-7). Permitted in read-only mode (BR-026), and a wrong current password is not a failed sign-in
/// attempt — it moves no counter.
/// </summary>
/// <remarks>US-012 TEST_WRITING skeleton (OD-005) — IMPLEMENTATION writes the body.</remarks>
public sealed class ChangeOwnPasswordUseCase
{
    /// <summary>US-012 TEST_WRITING skeleton (OD-005): IMPLEMENTATION turns this into a primary constructor
    /// holding the dependencies. They are listed here so the tests construct the type exactly as it will be.</summary>
    public ChangeOwnPasswordUseCase(
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
        string currentPassword,
        string newPassword,
        string? requestId,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException("US-012 IMPLEMENTATION (spec FR-014).");
}
