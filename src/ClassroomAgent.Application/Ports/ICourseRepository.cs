using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The installation's <c>course</c> table (US-014 entity model §7). Stages changes; never saves — the use case
/// owns the transaction boundary through <see cref="IUnitOfWork"/> (AD-7).
/// </summary>
public interface ICourseRepository
{
    /// <summary>The stored course, tracked for a change; null when this Google id has never been imported.</summary>
    Task<Course?> GetByGoogleIdAsync(string googleId, CancellationToken cancellationToken);

    void Add(Course course);

    /// <summary>
    /// US-018 entity model §4: the remembered complete-read updateTime of the course with this Google id, read
    /// untracked; null when the course is unknown or has no value (db-design §6, spec FR-001 (c)).
    /// </summary>
    Task<DateTimeOffset?> GetCompleteReadUpdateTimeAsync(string googleId, CancellationToken cancellationToken);
}
