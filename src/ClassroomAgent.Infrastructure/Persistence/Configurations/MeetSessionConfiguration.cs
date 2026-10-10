using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>meet_session</c> exactly as US-031 db-design §2 defines it: one row per Google conference, unique on the
/// conference id, with no foreign key to a course or a Classroom participant (PC-8, PC-12). Its participations are a
/// <c>Restrict</c> child collection (§3.2), loaded with it and written through it.
/// </summary>
public sealed class MeetSessionConfiguration : IEntityTypeConfiguration<MeetSession>
{
    public void Configure(EntityTypeBuilder<MeetSession> builder)
    {
        builder.ToTable("meet_session", table =>
        {
            table.HasCheckConstraint("ck_meet_session_ended_after_started", "ended_at >= started_at");
            table.HasCheckConstraint(
                "ck_meet_session_values",
                "char_length(conference_id) >= 1 AND char_length(meeting_code) >= 1 AND char_length(organizer_email) >= 3");
        });

        builder.HasKey(s => s.Id).HasName("pk_meet_session");
        builder.Property(s => s.Id).UseIdentityByDefaultColumn();
        builder.Property(s => s.ConferenceId).HasMaxLength(MeetSession.ConferenceIdMaxLength).IsRequired();
        builder.Property(s => s.MeetingCode).HasMaxLength(MeetSession.MeetingCodeMaxLength).IsRequired();
        builder.Property(s => s.OrganizerEmail).HasMaxLength(MeetSession.EmailMaxLength).IsRequired();
        builder.Property(s => s.StartedAt).IsRequired();
        builder.Property(s => s.EndedAt).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasIndex(s => s.ConferenceId).IsUnique().HasDatabaseName("uq_meet_session_conference_id");

        // db-design §2.3: the daily purge by meeting date, and the "code + date" index PC-7 names for Meet statistics.
        builder.HasIndex(s => s.StartedAt).HasDatabaseName("ix_meet_session_started_at");
        builder.HasIndex(s => new { s.MeetingCode, s.StartedAt }).HasDatabaseName("ix_meet_session_meeting_code_started_at");

        builder.HasMany(s => s.Participations)
            .WithOne()
            .HasForeignKey(p => p.MeetSessionId)
            .HasConstraintName("fk_meet_participation_meet_session_id")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.Navigation(s => s.Participations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
