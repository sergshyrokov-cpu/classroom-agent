using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>erased_participant</c> as US-038 db-design §2 and US-065 db-design §2 define it: the Google user id (absent
/// for an exclusion entered in advance until it is matched), the email fingerprint and the erasure date — no name, no
/// email, no foreign key. One exclusion per Google user id; a fingerprint may repeat (US-038 spec I-10).
/// </summary>
/// <remarks>
/// US-065 db-design §2.3: the trigger <c>tg_erased_participant_fill_only</c> (function
/// <c>erased_participant_fill_only()</c>) allows exactly one update — a missing Google user id gaining one — and rejects
/// every other. Migration <c>AddErasureExclusionInAdvance</c> creates it; the model snapshot cannot show it.
/// </remarks>
public sealed class ErasedParticipantConfiguration : IEntityTypeConfiguration<ErasedParticipant>
{
    public void Configure(EntityTypeBuilder<ErasedParticipant> builder)
    {
        builder.ToTable("erased_participant", table =>
        {
            table.HasCheckConstraint(
                "ck_erased_participant_values",
                "(google_user_id IS NULL OR char_length(google_user_id) >= 1) "
                + "AND (email_fingerprint IS NULL OR octet_length(email_fingerprint) = "
                + $"{ErasedParticipant.FingerprintLength}) "
                + "AND (google_user_id IS NOT NULL OR email_fingerprint IS NOT NULL)");
        });

        builder.HasKey(e => e.Id).HasName("pk_erased_participant");
        builder.Property(e => e.Id).UseIdentityByDefaultColumn();
        builder.Property(e => e.GoogleUserId).HasMaxLength(ErasedParticipant.MaxGoogleUserIdLength);
        builder.Property(e => e.EmailFingerprint).HasColumnType("bytea");
        builder.Property(e => e.ErasedAt).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        builder.HasIndex(e => e.GoogleUserId).IsUnique().HasDatabaseName("uq_erased_participant_google_user_id");
        builder.HasIndex(e => e.EmailFingerprint).HasDatabaseName("ix_erased_participant_email_fingerprint");
    }
}
