using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The forced change of a temporary password (US-012 spec FR-006). The new password obeys the policy and must
/// not equal the temporary one, which is checked by verifying it against the hash still stored — no plaintext is
/// kept anywhere to make that comparison (spec VR-003, S-10). Permitted in read-only mode (BR-026).
/// </summary>
public sealed class CompleteTemporaryPasswordChangeUseCase(
    IAppUserRepository users,
    IAuditEventRepository auditEvents,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    public async Task<PasswordChangeOutcome> ExecuteAsync(
        long deanId,
        string newPassword,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newPassword);

        var dean = await users.FindDeanByIdAsync(deanId, cancellationToken);
        if (dean is null || dean.PasswordHash is not { } hash)
        {
            return new PasswordChangeOutcome(true, false, null);
        }

        var violation = DeanPasswordPolicy.Check(newPassword, dean.Email);
        if (violation is { } broken)
        {
            return new PasswordChangeOutcome(true, false, broken);
        }

        // The new password may not equal the temporary one it replaces (BR-014 v64, spec VR-003).
        if (passwordHasher.Verify(hash, newPassword))
        {
            return new PasswordChangeOutcome(true, false, PasswordPolicyViolation.EqualsTemporaryPassword);
        }

        var now = timeProvider.GetUtcNow();
        dean.SetOwnPassword(passwordHasher.Hash(newPassword), now);
        auditEvents.Add(AuditEvent.DeanPasswordChanged(dean.Id, now, requestId));
        using (writeScope.Declare(PermittedServiceWrite.SignInBookkeeping))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new PasswordChangeOutcome(false, false, null);
    }
}
