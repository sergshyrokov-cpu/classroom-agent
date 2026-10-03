using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's <c>course_work</c> table (US-015 entity model §7). Stages changes; never saves (AD-7).
/// </summary>
public sealed class CourseWorkRepository(ClassroomAgentDbContext db) : ICourseWorkRepository
{
    /// <summary>Tracked, so the upsert's update half changes the row it finds (spec FR-010).</summary>
    public async Task<IReadOnlyList<CourseWork>> GetByCourseAsync(long courseId, CancellationToken cancellationToken) =>
        await db.CourseWorks.Where(c => c.CourseId == courseId).ToListAsync(cancellationToken);

    public void Add(CourseWork courseWork) => db.CourseWorks.Add(courseWork);
}
