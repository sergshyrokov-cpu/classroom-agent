using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>US-061 spec FR-003.2: a marked course of the course-choice page, its mark and the mark's instant.</summary>
public sealed record MeetCourseAbsenceRecord(long CourseId, CourseAbsenceMark Mark, DateTimeOffset Since);
