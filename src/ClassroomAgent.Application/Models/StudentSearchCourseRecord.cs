namespace ClassroomAgent.Application.Models;

/// <summary>US-071 entity model §2: a course of a found student, with what the BR-068 mark needs.</summary>
public sealed record StudentSearchCourseRecord(
    long ParticipantId,
    long CourseId,
    string Name,
    string? Section,
    bool OnRoster,
    bool SeenOnce,
    DateTimeOffset LastSeenAt);
