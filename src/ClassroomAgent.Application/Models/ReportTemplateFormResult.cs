using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>A template form to render (outcome <c>Succeeded</c>) or the reason it cannot be.</summary>
public sealed record ReportTemplateFormResult(
    ReportTemplateOutcome Outcome,
    ReportTemplateFormPageModel? Form);
