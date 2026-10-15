namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-038 <c>ParticipantSearchResult</c> (spec FR-002).</summary>
public sealed record ParticipantSearchResult(
    long ParticipantId,
    string? FullName,
    string? Email,
    int TeacherCourses,
    int StudentCourses);
