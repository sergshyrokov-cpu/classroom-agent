using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>meet_participation</c> exactly as US-031 db-design §3 defines it: one row per (session, endpoint). The
/// email is held only for a domain account; <c>email IS NULL</c> is the "other participant" mark. No name, display
/// name, device, location or telemetry column exists (AC-013), and no foreign key to <c>classroom_participant</c>.
/// </summary>
public sealed class MeetParticipationConfiguration : IEntityTypeConfiguration<MeetParticipation>
{
    public void Configure(EntityTypeBuilder<MeetParticipation> builder)
    {
        builder.ToTable("meet_participation", table =>
        {
            table.HasCheckConstraint(
                "ck_meet_participation_duration",
                $"duration_seconds BETWEEN 0 AND {MeetParticipation.MaxDurationSeconds}");
            table.HasCheckConstraint(
                "ck_meet_participation_values",
                "char_length(endpoint_id) >= 1 AND (email IS NULL OR char_length(email) >= 3)");
        });

        builder.HasKey(p => p.Id).HasName("pk_meet_participation");
        builder.Property(p => p.Id).UseIdentityByDefaultColumn();
        builder.Property(p => p.MeetSessionId).IsRequired();
        builder.Property(p => p.EndpointId).HasMaxLength(MeetParticipation.EndpointIdMaxLength).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(MeetSession.EmailMaxLength);
        builder.Property(p => p.JoinedAt).IsRequired();
        builder.Property(p => p.DurationSeconds).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
        builder.Ignore(p => p.IsOtherParticipant);

        // PC-3, AC-003: one row per connection; also the foreign key's index (PC-7).
        builder.HasIndex(p => new { p.MeetSessionId, p.EndpointId })
            .IsUnique()
            .HasDatabaseName("uq_meet_participation_meet_session_id_endpoint_id");

        // db-design §3.3: the "email + date" index PC-7 names for Meet statistics and the leaver expiry of US-032.
        builder.HasIndex(p => new { p.Email, p.JoinedAt }).HasDatabaseName("ix_meet_participation_email_joined_at");
    }
}
