using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// An Admin resets a Dean's password to a new temporary one (US-012 spec FR-009). It clears the lockout and
/// rotates the security stamp, and it never re-enables a disabled account (spec S-08).
/// </summary>
public sealed class ResetDeanPasswordUseCase(
    IReadOnlyModeGuard readOnlyMode,
    IAppUserRepository users,
    IAuditEventRepository auditEvents,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    public const string Operation = "DeanAccount.ResetPassword";

    public async Task<DeanAccountActionOutcome> ExecuteAsync(
        long adminId,
        long deanId,
        string temporaryPassword,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(temporaryPassword);

        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            await AuditRefusalAsync(adminId, deanId, requestId, cancellationToken);
            throw;
        }

        var dean = await users.FindDeanByIdAsync(deanId, cancellationToken);
        if (dean is null)
        {
            return new DeanAccountActionOutcome(DeanAccountRefusal.NoSuchDeanAccount, null, null);
        }

        // The policy is checked against that account's own login (spec VR-002).
        var violation = DeanPasswordPolicy.Check(temporaryPassword, dean.Email);
        if (violation is { } broken)
        {
            return new DeanAccountActionOutcome(DeanAccountRefusal.PasswordPolicy, broken, dean.Id);
        }

        var now = timeProvider.GetUtcNow();
        dean.ResetPassword(passwordHasher.Hash(temporaryPassword), now);

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                auditEvents.Add(AuditEvent.DeanAccountManaged(
                    adminId,
                    AuditAction.DeanAccountPasswordReset,
                    dean.Id,
                    now,
                    requestId));
                await unitOfWork.SaveChangesAsync(token);
            },
            cancellationToken);

        return new DeanAccountActionOutcome(null, null, dean.Id);
    }

    private async Task AuditRefusalAsync(
        long adminId,
        long deanId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        auditEvents.Add(AuditEvent.DeanAccountManagementRefused(
            adminId,
            AuditAction.DeanAccountPasswordReset,
            deanId,
            AuditRefusalCategory.ReadOnlyMode,
            timeProvider.GetUtcNow(),
            requestId));
        using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
