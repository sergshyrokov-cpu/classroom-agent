namespace ClassroomAgent.Application.Models;

/// <summary>A listed teacher as the teacher list reads it (US-022 entity model §2): the name parts, the email, the course count.</summary>
public sealed record TeacherListRecord(
    long ParticipantId, string? Surname, string? GivenName, string? Email, int CourseCount);
