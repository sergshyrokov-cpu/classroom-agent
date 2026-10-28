using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// The exclusion left by the erasure of one participant (US-038 entity model §1; <c>trebovaniya.md</c> §3 v93,
/// BR-076): the Google user id, a salted fingerprint of the email and the erasure date — never a name or an email.
/// Synchronization and the Meet load consult it where they write (spec FR-011, FR-012).
/// </summary>
/// <remarks>
/// Created by the erasure, or entered in advance by email (US-065), and deleted by the lifting. An exclusion entered in
/// advance gains its Google user id once, when synchronization matches it; nothing else ever changes
/// (<c>tg_erased_participant_fill_only</c>, US-065 db-design §2.3). No navigation: nothing it could point to survives
/// the erasure.
/// </remarks>
public sealed class ErasedParticipant
{
    /// <summary>db-design §2.1: as <c>classroom_participant.google_user_id</c>.</summary>
    public const int MaxGoogleUserIdLength = ClassroomParticipant.MaxGoogleUserIdLength;

    /// <summary>db-design §2.1: HMAC-SHA-256 is 32 bytes.</summary>
    public const int FingerprintLength = 32;

    private ErasedParticipant()
    {
    }

    public long Id { get; private set; }

    /// <summary>Null for an exclusion entered in advance until synchronization matches it (US-065 spec FR-003, FR-007).</summary>
    public string? GoogleUserId { get; private set; }

    /// <summary>Null when the participant had no email (US-038 OD-001 a).</summary>
    public byte[]? EmailFingerprint { get; private set; }

    public DateTimeOffset ErasedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The exclusion of one erased participant; rejects an empty or over-long Google user id.</summary>
    public static ErasedParticipant Record(string googleUserId, EmailFingerprint? fingerprint, DateTimeOffset erasedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleUserId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(googleUserId.Length, MaxGoogleUserIdLength, nameof(googleUserId));

        return new ErasedParticipant
        {
            GoogleUserId = googleUserId,
            EmailFingerprint = fingerprint?.Bytes.ToArray(),
            ErasedAt = erasedAt,
        };
    }

    /// <summary>
    /// US-065 spec FR-003, entity model §1: an exclusion entered by email before the person is loaded — the fingerprint,
    /// no Google user id, the entry date in <see cref="ErasedAt"/> (spec I-3).
    /// </summary>
    public static ErasedParticipant EnterInAdvance(EmailFingerprint fingerprint, DateTimeOffset enteredAt) =>
        new()
        {
            GoogleUserId = null,
            EmailFingerprint = fingerprint.Bytes.ToArray(),
            ErasedAt = enteredAt,
        };
}
