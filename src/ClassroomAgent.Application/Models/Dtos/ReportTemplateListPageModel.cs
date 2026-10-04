namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportTemplateListPageModel</c>.</summary>
public sealed record ReportTemplateListPageModel(
    IReadOnlyList<ReportTemplateListItem> Templates,
    ReportTemplateConfirmationKey? ConfirmationKey,
    ReportTemplateReferenceMessageKey? MessageKey);
