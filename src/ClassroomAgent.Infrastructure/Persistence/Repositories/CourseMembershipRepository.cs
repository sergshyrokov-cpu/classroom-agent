using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's <c>course_membership</c> table (US-014 entity model §7). Stages changes; never saves
/// (AD-7).
/// </summary>
public sealed class CourseMembershipRepository(ClassroomAgentDbContext db) : ICourseMembershipRepository
{
    /// <summary>
    /// The course's current memberships, tracked: this run advances the ones it sees again and marks the ones it
    /// did not see off the roster (spec FR-009, FR-010).
    /// </summary>
    public async Task<IReadOnlyList<CourseMembership>> GetByCourseAsync(long courseId, CancellationToken cancellationToken) =>
        await db.CourseMemberships.Where(m => m.CourseId == courseId).ToListAsync(cancellationToken);

    public void Add(CourseMembership membership) => db.CourseMemberships.Add(membership);
}
