using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>sync_skipped_course</c> exactly as US-058 db-design §4 defines it: one row per course the latest runs
/// skipped, keyed by Google's course id alone — no foreign key to <c>course</c> or <c>sync_state</c> (db-design §1).
/// </summary>
public sealed class SyncSkippedCourseConfiguration : IEntityTypeConfiguration<SyncSkippedCourse>
{
    public void Configure(EntityTypeBuilder<SyncSkippedCourse> builder)
    {
        builder.ToTable("sync_skipped_course", table =>
        {
            table.HasCheckConstraint(
                "ck_sync_skipped_course_reason",
                "reason IN ('value_out_of_range', 'unknown_course_state')");
            table.HasCheckConstraint("ck_sync_skipped_course_google_id", "char_length(course_google_id) >= 1");
            table.HasCheckConstraint(
                "ck_sync_skipped_course_name",
                "course_name IS NULL OR char_length(course_name) >= 1");
        });

        builder.HasKey(s => s.Id).HasName("pk_sync_skipped_course");
        builder.Property(s => s.Id).UseIdentityByDefaultColumn();
        builder.Property(s => s.CourseGoogleId).HasMaxLength(Course.MaxGoogleIdLength).IsRequired();
        builder.Property(s => s.CourseName).HasMaxLength(Course.MaxNameLength);
        builder.Property(s => s.Reason)
            .HasMaxLength(32)
            .IsRequired()
            .HasConversion(new ValueConverter<CourseSkipReason, string>(v => ReasonCode(v), code => ReasonFromCode(code)));
        builder.Property(s => s.SkippedSince).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasIndex(s => s.CourseGoogleId).IsUnique().HasDatabaseName("uq_sync_skipped_course_course_google_id");
    }

    private static string ReasonCode(CourseSkipReason value) => value switch
    {
        CourseSkipReason.ValueOutOfRange => "value_out_of_range",
        CourseSkipReason.UnknownCourseState => "unknown_course_state",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static CourseSkipReason ReasonFromCode(string code) => code switch
    {
        "value_out_of_range" => CourseSkipReason.ValueOutOfRange,
        "unknown_course_state" => CourseSkipReason.UnknownCourseState,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
