namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportHeader</c>; <see cref="Notice"/> is the course's header line (US-060 spec FR-003).</summary>
public sealed record ReportHeader(
    bool TemplateIsBuiltIn,
    string? TemplateName,
    string CourseName,
    string? CourseSection,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<PersonName> Teachers,
    CourseDataNotice? Notice = null);
