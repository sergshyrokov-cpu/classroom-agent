using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>course_membership</c> exactly as US-014 db-design §5 defines it: the explicit entity between a course
/// and a person, <b>unique on (course, participant)</b> per PC-8 with the role as a field (FR-007, I-9), carrying
/// the three BR-051 observations. Both foreign keys are <c>Restrict</c> (db-design §5.4).
/// </summary>
/// <remarks>
/// <c>on_roster</c> is deliberately not constrained against the dates: "in the past" needs a clock a row-level
/// check has none of, and equal instants are the legitimate shape — a person seen in this very run and still on
/// the roster. The entity owns that invariant instead (entity model §4.2).
/// </remarks>
public sealed class CourseMembershipConfiguration : IEntityTypeConfiguration<CourseMembership>
{
    public void Configure(EntityTypeBuilder<CourseMembership> builder)
    {
        builder.ToTable("course_membership", table =>
        {
            table.HasCheckConstraint("ck_course_membership_role", "role IN ('teacher', 'student')");
            table.HasCheckConstraint("ck_course_membership_seen_order", "last_seen_at >= first_seen_at");
        });

        builder.HasKey(m => m.Id).HasName("pk_course_membership");
        builder.Property(m => m.Id).UseIdentityByDefaultColumn();
        builder.Property(m => m.CourseId).IsRequired();
        builder.Property(m => m.ParticipantId).IsRequired();
        builder.Property(m => m.Role)
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => RoleCode(v), code => RoleFromCode(code));
        builder.Property(m => m.FirstSeenAt).IsRequired();
        builder.Property(m => m.LastSeenAt).IsRequired();
        builder.Property(m => m.OnRoster).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();

        builder.HasOne(m => m.Course)
            .WithMany()
            .HasForeignKey(m => m.CourseId)
            .HasConstraintName("fk_course_membership_course")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(m => m.Participant)
            .WithMany()
            .HasForeignKey(m => m.ParticipantId)
            .HasConstraintName("fk_course_membership_classroom_participant")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(m => new { m.CourseId, m.ParticipantId })
            .IsUnique()
            .HasDatabaseName("uq_course_membership_course_participant");

        // PC-7: "who was on this roster on the date of that meeting" (BR-051) — the equality on the course and the
        // range on the dates, which the unique index above cannot serve because it has no date column.
        builder.HasIndex(m => new { m.CourseId, m.FirstSeenAt, m.LastSeenAt })
            .HasDatabaseName("ix_course_membership_course_seen");

        builder.HasIndex(m => m.ParticipantId).HasDatabaseName("ix_course_membership_participant_id");
    }

    private static string RoleCode(ClassroomRole value) => value switch
    {
        ClassroomRole.Teacher => "teacher",
        ClassroomRole.Student => "student",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static ClassroomRole RoleFromCode(string code) => code switch
    {
        "teacher" => ClassroomRole.Teacher,
        "student" => ClassroomRole.Student,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
