using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Rules;

/// <summary>US-059 FR-006.1…FR-006.3, I-1: the mark for a stored course the complete list did not yield.</summary>
public static class CourseAbsenceRule
{
    /// <summary>
    /// NotVisibleToTechnicalAccount when a change is recorded and the course was last listed strictly before it, or
    /// never listed (I-1); NotInClassroom otherwise, and always when no change is recorded.
    /// </summary>
    public static CourseAbsenceMark MarkFor(DateTimeOffset? lastListedAt, DateTimeOffset? technicalAccountChangedAt) =>
        technicalAccountChangedAt is { } changedAt && (lastListedAt is not { } listedAt || listedAt < changedAt)
            ? CourseAbsenceMark.NotVisibleToTechnicalAccount
            : CourseAbsenceMark.NotInClassroom;
}
