using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// US-028 entity model §3.2: what the report page's export button submits; <see cref="BlankForm"/> carries the limits of
/// the US-043 per-copy inputs (openapi <c>ExportActionChange</c>).
/// </summary>
public sealed record ExportAction(
    string Template,
    long CourseId,
    DateOnly From,
    DateOnly To,
    ReportNameSource Names,
    BlankFormInputs BlankForm);
