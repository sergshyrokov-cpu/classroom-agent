using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Every form value as a string (openapi <c>ReportTemplateForm</c>), in the form's own wording (<c>full</c>, <c>true</c>, <c>none</c>, <c>program</c> …); <c>Names</c> is empty after a rejected value (US-042 api-design §2.3).</summary>
public sealed record ReportTemplateFormValues(
    string Name,
    string View,
    string HideMaterials,
    string ScaleMode,
    string HoursPerLesson,
    IReadOnlyList<ReportTemplateScaleRowValues> Scale,
    IReadOnlyDictionary<ReportCellState, ReportTemplateMarkValues> Marks,
    ReportTemplateMarkValues LateMark,
    string Names);
