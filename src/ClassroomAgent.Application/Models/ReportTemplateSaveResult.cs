using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>The end of a create or change: the template id on success, the form to re-render on <c>FieldsInvalid</c>.</summary>
public sealed record ReportTemplateSaveResult(
    ReportTemplateOutcome Outcome,
    long? TemplateId,
    ReportTemplateFormPageModel? Form);
