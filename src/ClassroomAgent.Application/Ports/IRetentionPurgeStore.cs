using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The retention purge's reads and set-based deletes (US-037 entity model §3, db-design §3 … §6) — the only place in
/// the installation that deletes teaching data, accounts or audit rows (PC-11, SC-11). It never opens a transaction:
/// the use case owns every boundary through <see cref="IUnitOfWork"/> (AD-7).
/// </summary>
public interface IRetentionPurgeStore
{
    /// <summary>Every stored course with the raw maxima of its dates (db-design §4); the rule is applied by the caller.</summary>
    Task<IReadOnlyList<CourseActivityDates>> GetCourseActivityDatesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one course child first: the participations and meetings reached through its linked codes, its links
    /// (US-032 db-design §6), submissions, coursework, memberships, the course (spec FR-003).
    /// </summary>
    Task<CourseDeletionCounts> DeleteCourseAsync(long courseId, CancellationToken cancellationToken);

    /// <summary>Courses holding at least one off-roster membership last seen before the cutoff (spec FR-005).</summary>
    Task<IReadOnlyList<long>> GetCourseIdsWithExpiredLeaversAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a course's expired leavers with their submissions in it and — US-032 db-design §6 — their participations
    /// (email matched case-insensitively) in meetings reached through the course's linked codes.
    /// </summary>
    Task<LeaverDeletionCounts> DeleteExpiredLeaversAsync(long courseId, DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>Deletes every participant no membership references (spec FR-006); returns how many.</summary>
    Task<int> DeleteOrphanedParticipantsAsync(CancellationToken cancellationToken);

    /// <summary>Deletes every account whose last sign-in, or creation, is before the cutoff (spec FR-007).</summary>
    Task<int> DeleteExpiredAccountsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>Deletes every audit row that occurred before the cutoff (spec FR-008) — the only audit deletion.</summary>
    Task<int> DeleteAuditEventsOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>US-031 db-design §6: up to <paramref name="batchSize"/> meetings that started before the cutoff, by id.</summary>
    Task<IReadOnlyList<long>> GetExpiredMeetSessionIdsAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken);

    /// <summary>US-031 db-design §6: deletes those meetings' participations, then the meetings; returns both counts.</summary>
    Task<(int Sessions, int Participations)> DeleteMeetSessionsAsync(
        IReadOnlyCollection<long> sessionIds,
        CancellationToken cancellationToken);

    /// <summary>US-032 db-design §6: deletes every "not a course" mark whose code has no meeting left; returns how many.</summary>
    Task<int> DeleteOrphanedNotACourseMarksAsync(CancellationToken cancellationToken);
}
