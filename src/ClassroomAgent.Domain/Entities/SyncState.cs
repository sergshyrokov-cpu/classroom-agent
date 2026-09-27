using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// The installation's record of background synchronization (US-013 entity model §1; <c>trebovaniya.md</c> §3):
/// the status of the current or last run, its identifier, its counter, the last error and the instant of the
/// last <b>successful</b> run. One per installation database (US-013 db-design §3).
/// </summary>
/// <remarks>
/// The entity holds no policy number and no clock — the interval never reaches it and every instant arrives as
/// an argument (AD-3, spec VR-006). A run is begun once and finished once; the invariants the check constraints
/// cannot express live here, and the one they cannot see at all is that <see cref="LastSuccessfulRunAt"/> is
/// written by <see cref="CompleteRun"/> alone (spec FR-008).
/// </remarks>
public sealed class SyncState
{
    /// <summary>The longest error the row stores; a longer message is truncated (db-design §3.3).</summary>
    public const int MaxErrorLength = 512;

    private SyncState()
    {
    }

    public long Id { get; private set; }

    public SyncRunStatus Status { get; private set; }

    public Guid RunId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Null exactly while the run is in progress.</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    public int ProcessedCount { get; private set; }

    /// <summary>Category and short message of the last failure; null unless the run failed.</summary>
    public string? LastError { get; private set; }

    /// <summary>Null until a run completes; never written by a failing run (spec FR-008).</summary>
    public DateTimeOffset? LastSuccessfulRunAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsRunning => Status == SyncRunStatus.Running;

    /// <summary>The first row of an installation that has never synchronized (spec I-2).</summary>
    public static SyncState BeginFirstRun(Guid runId, DateTimeOffset startedAt)
    {
        var state = new SyncState();
        state.Begin(runId, startedAt);
        return state;
    }

    /// <summary>A new run on the existing row: the last successful run is kept, the counter is reset.</summary>
    public void BeginRun(Guid runId, DateTimeOffset startedAt) => Begin(runId, startedAt);

    /// <summary>The only writer of <see cref="LastSuccessfulRunAt"/> (spec FR-008).</summary>
    public void CompleteRun(DateTimeOffset finishedAt, int processedCount)
    {
        Finish(finishedAt, processedCount);
        Status = SyncRunStatus.Completed;
        LastError = null;
        LastSuccessfulRunAt = finishedAt;
    }

    /// <summary>Records the failure and leaves <see cref="LastSuccessfulRunAt"/> untouched (spec FR-008).</summary>
    public void FailRun(DateTimeOffset finishedAt, int processedCount, string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("A failed run carries a diagnosis.", nameof(error));
        }

        Finish(finishedAt, processedCount);
        Status = SyncRunStatus.Failed;

        // Truncated, not refused: a verbose diagnosis must not make the failure fail again at the commit
        // (db-design §3.3).
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
    }

    private void Begin(Guid runId, DateTimeOffset startedAt)
    {
        RunId = runId;
        StartedAt = startedAt;
        Status = SyncRunStatus.Running;
        FinishedAt = null;
        ProcessedCount = 0;
        LastError = null;
    }

    private void Finish(DateTimeOffset finishedAt, int processedCount)
    {
        if (!IsRunning)
        {
            throw new ArgumentException("A run is finished once.", nameof(finishedAt));
        }

        if (processedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processedCount), processedCount, null);
        }

        if (finishedAt < StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(finishedAt), finishedAt, null);
        }

        FinishedAt = finishedAt;
        ProcessedCount = processedCount;
    }
}
