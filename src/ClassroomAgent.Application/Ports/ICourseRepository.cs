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

    /// <summary>
    /// US-059 entity model §6: every stored course, tracked for a change — read once per run at the end of a complete
    /// course list (a few hundred rows per school).
    /// </summary>
    Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// US-062 entity model §2.1: row-locked read (FOR UPDATE) of the given courses; call only inside
    /// ExecuteInTransactionAsync (db-design §4). Unknown ids are simply absent from the result.
    /// </summary>
    Task<IReadOnlyList<Course>> GetForAbsenceConfirmationAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken);
}
