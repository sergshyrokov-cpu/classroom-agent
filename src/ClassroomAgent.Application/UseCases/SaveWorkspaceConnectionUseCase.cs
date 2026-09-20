using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.Validation;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Saving the school's technical account (US-009 spec FR-006): the second of the Owner's three points of
/// control. The order is fixed here and testable without a browser — read-only guard, the allowed domain, the
/// impersonation domain, the domain to be written, then one transaction that stores the connection and its
/// audit row together.
/// </summary>
/// <remarks>
/// The use case takes <see cref="IReadOnlyModeGuard"/>, so saving is a guarded write and **not** a BR-026
/// service write: <c>PermittedServiceWrite</c> gains no member and this type is not registered in
/// <c>PermittedServiceWrites</c> (spec FR-008). The audit row of a read-only refusal is committed by declaring
/// <see cref="PermittedServiceWrite.AuditEvent"/> around that one commit, which is on the BR-026 list already
/// (spec I-8); the connection itself stays unwritten and the refusal continues to the handler.
/// <para>No Google call happens anywhere here: the account is recorded, not verified (US-011, spec S-10).</para>
/// </remarks>
public sealed class SaveWorkspaceConnectionUseCase(
    IWorkspaceConnectionRepository connections,
    IAuditEventRepository auditEvents,
    GetWorkspaceConnectionQuery connectionQuery,
    IReadOnlyModeGuard readOnlyMode,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    /// <summary>The operation name the read-only refusal carries (US-007 spec VR-001: a constant, never data).</summary>
    public const string Operation = "WorkspaceConnection.Save";

    public async Task<SaveWorkspaceConnectionOutcome> ExecuteAsync(
        long actorId,
        string impersonationUserEmail,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(impersonationUserEmail);

        // 1: read-only mode first, before any repository, port or transaction (US-007 FR-002, AD-6). The refusal
        // is audited and then rethrown, so the Admin sees the 409 US-008 maps and the attempt still leaves a trace.
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            await AuditRefusalAsync(actorId, AuditRefusalCategory.ReadOnlyMode, requestId, cancellationToken);
            throw;
        }

        var email = WorkspaceConnection.NormalizeEmail(impersonationUserEmail);

        // 2: the domain the Owner approved. Without one nothing may be saved, whatever the Admin typed.
        var domain = await connectionQuery.KnownDomainAsync(cancellationToken);
        if (domain is null)
        {
            return await RefuseAsync(
                actorId,
                SaveWorkspaceConnectionRefusal.DomainNotConfirmed,
                AuditRefusalCategory.DomainNotConfirmed,
                requestId,
                cancellationToken);
        }

        // 3: the technical account must belong to that domain (BR-020) — the field a person can get wrong.
        if (!DomainComparison.BelongsTo(email, domain))
        {
            return await RefuseAsync(
                actorId,
                SaveWorkspaceConnectionRefusal.ImpersonationDomainMismatch,
                AuditRefusalCategory.ImpersonationDomainMismatch,
                requestId,
                cancellationToken);
        }

        // 4: and the domain about to be written must be a domain at all. The value comes from LegitimacyState
        // (OD-001), so the form cannot break this — a hand-edited database can, and then the save is refused
        // rather than writing a row no rule would accept (spec VR-004; BR-020 stays a control, not a form rule).
        var domainToWrite = WorkspaceConnection.NormalizeDomain(domain);
        if (!WorkspaceDomainAttribute.IsDomain(domainToWrite))
        {
            return await RefuseAsync(
                actorId,
                SaveWorkspaceConnectionRefusal.DomainMismatch,
                AuditRefusalCategory.DomainMismatch,
                requestId,
                cancellationToken);
        }

        await StoreAsync(actorId, domainToWrite, email, requestId, cancellationToken);
        return SaveWorkspaceConnectionOutcome.Saved;
    }

    /// <summary>
    /// The connection and its audit row in one transaction, so a saved connection without its row is
    /// impossible and the row carries the identity the insert generated (db-design §3.3).
    /// </summary>
    private async Task StoreAsync(
        long actorId,
        string domain,
        string email,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var existing = await connections.GetForUpdateAsync(token);
                if (existing is null)
                {
                    var created = WorkspaceConnection.Create(domain, email);
                    connections.Add(created);
                    await unitOfWork.SaveChangesAsync(token);
                    existing = created;
                }
                else
                {
                    existing.ChangeTo(domain, email);
                    await unitOfWork.SaveChangesAsync(token);
                }

                auditEvents.Add(AuditEvent.WorkspaceConnectionSaved(actorId, existing.Id, now, requestId));
                using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
                {
                    await unitOfWork.SaveChangesAsync(token);
                }
            },
            cancellationToken);
    }

    private async Task<SaveWorkspaceConnectionOutcome> RefuseAsync(
        long actorId,
        SaveWorkspaceConnectionRefusal refusal,
        AuditRefusalCategory category,
        string? requestId,
        CancellationToken cancellationToken)
    {
        await AuditRefusalAsync(actorId, category, requestId, cancellationToken);
        return SaveWorkspaceConnectionOutcome.Refused(refusal);
    }

    /// <summary>
    /// One audit row, and nothing else. The declaration is <see cref="PermittedServiceWrite.AuditEvent"/>,
    /// already on the BR-026 closed list, so the row is written in read-only mode too (spec I-8).
    /// </summary>
    private async Task AuditRefusalAsync(
        long actorId,
        AuditRefusalCategory category,
        string? requestId,
        CancellationToken cancellationToken)
    {
        auditEvents.Add(AuditEvent.WorkspaceConnectionSaveRefused(
            actorId,
            category,
            timeProvider.GetUtcNow(),
            requestId));
        using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
