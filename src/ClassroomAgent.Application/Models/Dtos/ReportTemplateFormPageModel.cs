namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportTemplateFormPageModel</c>.</summary>
public sealed record ReportTemplateFormPageModel(
    ReportTemplateFormMode Mode,
    string? Reference,
    ReportTemplateFormValues Values,
    IReadOnlyList<ReportTemplateFieldError> FieldErrors,
    IReadOnlyList<ScaleRange> TwelvePointPreset);
