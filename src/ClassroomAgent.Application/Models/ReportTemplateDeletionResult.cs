using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>The delete confirmation page (outcome <c>Succeeded</c>) or the reason it cannot be shown.</summary>
public sealed record ReportTemplateDeletionResult(
    ReportTemplateOutcome Outcome,
    ReportTemplateDeletePageModel? Page);
