using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The course card's reads (US-021 entity model §2, db-design §2): untracked, one round trip per call. Nothing is written
/// and nothing personal is logged.
/// </summary>
public interface ICourseCardSource
{
    /// <summary>Q1 — the course's displayable details, or null when no course has this id.</summary>
    Task<CourseCardCourseRecord?> GetCourseAsync(long courseId, CancellationToken cancellationToken);

    /// <summary>
    /// Q2 — every membership of the course with its participant, in card order (db-design §2 Q2): named first, label
    /// under course_name_uk, email (none first), participant id.
    /// </summary>
    Task<IReadOnlyList<CourseCardMemberRecord>> GetMembersAsync(long courseId, CancellationToken cancellationToken);
}
