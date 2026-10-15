using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The exclusions (US-038 db-design §2, §7.4, §8). Created and removed, never updated; the lookups are set-based per
/// batch (spec §9). Stages changes; never saves (AD-7).
/// </summary>
public sealed class ErasedParticipantRepository(ClassroomAgentDbContext db) : IErasedParticipantRepository
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
            .Where(e => ids.Contains(e.GoogleUserId))
            .Select(e => e.GoogleUserId)
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
}
