using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// A Dean changes their own password (US-012 spec FR-014). It succeeds only when the current password verifies
/// and the new one obeys the policy; no rule forbids a new password equal to the current one, and none is added
/// (spec I-7). Permitted in read-only mode (BR-026), and a wrong current password is not a failed sign-in
/// attempt — it moves no counter.
/// </summary>
public sealed class ChangeOwnPasswordUseCase(
    IAppUserRepository users,
    IAuditEventRepository auditEvents,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    public async Task<PasswordChangeOutcome> ExecuteAsync(
        long deanId,
        string currentPassword,
        string newPassword,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentPassword);
        ArgumentNullException.ThrowIfNull(newPassword);

        var dean = await users.FindDeanByIdAsync(deanId, cancellationToken);
        if (dean is null || dean.PasswordHash is not { } hash)
        {
            return new PasswordChangeOutcome(true, false, null);
        }

        if (!passwordHasher.Verify(hash, currentPassword))
        {
            return new PasswordChangeOutcome(true, true, null);
        }

        var violation = DeanPasswordPolicy.Check(newPassword, dean.Email);
        if (violation is { } broken)
        {
            return new PasswordChangeOutcome(true, false, broken);
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
