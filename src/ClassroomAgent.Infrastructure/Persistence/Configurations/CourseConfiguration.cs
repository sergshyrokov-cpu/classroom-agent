using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>course</c> exactly as US-014 db-design §3 defines it: the Google course id is the upsert key with its
/// own unique index (PC-3), the owner is a value and not a reference (OD-004), and the five-value state
/// vocabulary is kept honest by <c>ck_course_course_state</c> — the database half of OD-010.
/// </summary>
/// <remarks>
/// The state is stored as a lower-case code through a value converter, exactly as <c>SyncStateConfiguration</c>
/// stores its three statuses. There is deliberately no index besides the unique one: this Story's only lookup is
/// by <c>google_id</c>, and EPIC-2's filters should be indexed against US-020's real queries (db-design §3.4).
/// </remarks>
public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("course", table =>
        {
            table.HasCheckConstraint(
                "ck_course_course_state",
                "course_state IN ('active', 'archived', 'provisioned', 'declined', 'suspended')");

            // US-059 db-design §2, VR-003: no mark, or a kind of the closed set together with its instant. The explicit
            // "absence_mark IS NOT NULL" keeps an instant without a kind from passing: NULL IN (...) is NULL, which a
            // CHECK accepts.
            table.HasCheckConstraint(
                "ck_course_absence_mark",
                "(absence_mark IS NULL AND absent_since IS NULL) OR (absence_mark IS NOT NULL AND absence_mark IN ('not_in_classroom', 'not_visible_to_technical_account') AND absent_since IS NOT NULL)");
        });

        builder.HasKey(c => c.Id).HasName("pk_course");
        builder.Property(c => c.Id).UseIdentityByDefaultColumn();
        builder.Property(c => c.GoogleId).HasMaxLength(Course.MaxGoogleIdLength).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(Course.MaxNameLength).IsRequired();
        builder.Property(c => c.Section).HasMaxLength(Course.MaxSectionLength);
        builder.Property(c => c.DescriptionHeading).HasMaxLength(Course.MaxDescriptionHeadingLength);
        builder.Property(c => c.Description).HasMaxLength(Course.MaxDescriptionLength);
        builder.Property(c => c.Room).HasMaxLength(Course.MaxRoomLength);
        builder.Property(c => c.OwnerGoogleId).HasMaxLength(Course.MaxOwnerGoogleIdLength);
        builder.Property(c => c.CreationTime);
        builder.Property(c => c.UpdateTime);

        // US-018 db-design §2: sync bookkeeping beside the mirror, nullable, no index (uq_course_google_id serves it).
        builder.Property(c => c.CompleteReadUpdateTime);

        // US-058 db-design §3: the run instant of the last committed full read, nullable, no index.
        builder.Property(c => c.LastCompleteReadAt);

        // US-059 db-design §2: the course's mark beside the mirror — a lower-case code and its instant — and the last
        // sighting on a complete list; all nullable, no index.
        builder.Property(c => c.AbsenceMark)
            .HasMaxLength(40)
            .HasConversion(new ValueConverter<CourseAbsenceMark, string>(v => MarkCode(v), code => MarkFromCode(code)));
        builder.Property(c => c.AbsentSince);
        builder.Property(c => c.LastListedAt);
        builder.Property(c => c.State)
            .HasColumnName("course_state")
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => StateCode(v), code => StateFromCode(code));
        builder.Property(c => c.AlternateLink).HasMaxLength(Course.MaxAlternateLinkLength);
        builder.Property(c => c.TeacherFolderId).HasMaxLength(Course.MaxTeacherFolderIdLength);
        builder.Property(c => c.TeacherFolderTitle).HasMaxLength(Course.MaxTeacherFolderTitleLength);
        builder.Property(c => c.CalendarId).HasMaxLength(Course.MaxCalendarIdLength);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasIndex(c => c.GoogleId).IsUnique().HasDatabaseName("uq_course_google_id");

        // US-038 db-design §6: the lookup key of the erasure's count and clearing of an owner (PC-7).
        builder.HasIndex(c => c.OwnerGoogleId).HasDatabaseName("ix_course_owner_google_id");
    }

    private static string StateCode(CourseState value) => value switch
    {
        CourseState.Active => "active",
        CourseState.Archived => "archived",
        CourseState.Provisioned => "provisioned",
        CourseState.Declined => "declined",
        CourseState.Suspended => "suspended",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static CourseState StateFromCode(string code) => code switch
    {
        "active" => CourseState.Active,
        "archived" => CourseState.Archived,
        "provisioned" => CourseState.Provisioned,
        "declined" => CourseState.Declined,
        "suspended" => CourseState.Suspended,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string MarkCode(CourseAbsenceMark value) => value switch
    {
        CourseAbsenceMark.NotInClassroom => "not_in_classroom",
        CourseAbsenceMark.NotVisibleToTechnicalAccount => "not_visible_to_technical_account",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static CourseAbsenceMark MarkFromCode(string code) => code switch
    {
        "not_in_classroom" => CourseAbsenceMark.NotInClassroom,
        "not_visible_to_technical_account" => CourseAbsenceMark.NotVisibleToTechnicalAccount,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
