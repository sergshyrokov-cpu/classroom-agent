using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>report_template</c> exactly as US-027 db-design §2.1 defines it: school-own data with a bare
/// <c>author_id</c> (no foreign key, no index, PC-11) and a unique normalized name. Enums are stored as lower-case
/// codes through explicit value converters.
/// </summary>
public sealed class ReportTemplateConfiguration : IEntityTypeConfiguration<ReportTemplate>
{
    public void Configure(EntityTypeBuilder<ReportTemplate> builder)
    {
        builder.ToTable("report_template", table =>
        {
            table.HasCheckConstraint("ck_report_template_view", "view IN ('full', 'short')");
            table.HasCheckConstraint(
                "ck_report_template_hours_per_lesson",
                "hours_per_lesson BETWEEN 1 AND 10");
            table.HasCheckConstraint("ck_report_template_scale_mode", "scale_mode IN ('none', 'ranges')");
            table.HasCheckConstraint(
                "ck_report_template_late_mark_kind",
                "late_mark_kind IN ('program', 'own', 'hidden')");
            table.HasCheckConstraint(
                "ck_report_template_late_mark_text",
                "(late_mark_kind = 'own') = (late_mark_text IS NOT NULL)");
        });

        builder.HasKey(t => t.Id).HasName("pk_report_template");
        builder.Property(t => t.Id).UseIdentityByDefaultColumn();
        builder.Property(t => t.Name).HasMaxLength(ReportTemplate.MaxNameLength).IsRequired();
        builder.Property(t => t.NormalizedName).HasMaxLength(ReportTemplate.MaxNameLength).IsRequired();
        builder.Property(t => t.View)
            .HasMaxLength(8)
            .IsRequired()
            .HasConversion(v => ViewCode(v), code => ViewFromCode(code));
        builder.Property(t => t.HideMaterials).IsRequired();
        builder.Property(t => t.HoursPerLesson).IsRequired();
        builder.Property(t => t.ScaleMode)
            .HasMaxLength(8)
            .IsRequired()
            .HasConversion(v => ScaleModeCode(v), code => ScaleModeFromCode(code));
        builder.Property(t => t.LateMarkKind)
            .HasMaxLength(8)
            .IsRequired()
            .HasConversion(v => LateKindCode(v), code => LateKindFromCode(code));
        builder.Property(t => t.LateMarkText).HasMaxLength(ReportTemplate.MaxMarkTextLength);

        // A bare identifier: no navigation, no foreign key, no index (db-design §2.1).
        builder.Property(t => t.AuthorId).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        builder.HasIndex(t => t.NormalizedName)
            .IsUnique()
            .HasDatabaseName("uq_report_template_normalized_name");

        builder.HasMany(t => t.Marks)
            .WithOne()
            .HasForeignKey(m => m.ReportTemplateId)
            .HasConstraintName("fk_report_template_mark_report_template")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.Navigation(t => t.Marks).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(t => t.ScaleRows)
            .WithOne()
            .HasForeignKey(r => r.ReportTemplateId)
            .HasConstraintName("fk_report_template_scale_row_report_template")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.Navigation(t => t.ScaleRows).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static string ViewCode(ReportView value) => value switch
    {
        ReportView.Full => "full",
        ReportView.Short => "short",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static ReportView ViewFromCode(string code) => code switch
    {
        "full" => ReportView.Full,
        "short" => ReportView.Short,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string ScaleModeCode(ReportScaleMode value) => value switch
    {
        ReportScaleMode.None => "none",
        ReportScaleMode.Ranges => "ranges",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static ReportScaleMode ScaleModeFromCode(string code) => code switch
    {
        "none" => ReportScaleMode.None,
        "ranges" => ReportScaleMode.Ranges,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string LateKindCode(ReportLateMarkKind value) => value switch
    {
        ReportLateMarkKind.Program => "program",
        ReportLateMarkKind.Own => "own",
        ReportLateMarkKind.Hidden => "hidden",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static ReportLateMarkKind LateKindFromCode(string code) => code switch
    {
        "program" => ReportLateMarkKind.Program,
        "own" => ReportLateMarkKind.Own,
        "hidden" => ReportLateMarkKind.Hidden,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
