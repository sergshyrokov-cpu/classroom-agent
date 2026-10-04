using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>report_template_mark</c> as US-027 db-design §2.2 defines it: one row per cell state. The foreign key
/// and its cascade are declared on the root's configuration; the unique index doubles as the foreign key's index.
/// </summary>
public sealed class ReportTemplateMarkConfiguration : IEntityTypeConfiguration<ReportTemplateMark>
{
    public void Configure(EntityTypeBuilder<ReportTemplateMark> builder)
    {
        builder.ToTable("report_template_mark", table =>
        {
            table.HasCheckConstraint(
                "ck_report_template_mark_state",
                "state IN ('turned_in_not_graded', 'returned_without_grade', 'turned_in', 'returned', "
                + "'not_turned_in', 'not_due_yet', 'not_turned_in_no_due_date', 'not_assigned', 'unrecognised')");
            table.HasCheckConstraint("ck_report_template_mark_kind", "kind IN ('program', 'own', 'empty')");
            table.HasCheckConstraint("ck_report_template_mark_text", "(kind = 'own') = (text IS NOT NULL)");
        });

        builder.HasKey(m => m.Id).HasName("pk_report_template_mark");
        builder.Property(m => m.Id).UseIdentityByDefaultColumn();
        builder.Property(m => m.ReportTemplateId).IsRequired();
        builder.Property(m => m.State)
            .HasMaxLength(32)
            .IsRequired()
            .HasConversion(v => StateCode(v), code => StateFromCode(code));
        builder.Property(m => m.Kind)
            .HasMaxLength(8)
            .IsRequired()
            .HasConversion(v => KindCode(v), code => KindFromCode(code));
        builder.Property(m => m.Text).HasMaxLength(ReportTemplate.MaxMarkTextLength);
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();

        builder.HasIndex(m => new { m.ReportTemplateId, m.State })
            .IsUnique()
            .HasDatabaseName("uq_report_template_mark_template_state");
    }

    private static string StateCode(ReportCellState value) => value switch
    {
        ReportCellState.TurnedInNotGraded => "turned_in_not_graded",
        ReportCellState.ReturnedWithoutGrade => "returned_without_grade",
        ReportCellState.TurnedIn => "turned_in",
        ReportCellState.Returned => "returned",
        ReportCellState.NotTurnedIn => "not_turned_in",
        ReportCellState.NotDueYet => "not_due_yet",
        ReportCellState.NotTurnedInNoDueDate => "not_turned_in_no_due_date",
        ReportCellState.NotAssigned => "not_assigned",
        ReportCellState.Unrecognised => "unrecognised",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static ReportCellState StateFromCode(string code) => code switch
    {
        "turned_in_not_graded" => ReportCellState.TurnedInNotGraded,
        "returned_without_grade" => ReportCellState.ReturnedWithoutGrade,
        "turned_in" => ReportCellState.TurnedIn,
        "returned" => ReportCellState.Returned,
        "not_turned_in" => ReportCellState.NotTurnedIn,
        "not_due_yet" => ReportCellState.NotDueYet,
        "not_turned_in_no_due_date" => ReportCellState.NotTurnedInNoDueDate,
        "not_assigned" => ReportCellState.NotAssigned,
        "unrecognised" => ReportCellState.Unrecognised,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string KindCode(ReportMarkKind value) => value switch
    {
        ReportMarkKind.Program => "program",
        ReportMarkKind.Own => "own",
        ReportMarkKind.Empty => "empty",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static ReportMarkKind KindFromCode(string code) => code switch
    {
        "program" => ReportMarkKind.Program,
        "own" => ReportMarkKind.Own,
        "empty" => ReportMarkKind.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
