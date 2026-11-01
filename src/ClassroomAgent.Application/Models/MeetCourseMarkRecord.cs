using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>US-061 spec FR-005: the stored mark of a course (US-059) and its last complete read, for the Meet line.</summary>
public sealed record MeetCourseMarkRecord(
    CourseAbsenceMark? AbsenceMark,
    DateTimeOffset? AbsentSince,
    DateTimeOffset? LastCompleteReadAt);
