namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>LessonTopicsPart</c>.</summary>
public sealed record LessonTopicsPart(
    IReadOnlyList<LessonTopicRow> Rows);
