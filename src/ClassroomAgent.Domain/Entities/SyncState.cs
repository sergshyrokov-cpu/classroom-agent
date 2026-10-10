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
    /// <summary>The longest error the row stores (db-design §3.3); a diagnosis name is far shorter.</summary>
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

    /// <summary>The name of the <see cref="SyncDiagnosis"/> of the last failure; null unless the run failed.</summary>
    public string? LastError { get; private set; }

    /// <summary>Null until a run completes; never written by a failing run (spec FR-008).</summary>
    public DateTimeOffset? LastSuccessfulRunAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsRunning => Status == SyncRunStatus.Running;

    /// <summary>
    /// US-031 spec FR-007: the end of the last successful Meet pull; null = never. Written by
    /// <see cref="CompleteRun"/> alone, so a failed or cancelled run never moves it.
    /// </summary>
    public DateTimeOffset? MeetLoadedUpTo { get; private set; }

    /// <summary>US-031 spec FR-010, I-5: the step that stopped a failed run; null otherwise and on pre-Story rows.</summary>
    public SyncStep? FailedStep { get; private set; }

    /// <summary>The first row of an installation that has never synchronized (spec I-2).</summary>
    public static SyncState BeginFirstRun(Guid runId, DateTimeOffset startedAt)
    {
        var state = new SyncState();
        state.Begin(runId, startedAt);
        return state;
    }

    /// <summary>A new run on the existing row: the last successful run is kept, the counter is reset.</summary>
    public void BeginRun(Guid runId, DateTimeOffset startedAt) => Begin(runId, startedAt);

    /// <summary>
    /// The only writer of <see cref="LastSuccessfulRunAt"/> (US-013 spec FR-008) and of <see cref="MeetLoadedUpTo"/>
    /// (US-031 spec FR-007): a completed run is one whose Meet step finished. The watermark is never later than the
    /// finish and never earlier than the stored one (VR-002); a refused value leaves the row unchanged.
    /// </summary>
    public void CompleteRun(DateTimeOffset finishedAt, int processedCount, DateTimeOffset meetLoadedUpTo)
    {
        if (meetLoadedUpTo > finishedAt || (MeetLoadedUpTo is { } stored && meetLoadedUpTo < stored))
        {
            throw new ArgumentOutOfRangeException(nameof(meetLoadedUpTo), meetLoadedUpTo, null);
        }

        Finish(finishedAt, processedCount);
        Status = SyncRunStatus.Completed;
        LastError = null;
        FailedStep = null;
        LastSuccessfulRunAt = finishedAt;
        MeetLoadedUpTo = meetLoadedUpTo;
    }

    /// <summary>
    /// Records the failure with its diagnosis (US-017 spec FR-006, entity model §2) and the step that stopped the run
    /// (US-031 spec FR-010), and leaves <see cref="LastSuccessfulRunAt"/> and <see cref="MeetLoadedUpTo"/> untouched
    /// (US-013 spec FR-008, US-031 spec FR-007). Both are stored by name; a value an enum does not declare is refused
    /// and the state is unchanged.
    /// </summary>
    public void FailRun(DateTimeOffset finishedAt, int processedCount, SyncDiagnosis diagnosis, SyncStep step)
    {
        if (!Enum.IsDefined(diagnosis))
        {
            throw new ArgumentOutOfRangeException(nameof(diagnosis), diagnosis, null);
        }

        if (!Enum.IsDefined(step))
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, null);
        }

        Finish(finishedAt, processedCount);
        Status = SyncRunStatus.Failed;
        LastError = diagnosis.ToString();
        FailedStep = step;
    }

    private void Begin(Guid runId, DateTimeOffset startedAt)
    {
        RunId = runId;
        StartedAt = startedAt;
        Status = SyncRunStatus.Running;
        FinishedAt = null;
        ProcessedCount = 0;
        LastError = null;
        FailedStep = null;
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
