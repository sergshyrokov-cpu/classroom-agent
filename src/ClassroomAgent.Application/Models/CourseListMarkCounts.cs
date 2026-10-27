namespace ClassroomAgent.Application.Models;

/// <summary>
/// US-059 FR-007: what the end of a course list read to its end did to the stored courses — counts only (SC-10). A list
/// that came back empty while courses are stored marks nothing and carries the number of stored courses instead
/// (FR-005.2, FR-007.2).
/// </summary>
public sealed record CourseListMarkCounts(
    int MarkedNotInClassroom,
    int MarkedNotVisibleToTechnicalAccount,
    int Unmarked,
    int? EmptyListStoredCourses)
{
    /// <summary>FR-007.1: at least one course was newly marked or unmarked.</summary>
    public bool AnyChange => MarkedNotInClassroom > 0 || MarkedNotVisibleToTechnicalAccount > 0 || Unmarked > 0;
}
