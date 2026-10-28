namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportHeader</c>; <see cref="Notice"/> is the course's header line (US-060 spec FR-003);
/// <see cref="Stamp"/> its "generated" and "data from Google as of" lines (US-063 spec FR-003.2, FR-004, FR-005).</summary>
public sealed record ReportHeader(
    bool TemplateIsBuiltIn,
    string? TemplateName,
    string CourseName,
    string? CourseSection,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<PersonName> Teachers,
    CourseDataNotice? Notice = null,
    CourseDataStamp? Stamp = null);
