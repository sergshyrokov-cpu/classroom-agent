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
}
