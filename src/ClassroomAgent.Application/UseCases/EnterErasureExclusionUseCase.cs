using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-065 spec FR-002 … FR-005, FR-011 … FR-013, VR-001: an Admin enters an exclusion by email for a person not loaded.
/// Order: the read-only guard (a refusal is audited and rethrown), the email, then one transaction — the exclusive
/// erasure fence, the loaded check, the salt (created if absent), the existing-exclusion check, the exclusion and its
/// audit row. "Already loaded" and "already excluded" roll back and write nothing.
/// </summary>
public sealed class EnterErasureExclusionUseCase(
    IReadOnlyModeGuard readOnlyMode,
    IParticipantErasureReadSource participants,
    IErasureFence fence,
    IErasureSaltStore salts,
    IErasedParticipantRepository exclusions,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    /// <summary>The operation name the read-only guard records; a constant, never user data (SC-10).</summary>
    public const string Operation = "ErasureExclusion.Enter";

    /// <summary>US-065 api-design §2.5: the "already loaded" list is capped as the US-038 search is.</summary>
    public const int MaxLoadedResults = SearchParticipantsForErasureQuery.MaxResults;

    public async Task<ExclusionEntryResult> ExecuteAsync(
        long adminId,
        string? email,
        string? requestId,
        CancellationToken cancellationToken)
    {
        // 1: read-only mode, before validation (AD-6, spec FR-012, api-design §2.6).
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            await AuditRefusalAsync(adminId, requestId, cancellationToken);
            throw;
        }

        // 2: the email — the lifting's rule, one validation for both (spec VR-001).
        var trimmed = email?.Trim() ?? string.Empty;
        if (LiftErasureExclusionUseCase.Validate(trimmed) is { } error)
        {
            return new ExclusionEntryResult(ExclusionEntryOutcome.Invalid, error, [], false);
        }

        // 3: one transaction (spec FR-002 step 3, db-design §3).
        var normalized = WorkspaceConnection.NormalizeEmail(trimmed);
        ExclusionEntryResult? result = null;
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async token => result = await EnterInTransactionAsync(adminId, normalized, requestId, token),
                cancellationToken);
        }
        catch (NothingToEnterException nothing)
        {
            // Spec FR-004, FR-005: rolled back — the salt too, had it been created on the way (api contract x-writes).
            return nothing.Result;
        }

        return result!;
    }

    private async Task<ExclusionEntryResult> EnterInTransactionAsync(
        long adminId,
        string normalizedEmail,
        string? requestId,
        CancellationToken cancellationToken)
    {
        // The exclusive fence first, so no synchronization course or Meet page can store the person between the loaded
        // check and the commit, and a run waiting on it sees the exclusion at its write (spec FR-011, I-6).
        await fence.EnterAsErasureAsync(cancellationToken);

        // 3.1: a loaded person needs the ordinary erasure, so this is decided before the exclusion check (spec I-5).
        var loaded = await participants.FindParticipantsByEmailAsync(normalizedEmail, cancellationToken);
        if (loaded.Count > 0)
        {
            throw new NothingToEnterException(new ExclusionEntryResult(
                ExclusionEntryOutcome.AlreadyLoaded,
                null,
                loaded.Take(MaxLoadedResults).ToList(),
                loaded.Count > MaxLoadedResults));
        }

        // 3.2, 3.3: the salt, created here when no erasure ever happened (US-038 FR-010), then the fingerprint.
        var salt = await salts.GetOrCreateAsync(cancellationToken);
        var fingerprint = EmailFingerprint.Of(normalizedEmail, salt);
        if (await exclusions.AnyWithFingerprintAsync(fingerprint, cancellationToken))
        {
            throw new NothingToEnterException(new ExclusionEntryResult(ExclusionEntryOutcome.AlreadyExcluded, null, [], false));
        }

        // 3.4: the exclusion is saved first so its id exists for the audit row, all in the one transaction.
        var now = timeProvider.GetUtcNow();
        var exclusion = ErasedParticipant.EnterInAdvance(fingerprint, now);
        exclusions.Add(exclusion);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        auditEvents.Add(AuditEvent.ErasureExclusionEntered(adminId, exclusion.Id, now, requestId));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ExclusionEntryResult(ExclusionEntryOutcome.Entered, null, [], false);
    }

    /// <summary>
    /// One audit row and nothing else, declared as <see cref="PermittedServiceWrite.AuditEvent"/> — on the BR-026 closed
    /// list — so the refusal is recorded in read-only mode too (spec FR-012, FR-013: no target).
    /// </summary>
    private async Task AuditRefusalAsync(long adminId, string? requestId, CancellationToken cancellationToken)
    {
        auditEvents.Add(AuditEvent.ErasureRefused(
            AuditAction.ErasureExclusionEntered,
            adminId,
            null,
            AuditRefusalCategory.ReadOnlyMode,
            timeProvider.GetUtcNow(),
            requestId));
        using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Leaves the transaction so that it rolls back when there is nothing to enter; caught by <see cref="ExecuteAsync"/>
    /// and answered with its result — it never leaves this class.
    /// </summary>
    private sealed class NothingToEnterException(ExclusionEntryResult result) : Exception
    {
        public ExclusionEntryResult Result { get; } = result;
    }
}
