namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportHeader</c>.</summary>
public sealed record ReportHeader(
    bool TemplateIsBuiltIn,
    string? TemplateName,
    string CourseName,
    string? CourseSection,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<PersonName> Teachers);
