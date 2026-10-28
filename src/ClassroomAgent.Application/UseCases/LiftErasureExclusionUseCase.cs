using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-038 spec FR-009, FR-013, FR-014, VR-003: an Admin lifts the exclusion(s) of one email. Order: the read-only guard
/// (a refusal is audited and rethrown), the email, its fingerprint, then one transaction removing every matching
/// exclusion with one audit row each. No salt or no match: nothing written.
/// </summary>
public sealed class LiftErasureExclusionUseCase(
    IReadOnlyModeGuard readOnlyMode,
    IErasureSaltStore salts,
    IErasedParticipantRepository exclusions,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    /// <summary>The operation name the read-only guard records; a constant, never user data (SC-10).</summary>
    public const string Operation = "ErasureExclusion.Lift";

    public const int MaxEmailLength = 254;

    public async Task<ExclusionLiftingResult> ExecuteAsync(
        long adminId,
        string? email,
        string? requestId,
        CancellationToken cancellationToken)
    {
        // 1: read-only mode, before validation (AD-6, spec FR-014, api-design §2.4).
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            await AuditRefusalAsync(adminId, requestId, cancellationToken);
            throw;
        }

        // 2: the email (spec VR-003); any domain is accepted (I-13).
        var trimmed = email?.Trim() ?? string.Empty;
        if (Validate(trimmed) is { } error)
        {
            return new ExclusionLiftingResult(ExclusionLiftingOutcome.InvalidEmail, error, 0);
        }

        // 3: no salt yet — no erasure ever ran, so no fingerprinted exclusion can exist (db-design §3.3, §8).
        var salt = await salts.FindAsync(cancellationToken);
        if (salt is null)
        {
            return NoExclusion();
        }

        // The lifting takes no fence: removing an exclusion only permits more (db-design §7.3).
        var fingerprint = EmailFingerprint.Of(trimmed, salt);
        var lifted = 0;
        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var matching = await exclusions.GetByFingerprintAsync(fingerprint, token);
                if (matching.Count == 0)
                {
                    return;
                }

                var now = timeProvider.GetUtcNow();
                foreach (var exclusion in matching)
                {
                    exclusions.Remove(exclusion);
                    auditEvents.Add(AuditEvent.ErasureExclusionLifted(adminId, exclusion.Id, now, requestId));
                }

                await unitOfWork.SaveChangesAsync(token);
                lifted = matching.Count;
            },
            cancellationToken);

        return lifted == 0 ? NoExclusion() : new ExclusionLiftingResult(ExclusionLiftingOutcome.Lifted, null, lifted);
    }

    private static ExclusionLiftingResult NoExclusion() => new(ExclusionLiftingOutcome.NoExclusionForEmail, null, 0);

    /// <summary>
    /// Spec VR-003: required, at most 254 characters, <c>local@domain</c> with one <c>@</c> and non-empty parts. Shared
    /// with the entry of US-065 (its spec VR-001: one validation, no second copy).
    /// </summary>
    internal static LiftFieldError? Validate(string trimmed)
    {
        if (trimmed.Length == 0)
        {
            return LiftFieldError.EmailRequired;
        }

        if (trimmed.Length > MaxEmailLength)
        {
            return LiftFieldError.EmailTooLong;
        }

        var parts = trimmed.Split('@');
        var wellFormed = parts.Length == 2
            && parts[0].Length > 0
            && parts[1].Length > 0
            && !trimmed.Any(char.IsWhiteSpace);
        return wellFormed ? null : LiftFieldError.EmailInvalid;
    }

    /// <summary>
    /// One audit row and nothing else, declared as <see cref="PermittedServiceWrite.AuditEvent"/> — on the BR-026 closed
    /// list — so the refusal is recorded in read-only mode too (spec I-12, db-design §9.4: no target).
    /// </summary>
    private async Task AuditRefusalAsync(long adminId, string? requestId, CancellationToken cancellationToken)
    {
        auditEvents.Add(AuditEvent.ErasureRefused(
            AuditAction.ErasureExclusionLifted,
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
}
