using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Validation;

/// <summary>
/// A posted template form that has the shape of api-design §2.7 (US-027 spec VR-002, VR-003, VR-004): every enumerated
/// value is a known one, every state is present, no field is repeated. Texts are trimmed but not yet validated; the
/// numbers are still text, so a refused form can hand back exactly what was entered.
/// </summary>
internal sealed record ParsedReportTemplateForm(
    string Name,
    ReportView View,
    bool HideMaterials,
    string Hours,
    ReportScaleMode ScaleMode,
    IReadOnlyList<ParsedReportTemplateForm.ScaleRowEntry> ScaleRows,
    IReadOnlyDictionary<ReportCellState, ParsedReportTemplateForm.MarkEntry> Marks,
    ParsedReportTemplateForm.LateMarkEntry LateMark)
{
    /// <summary>The entered values, for the form of a refusal.</summary>
    public ReportTemplateFormValues ToValues() => new(
        Name,
        View == ReportView.Full ? "full" : "short",
        HideMaterials ? "true" : "false",
        ScaleMode == ReportScaleMode.Ranges ? "ranges" : "none",
        Hours,
        ScaleMode == ReportScaleMode.Ranges
            ? ScaleRows.Select(r => new ReportTemplateScaleRowValues(r.From, r.To, r.Label)).ToList()
            : [],
        Marks.ToDictionary(
            m => m.Key,
            m => new ReportTemplateMarkValues(
                m.Value.Kind switch
                {
                    ReportMarkKind.Own => "own",
                    ReportMarkKind.Empty => "empty",
                    _ => "program",
                },
                m.Value.Kind == ReportMarkKind.Own ? m.Value.Text : null)),
        new ReportTemplateMarkValues(
            LateMark.Kind switch
            {
                ReportLateMarkKind.Own => "own",
                ReportLateMarkKind.Hidden => "hidden",
                _ => "program",
            },
            LateMark.Kind == ReportLateMarkKind.Own ? LateMark.Text : null));

    /// <summary>One submitted scale row; <paramref name="Number"/> is its submitted index plus one.</summary>
    internal sealed record ScaleRowEntry(int Number, string From, string To, string Label);

    internal sealed record MarkEntry(ReportMarkKind Kind, string Text);

    internal sealed record LateMarkEntry(ReportLateMarkKind Kind, string Text);
}
