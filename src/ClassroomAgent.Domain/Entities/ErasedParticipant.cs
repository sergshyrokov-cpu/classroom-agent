using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// The exclusion left by the erasure of one participant (US-038 entity model §1; <c>trebovaniya.md</c> §3 v93,
/// BR-076): the Google user id, a salted fingerprint of the email and the erasure date — never a name or an email.
/// Synchronization and the Meet load consult it where they write (spec FR-011, FR-012).
/// </summary>
/// <remarks>
/// Created by the erasure and deleted by the lifting; never changed (<c>ck_erased_participant_immutable</c>). No
/// navigation: nothing it could point to survives the erasure.
/// </remarks>
public sealed class ErasedParticipant
{
    /// <summary>db-design §2.1: as <c>classroom_participant.google_user_id</c>.</summary>
    public const int MaxGoogleUserIdLength = ClassroomParticipant.MaxGoogleUserIdLength;

    /// <summary>db-design §2.1: HMAC-SHA-256 is 32 bytes.</summary>
    public const int FingerprintLength = 32;

    private ErasedParticipant()
    {
        GoogleUserId = string.Empty;
    }

    public long Id { get; private set; }

    public string GoogleUserId { get; private set; }

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
}
