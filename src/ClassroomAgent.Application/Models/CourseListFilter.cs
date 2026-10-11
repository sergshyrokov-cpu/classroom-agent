using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// The validated filter of the course list (US-020 entity model §2): <see cref="State"/> null means <c>all</c>;
/// <see cref="SearchText"/> is the trimmed text or null. Built only by <c>GetCourseListQuery</c> after validation.
/// </summary>
public sealed record CourseListFilter(CourseState? State, long? TeacherParticipantId, string? SearchText);
