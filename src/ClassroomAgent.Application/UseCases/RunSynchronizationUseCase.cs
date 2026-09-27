using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// A synchronization run (US-013 spec FR-005): (1) the read-only guard runs first, and a
/// <see cref="ReadOnlyModeException"/> becomes the skipped-read-only outcome; (2) only then the saved connection
/// is read, and a connection that is not usable becomes the skipped-connection outcome; (3) only then the run
/// itself happens.
/// </summary>
/// <remarks>
/// A skipped run writes <b>nothing</b>: a synchronization write is not on the BR-026 closed list, so the row is
/// neither created nor updated and no commit happens (spec FR-007, SC-5). Because the guard is called first,
/// this use case is a protected write path rather than a permitted service write, and
/// <c>PermittedServiceWrites</c> does not grow (spec FR-006).
/// <para>
/// The pipeline the run executes is empty in this Story (OD-001): the run completes with the counter at zero,
/// which is the expected outcome and not a defect (spec I-1). US-014 adds the first step here, and whatever
/// Google port it introduces is already behind the guard.
/// </para>
/// </remarks>
public sealed class RunSynchronizationUseCase(
    ISyncStateRepository syncStates,
    GetWorkspaceConnectionQuery connectionQuery,
    IReadOnlyModeGuard readOnlyMode,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>The operation name the read-only refusal carries (US-007 spec VR-001).</summary>
    public const string Operation = "Sync.Run";

    public async Task<SynchronizationRunOutcome> ExecuteAsync(Guid runId, CancellationToken cancellationToken)
    {
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException refusal)
        {
            return SynchronizationRunOutcome.SkippedReadOnly(refusal.Reason);
        }

        var connection = await connectionQuery.ExecuteAsync(cancellationToken);
        if (!connection.IsUsable)
        {
            return SynchronizationRunOutcome.SkippedConnection(connection.State);
        }

        var state = await BeginAsync(runId, cancellationToken);
        try
        {
            // The pipeline of this Story has no step (OD-001). US-014 adds the first one, and the counter it
            // reports replaces the zero below.
            var processedCount = 0;
            state.CompleteRun(timeProvider.GetUtcNow(), processedCount);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return SynchronizationRunOutcome.Ran(runId, processedCount, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A host stop is not a failed run (spec FR-010): the row stays as it is and the next run replaces it.
            throw;
        }
        catch (Exception failure)
        {
            state.FailRun(timeProvider.GetUtcNow(), 0, Diagnosis(failure));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return SynchronizationRunOutcome.Ran(runId, 0, state.LastError);
        }
    }

    /// <summary>
    /// A category and a short sentence, never the exception's own message: SC-10 forbids a raw error text, and a
    /// Google error object would be the worst of them (spec FR-008, S-06).
    /// </summary>
    private static string Diagnosis(Exception failure) => "RunFailed:" + failure.GetType().Name;

    /// <summary>
    /// The row moves to <c>Running</c> and is committed before the run proceeds, so a reader sees the run in
    /// progress (spec FR-006). The first run of an installation creates the row; its absence until then is what
    /// "never synchronized" means (spec I-2).
    /// </summary>
    private async Task<SyncState> BeginAsync(Guid runId, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var state = await syncStates.GetAsync(cancellationToken);
        if (state is null)
        {
            state = SyncState.BeginFirstRun(runId, startedAt);
            syncStates.Add(state);
        }
        else
        {
            state.BeginRun(runId, startedAt);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return state;
    }
}
