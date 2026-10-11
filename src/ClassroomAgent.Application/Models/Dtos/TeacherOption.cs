namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi US-020 <c>TeacherOption</c>: a participant of the teacher choice; <see cref="DisambiguatingEmail"/> is set
/// only when another option renders the same label and this participant has an email (spec FR-005, I-3).
/// </summary>
public sealed record TeacherOption(long ParticipantId, CourseListPersonName Name, string? DisambiguatingEmail);
