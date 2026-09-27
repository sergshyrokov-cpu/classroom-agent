using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The installation's <c>course_membership</c> table (US-014 entity model §7). Stages changes; never saves —
/// the use case owns the transaction boundary through <see cref="IUnitOfWork"/> (AD-7).
/// </summary>
/// <remarks>
/// Compile-only skeleton created at TEST_WRITING under US-014 OD-012; IMPLEMENTATION owns it from here.
/// </remarks>
public interface ICourseMembershipRepository
{
    /// <summary>The course's current memberships, needed to decide which ones this run did not see (FR-010).</summary>
    Task<IReadOnlyList<CourseMembership>> GetByCourseAsync(long courseId, CancellationToken cancellationToken);

    void Add(CourseMembership membership);
}
