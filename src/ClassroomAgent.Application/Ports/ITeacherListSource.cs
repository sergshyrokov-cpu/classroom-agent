using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The teacher list's reads (US-022 entity model §2, db-design §3): untracked, one round trip per call, filtering,
/// counting, ordering and paging in the database. Nothing is written and nothing personal is logged.
/// </summary>
public interface ITeacherListSource
{
    /// <summary>T1 — number of participants with at least one current teacher membership in a course of the state (null = all).</summary>
    Task<int> CountTeachersAsync(CourseState? state, CancellationToken cancellationToken);

    /// <summary>T2 — those participants with their course counts, in label-key order, skipping offset, at most limit.</summary>
    Task<IReadOnlyList<TeacherListRecord>> GetTeacherPageAsync(
        CourseState? state, int offset, int limit, CancellationToken cancellationToken);
}
