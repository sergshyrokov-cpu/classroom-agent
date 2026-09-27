namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// The status of a synchronization run (US-013 entity model §2; <c>trebovaniya.md</c> §3 —
/// "running/completed/failed"). Exactly three values: the "never synchronized" state is the absence of the
/// <c>sync_state</c> row, not a fourth member (US-013 spec I-2), and a run interrupted by a stopped process
/// leaves the row at <see cref="Running"/> rather than gaining a status of its own (spec I-3).
/// </summary>
public enum SyncRunStatus
{
    Running,
    Completed,
    Failed,
}
