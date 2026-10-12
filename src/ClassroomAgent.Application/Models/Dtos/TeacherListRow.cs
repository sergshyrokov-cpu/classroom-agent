namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi US-022 <c>TeacherListRow</c>: one listed teacher — the name rule, the email as stored, the number of courses
/// of the chosen status, and the link to the course list built by <c>CourseListUrl</c> (spec FR-003, FR-007).
/// </summary>
public sealed record TeacherListRow(
    long ParticipantId, CourseListPersonName Name, string? Email, int CourseCount, string CoursesUrl);
