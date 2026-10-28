using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The exclusions (US-038 db-design §2, §7.4, §8). Created and removed; the lookups are set-based per batch (spec §9).
/// Stages changes and never saves (AD-7) — except the one fill of US-065 (db-design §4 step 6), a guarded set-based
/// <c>UPDATE</c> that runs at once inside the caller's transaction.
/// </summary>
public sealed class ErasedParticipantRepository(ClassroomAgentDbContext db, TimeProvider timeProvider)
    : IErasedParticipantRepository
{
    public void Add(ErasedParticipant exclusion) => db.ErasedParticipants.Add(exclusion);

    public async Task<IReadOnlySet<string>> GetExcludedGoogleUserIdsAsync(
        IReadOnlyCollection<string> googleUserIds,
        CancellationToken cancellationToken)
    {
        if (googleUserIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var ids = googleUserIds.Distinct(StringComparer.Ordinal).ToList();
        var found = await db.ErasedParticipants
            .AsNoTracking()
            .Where(e => e.GoogleUserId != null && ids.Contains(e.GoogleUserId))
            .Select(e => e.GoogleUserId!)
            .ToListAsync(cancellationToken);
        return found.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<EmailFingerprint>> GetExcludedFingerprintsAsync(
        IReadOnlyCollection<EmailFingerprint> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var values = candidates.Select(c => c.Bytes.ToArray()).ToList();
        var found = await db.ErasedParticipants
            .AsNoTracking()
            .Where(e => e.EmailFingerprint != null && values.Contains(e.EmailFingerprint))
            .Select(e => e.EmailFingerprint!)
            .ToListAsync(cancellationToken);
        return candidates.Where(c => found.Any(f => c.Bytes.Span.SequenceEqual(f))).ToList();
    }

    public async Task<IReadOnlyList<ErasedParticipant>> GetByFingerprintAsync(
        EmailFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var value = fingerprint.Bytes.ToArray();
        return await db.ErasedParticipants
            .Where(e => e.EmailFingerprint == value)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public void Remove(ErasedParticipant exclusion) => db.ErasedParticipants.Remove(exclusion);

    /// <summary>US-065 db-design §3 step 4: any exclusion — entered in advance or left by an erasure — with this fingerprint.</summary>
    public Task<bool> AnyWithFingerprintAsync(EmailFingerprint fingerprint, CancellationToken cancellationToken)
    {
        var value = fingerprint.Bytes.ToArray();
        return db.ErasedParticipants.AsNoTracking().AnyAsync(e => e.EmailFingerprint == value, cancellationToken);
    }

    /// <summary>
    /// US-065 db-design §4 step 3: the exclusions with no Google user id whose fingerprint is a candidate, in one query,
    /// in id order (the fill goes by id, db-design §4).
    /// </summary>
    public async Task<IReadOnlyList<UnmatchedExclusion>> GetUnmatchedByFingerprintsAsync(
        IReadOnlyCollection<EmailFingerprint> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var distinct = candidates.Distinct().ToList();
        var values = distinct.Select(c => c.Bytes.ToArray()).ToList();
        var found = await db.ErasedParticipants
            .AsNoTracking()
            .Where(e => e.GoogleUserId == null && e.EmailFingerprint != null && values.Contains(e.EmailFingerprint))
            .OrderBy(e => e.Id)
            .Select(e => new { e.Id, Fingerprint = e.EmailFingerprint! })
            .ToListAsync(cancellationToken);

        // The value object is the candidate the stored bytes equal, as in GetExcludedFingerprintsAsync.
        return found
            .Select(e => new UnmatchedExclusion(e.Id, distinct.First(c => c.Bytes.Span.SequenceEqual(e.Fingerprint))))
            .ToList();
    }

    /// <summary>
    /// US-065 db-design §4 step 6: the one fill, guarded on the id still being absent. Zero rows — the exclusion was lifted
    /// meanwhile or already filled — is not an error. <c>ExecuteUpdate</c> bypasses the timestamp interceptor, so
    /// <c>updated_at</c> is set here from the injected clock (PC-6).
    /// </summary>
    public async Task<bool> FillGoogleUserIdAsync(long exclusionId, string googleUserId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleUserId);

        var now = timeProvider.GetUtcNow();
        var affected = await db.ErasedParticipants
            .Where(e => e.Id == exclusionId && e.GoogleUserId == null)
            .ExecuteUpdateAsync(
                set => set.SetProperty(e => e.GoogleUserId, googleUserId).SetProperty(e => e.UpdatedAt, now),
                cancellationToken);
        return affected == 1;
    }
}
