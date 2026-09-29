using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The installation's <c>course_work</c> table (US-015 entity model §7). Stages changes; never saves — the use
/// case owns the transaction boundary through <see cref="IUnitOfWork"/> (AD-7).
/// </summary>
public interface ICourseWorkRepository
{
    /// <summary>
    /// The course's current items, so the upsert matches by <c>(resource, google_id)</c> within that course
    /// without a query per item.
    /// </summary>
    Task<IReadOnlyList<CourseWork>> GetByCourseAsync(long courseId, CancellationToken cancellationToken);

    void Add(CourseWork courseWork);
}
