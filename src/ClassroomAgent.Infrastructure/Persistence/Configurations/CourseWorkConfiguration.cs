using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>course_work</c> exactly as US-015 db-design §3 defines it: both Classroom resources in one table
/// (OD-008), the upsert key scoped by course and resource (Specification v2, PC-3), and the material shape guard
/// that keeps a <c>courseWorkMaterial</c> from acquiring grading columns (§3.2).
/// </summary>
/// <remarks>
/// The resource is stored as a lower-case code through a value converter, the <c>SyncStateConfiguration</c>
/// pattern US-014 also followed. <see cref="CourseWork.Kind"/> is computed and never stored (db-design §3.6, §9)
/// so it is explicitly ignored.
/// </remarks>
public sealed class CourseWorkConfiguration : IEntityTypeConfiguration<CourseWork>
{
    public void Configure(EntityTypeBuilder<CourseWork> builder)
    {
        builder.ToTable("course_work", table =>
        {
            table.HasCheckConstraint(
                "ck_course_work_resource",
                "resource IN ('course_work', 'course_work_material')");
            table.HasCheckConstraint(
                "ck_course_work_material_has_no_grading",
                "resource <> 'course_work_material' OR (max_points IS NULL AND due_at IS NULL)");
            table.HasCheckConstraint(
                "ck_course_work_max_points_non_negative",
                "max_points IS NULL OR max_points >= 0");
        });

        builder.HasKey(c => c.Id).HasName("pk_course_work");
        builder.Property(c => c.Id).UseIdentityByDefaultColumn();
        builder.Property(c => c.CourseId).IsRequired();
        builder.Property(c => c.GoogleId).HasMaxLength(CourseWork.MaxGoogleIdLength).IsRequired();
        builder.Property(c => c.Resource)
            .HasColumnName("resource")
            .HasMaxLength(24)
            .IsRequired()
            .HasConversion(v => ResourceCode(v), code => ResourceFromCode(code));
        builder.Property(c => c.Title).HasMaxLength(CourseWork.MaxTitleLength).IsRequired();
        builder.Property(c => c.ItemDate).IsRequired();
        builder.Property(c => c.DueAt);
        builder.Property(c => c.MaxPoints).HasColumnType("numeric(10,4)");
        builder.Property(c => c.CreationTime);
        builder.Property(c => c.ScheduledTime);
        builder.Property(c => c.UpdateTime);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.Ignore(c => c.Kind);

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(c => c.CourseId)
            .HasConstraintName("fk_course_work_course")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(c => new { c.CourseId, c.Resource, c.GoogleId })
            .IsUnique()
            .HasDatabaseName("uq_course_work_course_resource_google_id");

        builder.HasIndex(c => new { c.CourseId, c.ItemDate })
            .HasDatabaseName("ix_course_work_course_item_date");
    }

    private static string ResourceCode(CourseWorkResource value) => value switch
    {
        CourseWorkResource.CourseWork => "course_work",
        CourseWorkResource.CourseWorkMaterial => "course_work_material",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static CourseWorkResource ResourceFromCode(string code) => code switch
    {
        "course_work" => CourseWorkResource.CourseWork,
        "course_work_material" => CourseWorkResource.CourseWorkMaterial,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
