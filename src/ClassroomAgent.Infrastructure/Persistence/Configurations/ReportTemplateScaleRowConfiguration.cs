using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>report_template_scale_row</c> as US-027 db-design §2.3 defines it. The foreign key and its cascade are
/// declared on the root's configuration; the unique index doubles as the foreign key's index.
/// </summary>
public sealed class ReportTemplateScaleRowConfiguration : IEntityTypeConfiguration<ReportTemplateScaleRow>
{
    public void Configure(EntityTypeBuilder<ReportTemplateScaleRow> builder)
    {
        builder.ToTable("report_template_scale_row", table =>
        {
            table.HasCheckConstraint(
                "ck_report_template_scale_row_bounds",
                "from_percent BETWEEN 0 AND 100 AND to_percent BETWEEN 0 AND 100 AND from_percent <= to_percent");
        });

        builder.HasKey(r => r.Id).HasName("pk_report_template_scale_row");
        builder.Property(r => r.Id).UseIdentityByDefaultColumn();
        builder.Property(r => r.ReportTemplateId).IsRequired();
        builder.Property(r => r.FromPercent).HasColumnType("smallint").IsRequired();
        builder.Property(r => r.ToPercent).HasColumnType("smallint").IsRequired();
        builder.Property(r => r.Label).HasMaxLength(ReportTemplate.MaxScaleLabelLength).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasIndex(r => new { r.ReportTemplateId, r.FromPercent })
            .IsUnique()
            .HasDatabaseName("uq_report_template_scale_row_template_from");
    }
}
