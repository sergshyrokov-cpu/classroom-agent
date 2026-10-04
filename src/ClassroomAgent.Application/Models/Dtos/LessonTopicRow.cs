namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One row of the Lesson topics part (spec FR-005.6).</summary>
public sealed record LessonTopicRow(
    DateOnly LessonDate,
    string Title,
    int Hours);
