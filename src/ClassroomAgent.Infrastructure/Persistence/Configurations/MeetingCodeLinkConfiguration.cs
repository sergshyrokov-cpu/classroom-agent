using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>meeting_code_link</c> exactly as US-032 db-design §2 defines it: one row per meeting code that is linked to
/// a course or marked "not a course" (a row with no course). The account ids are bare identifiers without foreign keys
/// (PC-11); the course foreign key is <c>Restrict</c> (PC-8) and the purge deletes links before the course.
/// </summary>
public sealed class MeetingCodeLinkConfiguration : IEntityTypeConfiguration<MeetingCodeLink>
{
    public void Configure(EntityTypeBuilder<MeetingCodeLink> builder)
    {
        builder.ToTable("meeting_code_link", table =>
        {
            table.HasCheckConstraint(
                "ck_meeting_code_link_state",
                "(course_id IS NOT NULL AND linked_automatically IS NOT NULL AND linked_at IS NOT NULL "
                + "AND marked_by_app_user_id IS NULL AND marked_at IS NULL) "
                + "OR (course_id IS NULL AND linked_automatically IS NULL AND linked_at IS NULL "
                + "AND linked_by_app_user_id IS NULL AND confirmed_by_app_user_id IS NULL "
                + "AND confirmed_at IS NULL "
                + "AND marked_by_app_user_id IS NOT NULL AND marked_at IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_meeting_code_link_maker",
                "linked_automatically IS NULL OR linked_automatically = (linked_by_app_user_id IS NULL)");
            table.HasCheckConstraint(
                "ck_meeting_code_link_confirmation",
                "(confirmed_by_app_user_id IS NULL) = (confirmed_at IS NULL) "
                + "AND (confirmed_at IS NULL OR linked_automatically)");
            table.HasCheckConstraint(
                "ck_meeting_code_link_values",
                "char_length(meeting_code) >= 1 AND char_length(concurrency_stamp) >= 1");
        });

        builder.HasKey(l => l.Id).HasName("pk_meeting_code_link");
        builder.Property(l => l.Id).UseIdentityByDefaultColumn();
        builder.Property(l => l.MeetingCode).HasMaxLength(MeetingCodeLink.MeetingCodeMaxLength).IsRequired();
        builder.Property(l => l.CourseId);
        builder.Property(l => l.LinkedAutomatically);
        builder.Property(l => l.LinkedByAppUserId);
        builder.Property(l => l.LinkedAt);
        builder.Property(l => l.ConfirmedByAppUserId);
        builder.Property(l => l.ConfirmedAt);
        builder.Property(l => l.MarkedByAppUserId);
        builder.Property(l => l.MarkedAt);
        builder.Property(l => l.ConcurrencyStamp).HasMaxLength(64).IsRequired().IsConcurrencyToken();
        builder.Property(l => l.CreatedAt).IsRequired();
        builder.Property(l => l.UpdatedAt).IsRequired();

        // A unique constraint (db-design §2.2, "uq_" names a constraint); PostgreSQL backs it with the index of the same
        // name, which is what the anti-join on the code uses (§2.3).
        builder.HasAlternateKey(l => l.MeetingCode).HasName("uq_meeting_code_link_meeting_code");
        builder.HasIndex(l => l.CourseId).HasDatabaseName("ix_meeting_code_link_course_id");

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(l => l.CourseId)
            .HasConstraintName("fk_meeting_code_link_course_id")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
