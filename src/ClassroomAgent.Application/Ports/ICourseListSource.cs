using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The course list's reads (US-020 entity model §2, db-design §3): untracked, one round trip per call, ordering in the
/// database. Nothing is written and nothing personal is logged.
/// </summary>
public interface ICourseListSource
{
    /// <summary>Q1 — participants with a current-teacher membership in any course, ordered by label key, then id.</summary>
    Task<IReadOnlyList<CourseListPersonRecord>> GetTeacherChoiceAsync(CancellationToken cancellationToken);

    /// <summary>Q2 — number of courses matching the filter.</summary>
    Task<int> CountCoursesAsync(CourseListFilter filter, CancellationToken cancellationToken);

    /// <summary>Q3 — whether any course is stored.</summary>
    Task<bool> AnyCourseStoredAsync(CancellationToken cancellationToken);

    /// <summary>Q4 — the courses matching the filter in list order, skipping offset, at most limit.</summary>
    Task<IReadOnlyList<CourseListCourseRecord>> GetCoursePageAsync(
        CourseListFilter filter, int offset, int limit, CancellationToken cancellationToken);

    /// <summary>Q5 — current teachers of the given courses, ordered by course, label key, participant id.</summary>
    Task<IReadOnlyList<CourseListTeacherRecord>> GetCurrentTeachersAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken);

    /// <summary>Q6 — current-student count of each given course that has at least one; a missing course means 0.</summary>
    Task<IReadOnlyList<CourseListStudentCountRecord>> GetStudentCountsAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken);
}
