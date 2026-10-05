using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Rules;

/// <summary>The built-in templates, defined in code and never stored (US-027 spec FR-006, I-1).</summary>
public static class BuiltInReportTemplates
{
    /// <summary>The own text "Academic journal" shows for a coursework item not assigned (spec FR-006, OD-003 a).</summary>
    public const string NotAssignedText = "—";

    /// <summary>
    /// "Academic journal" (spec FR-006, OD-003 a): short view, materials hidden, the 12-point preset, 2 hours, a dash
    /// for "not assigned", every other state empty, the late mark not shown.
    /// </summary>
    public static ReportTemplateSettings AcademicJournal { get; } = new(
        ReportView.Short,
        HideMaterials: true,
        HoursPerLesson: 2,
        ReportScaleMode.Ranges,
        TwelvePointScale.Rows,
        Enum.GetValues<ReportCellState>().ToDictionary(
            state => state,
            state => state == ReportCellState.NotAssigned
                ? new ReportMark(ReportMarkKind.Own, NotAssignedText)
                : new ReportMark(ReportMarkKind.Empty, null)),
        new ReportLateMark(ReportLateMarkKind.Hidden, null),
        ReportNameSource.Profile);
}
