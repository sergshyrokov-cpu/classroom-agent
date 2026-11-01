using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// US-061 spec FR-005.1: the Meet line of a marked course — its mark with <see cref="Since"/>, and the date its roster is
/// as of (<see cref="RosterAsOf"/>; null when no complete read is recorded, OD-001 a). Dates of the school's zone (BR-053).
/// </summary>
public sealed record MeetCourseNotice(CourseAbsenceMark Mark, DateOnly Since, DateOnly? RosterAsOf);
