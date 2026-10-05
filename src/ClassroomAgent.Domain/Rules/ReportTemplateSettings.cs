using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// Everything a report template is (US-027 spec FR-003, entity model §1.2): what both a created template and the
/// built-in one hand the report builder. <see cref="ScaleRows"/> is empty exactly when the mode is
/// <see cref="ReportScaleMode.None"/>; <see cref="Marks"/> holds exactly the nine states.
/// </summary>
public sealed record ReportTemplateSettings(
    ReportView View,
    bool HideMaterials,
    int HoursPerLesson,
    ReportScaleMode ScaleMode,
    IReadOnlyList<ReportScaleRow> ScaleRows,
    IReadOnlyDictionary<ReportCellState, ReportMark> Marks,
    ReportLateMark LateMark,
    ReportNameSource NameSource = ReportNameSource.Profile);
