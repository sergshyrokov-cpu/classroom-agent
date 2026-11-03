namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-071 entity model §3: a found student with all the courses they are a student of.</summary>
public sealed record StudentSearchRow(
    long ParticipantId, string Label, string? Email, IReadOnlyList<StudentSearchCourse> Courses);
