namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One row of the Lesson topics part (spec FR-005.6); <see cref="NotInClassroom"/> is US-060 spec FR-005.3.</summary>
public sealed record LessonTopicRow(
    DateOnly LessonDate,
    string Title,
    int Hours,
    bool NotInClassroom = false);
