namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>GradingColumn</c>.</summary>
public sealed record GradingColumn(
    DateOnly LessonDate,
    string Title,
    bool IsMaterial);
