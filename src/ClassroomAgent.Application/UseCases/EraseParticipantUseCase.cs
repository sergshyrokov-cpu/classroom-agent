using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-038 spec FR-004 … FR-008, FR-013, FR-014: an Admin erases all data of one participant. Order: the read-only guard
/// (a refusal is audited under the audit service write and rethrown), the mark, then one transaction — the exclusive
/// fence, the participant, the deletes and clearings, the salt, the exclusion and the audit row. The host writes the
/// log line from the returned counts after commit.
/// </summary>
public sealed class EraseParticipantUseCase(
    IReadOnlyModeGuard readOnlyMode,
    IParticipantErasureReadSource reads,
    IParticipantErasureStore store,
    IErasureFence fence,
    IErasureSaltStore salts,
    IErasedParticipantRepository exclusions,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    /// <summary>The operation name the read-only guard records; a constant, never user data (SC-10).</summary>
    public const string Operation = "Participant.Erase";

    public async Task<ParticipantErasureResult> ExecuteAsync(
        long adminId,
        long participantId,
        bool irreversibleConfirmed,
        string? requestId,
        CancellationToken cancellationToken)
    {
        // 1: read-only mode, before the mark and before any read or transaction (AD-6, spec FR-014, api-design §2.4).
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            await AuditRefusalAsync(adminId, participantId, requestId, cancellationToken);
            throw;
        }

        // 2: the mark (spec VR-002); nothing is read or written without it.
        if (!irreversibleConfirmed)
        {
            return new ParticipantErasureResult(ParticipantErasureOutcome.IrreversibleMarkRequired, null);
        }

        // 3, 4: one transaction (spec FR-004, db-design §7.1).
        ErasureCounts? committed = null;
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async token => committed = await EraseInTransactionAsync(adminId, participantId, requestId, token),
                cancellationToken);
        }
        catch (ParticipantGoneException)
        {
            // Spec FR-016: never existed, or erased meanwhile — rolled back, nothing changed.
            return new ParticipantErasureResult(ParticipantErasureOutcome.ParticipantNotFound, null);
        }

        return new ParticipantErasureResult(ParticipantErasureOutcome.Erased, committed);
    }

    private async Task<ErasureCounts> EraseInTransactionAsync(
        long adminId,
        long participantId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        // The fence first, so no synchronization or Meet page can store the person between this read and the commit
        // (spec FR-011, db-design §7.3); the participant is read after it.
        await fence.EnterAsErasureAsync(cancellationToken);

        var target = await reads.GetTargetAsync(participantId, cancellationToken)
            ?? throw new ParticipantGoneException();
        var normalizedEmail = string.IsNullOrWhiteSpace(target.Email) ? null : WorkspaceConnection.NormalizeEmail(target.Email);

        var counts = await store.EraseAsync(target.ParticipantId, target.GoogleUserId, normalizedEmail, cancellationToken)
            ?? throw new ParticipantGoneException();

        var now = timeProvider.GetUtcNow();
        EmailFingerprint? fingerprint = null;
        if (normalizedEmail is not null)
        {
            var salt = await salts.GetOrCreateAsync(cancellationToken);
            fingerprint = EmailFingerprint.Of(normalizedEmail, salt);
        }

        exclusions.Add(ErasedParticipant.Record(target.GoogleUserId, fingerprint, now));
        auditEvents.Add(AuditEvent.ParticipantErased(adminId, target.ParticipantId, counts, now, requestId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return counts;
    }

    /// <summary>
    /// One audit row and nothing else, declared as <see cref="PermittedServiceWrite.AuditEvent"/> — on the BR-026 closed
    /// list — so the refusal is recorded in read-only mode too (spec I-12, db-design §9.4).
    /// </summary>
    private async Task AuditRefusalAsync(
        long adminId,
        long participantId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        auditEvents.Add(AuditEvent.ErasureRefused(
            AuditAction.ParticipantErased,
            adminId,
            participantId,
            AuditRefusalCategory.ReadOnlyMode,
            timeProvider.GetUtcNow(),
            requestId));
        using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Leaves the transaction so that it rolls back when the participant row is absent; caught by
    /// <see cref="ExecuteAsync"/> and answered as not found — it never leaves this class.
    /// </summary>
    private sealed class ParticipantGoneException : Exception
    {
    }
}
