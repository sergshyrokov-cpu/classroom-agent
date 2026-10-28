using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.Ports;

/// <summary>The exclusions (US-038 entity model §7, db-design §2, §7.4, §8). Created and removed; never updated.</summary>
public interface IErasedParticipantRepository
{
    void Add(ErasedParticipant exclusion);

    /// <summary>Those of <paramref name="googleUserIds"/> that are excluded (synchronization, per course).</summary>
    Task<IReadOnlySet<string>> GetExcludedGoogleUserIdsAsync(
        IReadOnlyCollection<string> googleUserIds,
        CancellationToken cancellationToken);

    /// <summary>Those of <paramref name="candidates"/> that match an exclusion (Meet load, per page).</summary>
    Task<IReadOnlyList<EmailFingerprint>> GetExcludedFingerprintsAsync(
        IReadOnlyCollection<EmailFingerprint> candidates,
        CancellationToken cancellationToken);

    /// <summary>Every exclusion with that fingerprint — more than one is possible (spec I-10).</summary>
    Task<IReadOnlyList<ErasedParticipant>> GetByFingerprintAsync(
        EmailFingerprint fingerprint,
        CancellationToken cancellationToken);

    void Remove(ErasedParticipant exclusion);

    /// <summary>US-065 entity model §3: true when any exclusion has this fingerprint (entry, "already excluded").</summary>
    Task<bool> AnyWithFingerprintAsync(EmailFingerprint fingerprint, CancellationToken cancellationToken);

    /// <summary>US-065 entity model §3: exclusions with no Google id whose fingerprint is among the candidates (sync match).</summary>
    Task<IReadOnlyList<UnmatchedExclusion>> GetUnmatchedByFingerprintsAsync(
        IReadOnlyCollection<EmailFingerprint> candidates,
        CancellationToken cancellationToken);

    /// <summary>
    /// US-065 entity model §3, db-design §4 step 6: the one fill; false when the row is gone or already has an id.
    /// </summary>
    Task<bool> FillGoogleUserIdAsync(long exclusionId, string googleUserId, CancellationToken cancellationToken);
}
