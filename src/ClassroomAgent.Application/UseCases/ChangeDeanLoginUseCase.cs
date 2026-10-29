using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// An Admin changes a Dean account's login to the Dean's renamed work email (US-067 spec FR-001 … FR-008). The
/// evaluation order is fixed (spec FR-002): read-only mode, the account, the address and the school's domain by
/// creation's own rules, "the same address", uniqueness. Only the login and the security stamp change.
/// </summary>
public sealed class ChangeDeanLoginUseCase(
    IReadOnlyModeGuard readOnlyMode,
    IAppUserRepository users,
    ILegitimacyStateRepository legitimacyStates,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    /// <summary>The operation name the read-only guard records; a constant, never user data (SC-10).</summary>
    public const string Operation = "DeanAccount.ChangeLogin";

    /// <param name="newEmail">The new address as entered; trimmed and checked here, never trusted.</param>
    public async Task<DeanAccountActionOutcome> ExecuteAsync(
        long adminId,
        long deanId,
        string newEmail,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newEmail);

        // 1: read-only mode, before any repository call (AD-6, BR-026; spec FR-008).
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            await AuditRefusalAsync(adminId, deanId, requestId, cancellationToken);
            throw;
        }

        // 2: the account — an id that is not a Dean (an Admin's included) and an unknown id are one answer.
        var dean = await users.FindDeanByIdAsync(deanId, cancellationToken);
        if (dean is null)
        {
            return Refused(DeanAccountRefusal.NoSuchDeanAccount, null);
        }

        // 3–4: the address and the school's domain, by creation's own rules (spec FR-002 "the same code").
        var trimmed = newEmail.Trim();
        if (!DeanLoginAddress.IsWellFormed(trimmed))
        {
            return Refused(DeanAccountRefusal.NotAnEmailAddress, dean.Id);
        }

        var state = await legitimacyStates.GetForReadAsync(cancellationToken);
        if (!DeanLoginAddress.IsInDomain(trimmed, DeanLoginAddress.SchoolDomainOf(state)))
        {
            return Refused(DeanAccountRefusal.OutsideSchoolDomain, dean.Id);
        }

        // 5: the account's own login — nothing is written, nothing rotates (spec FR-006, OD-002 a).
        var normalized = AppUser.Normalize(trimmed);
        if (string.Equals(normalized, dean.NormalizedEmail, StringComparison.Ordinal))
        {
            return Refused(DeanAccountRefusal.LoginUnchanged, dean.Id);
        }

        // 6: no other account of any role holds the address (spec FR-002 step 6).
        if (await users.FindByNormalizedEmailAsync(normalized, cancellationToken) is not null)
        {
            return Refused(DeanAccountRefusal.EmailAlreadyUsed, dean.Id);
        }

        var now = timeProvider.GetUtcNow();
        dean.ChangeLogin(normalized, now);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
                    auditEvents.Add(AuditEvent.DeanAccountManaged(
                        adminId,
                        AuditAction.DeanLoginChanged,
                        dean.Id,
                        now,
                        requestId));
                    await unitOfWork.SaveChangesAsync(token);
                },
                cancellationToken);
        }
        catch (UniqueEmailViolationException)
        {
            // Another account took the address after step 6; the transaction rolled back (spec FR-004).
            unitOfWork.DiscardUncommittedChanges();
            return Refused(DeanAccountRefusal.EmailAlreadyUsed, dean.Id);
        }

        return new DeanAccountActionOutcome(null, null, dean.Id);
    }

    private static DeanAccountActionOutcome Refused(DeanAccountRefusal refusal, long? deanId) => new(refusal, null, deanId);

    /// <summary>
    /// One audit row and nothing else, declared as <see cref="PermittedServiceWrite.AuditEvent"/> — on the BR-026
    /// closed list — so the refusal is recorded in read-only mode too (spec FR-008).
    /// </summary>
    private async Task AuditRefusalAsync(
        long adminId,
        long deanId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        auditEvents.Add(AuditEvent.DeanAccountManagementRefused(
            adminId,
            AuditAction.DeanLoginChanged,
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
