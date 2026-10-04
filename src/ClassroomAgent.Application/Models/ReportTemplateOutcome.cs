namespace ClassroomAgent.Application.Models;

/// <summary>How a template operation ends (US-027 api-design §2.5).</summary>
public enum ReportTemplateOutcome
{
    Succeeded,

    FieldsInvalid,

    FormMalformed,

    ReferenceMalformed,

    BuiltInNotChangeable,

    NotFound,
}
