using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Validation;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>Template settings to the values a form shows, and the pages built from them (US-027 spec FR-007, FR-008).</summary>
internal static class ReportTemplateFormMapper
{
    /// <summary>FR-007: full view, materials shown, no conversion, 2 hours, every mark the program's; names from the profile (US-042 I-2).</summary>
    public static ReportTemplateFormValues Defaults() => new(
        string.Empty,
        "full",
        "false",
        "none",
        "2",
        [],
        Enum.GetValues<ReportCellState>().ToDictionary(s => s, _ => new ReportTemplateMarkValues("program", null)),
        new ReportTemplateMarkValues("program", null),
        NameSourceCode.Profile);

    public static ReportTemplateFormValues FromSettings(string name, ReportTemplateSettings settings) => new(
        name,
        settings.View == ReportView.Full ? "full" : "short",
        settings.HideMaterials ? "true" : "false",
        settings.ScaleMode == ReportScaleMode.Ranges ? "ranges" : "none",
        settings.HoursPerLesson.ToString(CultureInfo.InvariantCulture),
        settings.ScaleRows
            .OrderBy(r => r.FromPercent)
            .Select(r => new ReportTemplateScaleRowValues(
                r.FromPercent.ToString(CultureInfo.InvariantCulture),
                r.ToPercent.ToString(CultureInfo.InvariantCulture),
                r.Label))
            .ToList(),
        Enum.GetValues<ReportCellState>().ToDictionary(
            s => s,
            s =>
            {
                var mark = settings.Marks[s];
                return new ReportTemplateMarkValues(
                    mark.Kind switch
                    {
                        ReportMarkKind.Own => "own",
                        ReportMarkKind.Empty => "empty",
                        _ => "program",
                    },
                    mark.Kind == ReportMarkKind.Own ? mark.Text : null);
            }),
        new ReportTemplateMarkValues(
            settings.LateMark.Kind switch
            {
                ReportLateMarkKind.Own => "own",
                ReportLateMarkKind.Hidden => "hidden",
                _ => "program",
            },
            settings.LateMark.Kind == ReportLateMarkKind.Own ? settings.LateMark.Text : null),
        NameSourceCode.Of(settings.NameSource));

    public static ReportTemplateFormPageModel Page(
        ReportTemplateFormMode mode,
        string? reference,
        ReportTemplateFormValues values,
        IReadOnlyList<ReportTemplateFieldError> errors) => new(
        mode,
        reference,
        values,
        errors,
        TwelvePointScale.Rows.Select(r => new ScaleRange(r.FromPercent, r.ToPercent, r.Label)).ToList());
}
