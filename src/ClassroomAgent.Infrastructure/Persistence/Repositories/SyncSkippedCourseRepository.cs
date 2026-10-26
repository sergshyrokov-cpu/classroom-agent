using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's <c>sync_skipped_course</c> table (US-058 entity model §4). Stages changes; never saves (AD-7).
/// </summary>
public sealed class SyncSkippedCourseRepository(ClassroomAgentDbContext db) : ISyncSkippedCourseRepository
{
    /// <summary>Tracked, so a repeated skip and a removal are saved by the unit of work.</summary>
    public async Task<IReadOnlyList<SyncSkippedCourse>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.SyncSkippedCourses.ToListAsync(cancellationToken);

    public void Add(SyncSkippedCourse skippedCourse) => db.SyncSkippedCourses.Add(skippedCourse);

    public void Remove(SyncSkippedCourse skippedCourse) => db.SyncSkippedCourses.Remove(skippedCourse);
}
