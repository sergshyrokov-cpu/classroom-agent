using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>erased_participant</c> exactly as US-038 db-design §2 defines it: the Google user id, the email fingerprint
/// and the erasure date — no name, no email, no foreign key. One exclusion per Google user id; a fingerprint may repeat
/// (spec I-10). Created and deleted, never changed.
/// </summary>
public sealed class ErasedParticipantConfiguration : IEntityTypeConfiguration<ErasedParticipant>
{
    public void Configure(EntityTypeBuilder<ErasedParticipant> builder)
    {
        builder.ToTable("erased_participant", table =>
        {
            table.HasCheckConstraint(
                "ck_erased_participant_values",
                "char_length(google_user_id) >= 1 "
                + "AND (email_fingerprint IS NULL OR octet_length(email_fingerprint) = "
                + $"{ErasedParticipant.FingerprintLength})");
            table.HasCheckConstraint("ck_erased_participant_immutable", "updated_at = created_at");
        });

        builder.HasKey(e => e.Id).HasName("pk_erased_participant");
        builder.Property(e => e.Id).UseIdentityByDefaultColumn();
        builder.Property(e => e.GoogleUserId).HasMaxLength(ErasedParticipant.MaxGoogleUserIdLength).IsRequired();
        builder.Property(e => e.EmailFingerprint).HasColumnType("bytea");
        builder.Property(e => e.ErasedAt).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        builder.HasIndex(e => e.GoogleUserId).IsUnique().HasDatabaseName("uq_erased_participant_google_user_id");
        builder.HasIndex(e => e.EmailFingerprint).HasDatabaseName("ix_erased_participant_email_fingerprint");
    }
}
