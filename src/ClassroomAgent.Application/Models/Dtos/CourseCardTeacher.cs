namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-021 <c>CourseCardTeacher</c>: a current teacher, the owner marked (spec FR-004).</summary>
public sealed record CourseCardTeacher(long ParticipantId, CourseListPersonName Name, string? Email, bool IsOwner);
