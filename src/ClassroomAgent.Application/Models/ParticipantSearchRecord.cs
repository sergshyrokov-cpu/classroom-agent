namespace ClassroomAgent.Application.Models;

/// <summary>US-038 spec FR-002: one participant found, with the number of courses per role.</summary>
public sealed record ParticipantSearchRecord(
    long ParticipantId,
    string? FullName,
    string? Email,
    int TeacherCourses,
    int StudentCourses);
