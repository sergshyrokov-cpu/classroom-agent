using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The installation's sync_skipped_course table (US-058 entity model §4). Stages changes; never saves —
/// the use case owns the transaction boundary through IUnitOfWork (AD-7).
/// </summary>
/// <remarks>
/// Compile-only skeleton created at TEST_WRITING (standing rule); IMPLEMENTATION owns it from here.
/// </remarks>
public interface ISyncSkippedCourseRepository
{
    /// <summary>Every record, tracked for a change — read once per run (the table holds a handful of rows).</summary>
    Task<IReadOnlyList<SyncSkippedCourse>> GetAllAsync(CancellationToken cancellationToken);

    void Add(SyncSkippedCourse skippedCourse);

    void Remove(SyncSkippedCourse skippedCourse);
}
