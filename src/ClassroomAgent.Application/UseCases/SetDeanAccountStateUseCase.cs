using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// An Admin disables or re-enables a Dean account (US-012 spec FR-007, FR-008). One use case for both
/// directions, as the contract has one operation; the audit row still names which it was (db-design §4.1).
/// </summary>
public sealed class SetDeanAccountStateUseCase(
    IReadOnlyModeGuard readOnlyMode,
    IAppUserRepository users,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    public const string Operation = "DeanAccount.SetState";

    /// <param name="disable">True to disable the account, false to re-enable it.</param>
    public async Task<DeanAccountActionOutcome> ExecuteAsync(
        long adminId,
        long deanId,
        bool disable,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var action = disable ? AuditAction.DeanAccountDisabled : AuditAction.DeanAccountReEnabled;

        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            await AuditRefusalAsync(adminId, action, deanId, requestId, cancellationToken);
            throw;
        }

        // An id that is not a Dean and an id that matches nothing are one answer (spec VR-005, I-8).
        var dean = await users.FindDeanByIdAsync(deanId, cancellationToken);
        if (dean is null)
        {
            return new DeanAccountActionOutcome(DeanAccountRefusal.NoSuchDeanAccount, null, null);
        }

        if (dean.IsDisabled == disable)
        {
            return new DeanAccountActionOutcome(DeanAccountRefusal.AlreadyInThatState, null, dean.Id);
        }

        var now = timeProvider.GetUtcNow();
        if (disable)
        {
            dean.Disable(now);
        }
        else
        {
            dean.ReEnable(now);
        }

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                auditEvents.Add(AuditEvent.DeanAccountManaged(adminId, action, dean.Id, now, requestId));
                await unitOfWork.SaveChangesAsync(token);
            },
            cancellationToken);

        return new DeanAccountActionOutcome(null, null, dean.Id);
    }

    private async Task AuditRefusalAsync(
        long adminId,
        AuditAction action,
        long deanId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        auditEvents.Add(AuditEvent.DeanAccountManagementRefused(
            adminId,
            action,
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
