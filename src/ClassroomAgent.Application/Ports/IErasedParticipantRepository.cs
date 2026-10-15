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
}
