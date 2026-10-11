namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-021 <c>CourseCardPerson</c>: a current student (spec FR-003, FR-005).</summary>
public sealed record CourseCardPerson(long ParticipantId, CourseListPersonName Name, string? Email);
