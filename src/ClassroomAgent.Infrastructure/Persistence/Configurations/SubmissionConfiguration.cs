using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>submission</c> exactly as US-015 db-design §4 defines it: the upsert key scoped by the owning course
/// work (Specification v2), the seven-code state vocabulary plus OD-005's <c>raw_state</c> biconditional, and the
/// deliberately non-unique cell-lookup index of §4.3.
/// </summary>
/// <remarks>
/// The state is stored as a lower-case code through a value converter, the <c>SyncStateConfiguration</c> pattern
/// US-014 also followed. Both foreign keys are <c>Restrict</c> (db-design §4.4, PC-8) so a student's grades never
/// disappear because a parent row was removed.
/// </remarks>
public sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submission", table =>
        {
            table.HasCheckConstraint(
                "ck_submission_state",
                "state IN ('new', 'created', 'turned_in', 'returned', 'reclaimed_by_student', " +
                "'student_edited_after_turn_in', 'unrecognised')");
            table.HasCheckConstraint(
                "ck_submission_raw_state",
                "(state = 'unrecognised') = (raw_state IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_submission_grades_non_negative",
                "(assigned_grade IS NULL OR assigned_grade >= 0) AND (draft_grade IS NULL OR draft_grade >= 0)");
        });

        builder.HasKey(s => s.Id).HasName("pk_submission");
        builder.Property(s => s.Id).UseIdentityByDefaultColumn();
        builder.Property(s => s.CourseWorkId).IsRequired();
        builder.Property(s => s.ParticipantId).IsRequired();
        builder.Property(s => s.GoogleId).HasMaxLength(Submission.MaxGoogleIdLength).IsRequired();
        builder.Property(s => s.State)
            .HasColumnName("state")
            .HasMaxLength(32)
            .IsRequired()
            .HasConversion(v => StateCode(v), code => StateFromCode(code));
        builder.Property(s => s.RawState).HasMaxLength(Submission.MaxRawStateLength);
        builder.Property(s => s.AssignedGrade).HasColumnType("numeric(10,4)");
        builder.Property(s => s.DraftGrade).HasColumnType("numeric(10,4)");
        builder.Property(s => s.TurnedInAt);
        builder.Property(s => s.Late).IsRequired();
        builder.Property(s => s.UpdateTime);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasOne<CourseWork>()
            .WithMany()
            .HasForeignKey(s => s.CourseWorkId)
            .HasConstraintName("fk_submission_course_work")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<ClassroomParticipant>()
            .WithMany()
            .HasForeignKey(s => s.ParticipantId)
            .HasConstraintName("fk_submission_classroom_participant")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(s => new { s.CourseWorkId, s.GoogleId })
            .IsUnique()
            .HasDatabaseName("uq_submission_course_work_google_id");

        builder.HasIndex(s => new { s.CourseWorkId, s.ParticipantId })
            .HasDatabaseName("ix_submission_course_work_participant");

        builder.HasIndex(s => s.ParticipantId).HasDatabaseName("ix_submission_participant_id");
    }

    private static string StateCode(SubmissionState value) => value switch
    {
        SubmissionState.New => "new",
        SubmissionState.Created => "created",
        SubmissionState.TurnedIn => "turned_in",
        SubmissionState.Returned => "returned",
        SubmissionState.ReclaimedByStudent => "reclaimed_by_student",
        SubmissionState.StudentEditedAfterTurnIn => "student_edited_after_turn_in",
        SubmissionState.Unrecognised => "unrecognised",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static SubmissionState StateFromCode(string code) => code switch
    {
        "new" => SubmissionState.New,
        "created" => SubmissionState.Created,
        "turned_in" => SubmissionState.TurnedIn,
        "returned" => SubmissionState.Returned,
        "reclaimed_by_student" => SubmissionState.ReclaimedByStudent,
        "student_edited_after_turn_in" => SubmissionState.StudentEditedAfterTurnIn,
        "unrecognised" => SubmissionState.Unrecognised,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
