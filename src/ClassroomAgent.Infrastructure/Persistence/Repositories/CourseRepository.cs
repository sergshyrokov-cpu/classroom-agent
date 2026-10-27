using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's <c>course</c> table (US-014 entity model §7). Stages changes; never saves (AD-7).
/// </summary>
public sealed class CourseRepository(ClassroomAgentDbContext db) : ICourseRepository
{
    /// <summary>Tracked, because the upsert's update half changes the row it finds (spec FR-008).</summary>
    public Task<Course?> GetByGoogleIdAsync(string googleId, CancellationToken cancellationToken) =>
        db.Courses.SingleOrDefaultAsync(c => c.GoogleId == googleId, cancellationToken);

    public void Add(Course course) => db.Courses.Add(course);

    /// <summary>
    /// US-018 entity model §4, db-design §6: one column, untracked, so the tracked load inside the course's
    /// transaction is unaffected. Null for an unknown course and for a course with no value alike.
    /// </summary>
    public Task<DateTimeOffset?> GetCompleteReadUpdateTimeAsync(string googleId, CancellationToken cancellationToken) =>
        db.Courses
            .AsNoTracking()
            .Where(c => c.GoogleId == googleId)
            .Select(c => c.CompleteReadUpdateTime)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>US-059 entity model §6: tracked, because the end of a complete list marks, unmarks and sights them.</summary>
    public async Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Courses.ToListAsync(cancellationToken);
}
