namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportGrade</c>.</summary>
public sealed record ReportGrade(
    ReportGradeKind Kind,
    string? Label,
    decimal? Points,
    decimal? MaxPoints);
