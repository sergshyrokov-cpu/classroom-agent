namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>GradingColumn</c>; <see cref="NotInClassroom"/> is US-060 spec FR-005.</summary>
public sealed record GradingColumn(
    DateOnly LessonDate,
    string Title,
    bool IsMaterial,
    bool NotInClassroom = false);
