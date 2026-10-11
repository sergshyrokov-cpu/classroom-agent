namespace ClassroomAgent.Application.Models;

/// <summary>A participant as the course list reads it (US-020 entity model §2): the FR-007 name parts and the email.</summary>
public sealed record CourseListPersonRecord(long ParticipantId, string? Surname, string? GivenName, string? Email);
