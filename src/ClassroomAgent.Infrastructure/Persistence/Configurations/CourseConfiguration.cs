using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
        builder.ToTable("course", table => table.HasCheckConstraint(
            "ck_course_course_state",
            "course_state IN ('active', 'archived', 'provisioned', 'declined', 'suspended')"));

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
}
