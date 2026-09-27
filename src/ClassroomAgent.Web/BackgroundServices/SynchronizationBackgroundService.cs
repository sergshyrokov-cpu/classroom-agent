using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.Configuration;

namespace ClassroomAgent.Web.BackgroundServices;

/// <summary>
/// The synchronization schedule (US-013 spec FR-003, FR-004, FR-010, FR-011, FR-012; AD-5, NFR-001): a run
/// becomes due one configured interval after the completion of the previous one, one run happens at a time, and
/// the first run of a process waits for the first legitimacy determination. Nothing a run does stops the service
/// or the host.
/// </summary>
/// <remarks>
/// The interval is the same after a success and after a failure (OD-003): retrying the call that actually failed
/// is US-017's business. A run skipped because of read-only mode or a missing connection writes nothing and says
/// so in one line (OD-005) — the closed list of BR-026 permits no synchronization write.
/// </remarks>
public sealed partial class SynchronizationBackgroundService(
    IServiceScopeFactory scopes,
    SyncRunCoordinator runs,
    SynchronizationServiceMemory memory,
    LegitimacyCheckMemory legitimacy,
    SyncScheduleSettings schedule,
    TimeProvider timeProvider,
    ILogger<SynchronizationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        memory.MarkRunning();
        try
        {
            // The first run waits for the first legitimacy determination: an installation whose check has not
            // answered yet is read-only by BR-025, so a run at the instant of start would always be skipped
            // (spec FR-011, OD-006). The wait is bounded, so a stalled check cannot stop synchronization for
            // the life of the process (spec I-5).
            await WaitForFirstLegitimacyDeterminationAsync(stoppingToken);

            while (true)
            {
                if (runs.TryStartScheduledRun() || runs.TryStartRequestedRun())
                {
                    await RunOnceAsync(stoppingToken);
                }

                var dueAt = timeProvider.GetUtcNow() + schedule.Interval;
                await WaitForNextRunAsync(dueAt, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown: no new run starts, and the stop is not a failed run (spec FR-010).
        }
        finally
        {
            memory.MarkStopped();
        }
    }

    /// <summary>
    /// Waits until the next run may start: the scheduled instant, or an out-of-schedule request US-019 will
    /// make. Returns with nothing started — the caller asks the coordinator, which decides atomically.
    /// </summary>
    private async Task WaitForNextRunAsync(DateTimeOffset dueAt, CancellationToken stoppingToken)
    {
        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();
            if (timeProvider.GetUtcNow() >= dueAt)
            {
                return;
            }

            var changed = runs.Changed;
            var remaining = dueAt - timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            // Cancelled on the way out so the timer leaves the clock: a stale one would fire a later advance.
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            try
            {
                await Task.WhenAny(changed, Task.Delay(remaining, timeProvider, wait.Token));
            }
            finally
            {
                await wait.CancelAsync();
            }

            if (runs.IsRequested)
            {
                return;
            }
        }
    }

    private async Task WaitForFirstLegitimacyDeterminationAsync(CancellationToken stoppingToken)
    {
        var giveUpAt = timeProvider.GetUtcNow() + schedule.Interval;
        while (legitimacy.LastOutcome is null && timeProvider.GetUtcNow() < giveUpAt)
        {
            stoppingToken.ThrowIfCancellationRequested();

            // The signal, not a poll: the clock is injected, and a poll would need it to move — which nothing
            // does while the installation is only waiting to learn whether it is legitimate (spec I-5).
            var changed = legitimacy.Changed;
            if (legitimacy.LastOutcome is not null)
            {
                return;
            }

            using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            try
            {
                await Task.WhenAny(changed, Task.Delay(giveUpAt - timeProvider.GetUtcNow(), timeProvider, wait.Token));
            }
            finally
            {
                await wait.CancelAsync();
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        var runId = Guid.NewGuid();
        try
        {
            // Every line of this run carries its identifier, so one run reads end to end (DC-10).
            using var scopeOfRun = logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId });
            SynchronizationRunOutcome outcome;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                LogRunStarted(logger, runId);
                outcome = await scope.ServiceProvider
                    .GetRequiredService<RunSynchronizationUseCase>()
                    .ExecuteAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Only the type: a message may carry remote detail (SC-10). A failing run never stops the
                // service (spec FR-008).
                LogRunException(logger, runId, exception.GetType().Name);
                return;
            }

            LogOutcome(runId, outcome);
        }
        finally
        {
            runs.RunCompleted();
        }
    }

    private void LogOutcome(Guid runId, SynchronizationRunOutcome outcome)
    {
        if (outcome.ReadOnlyReason is { } reason)
        {
            LogRunSkipped(logger, runId, "ReadOnly:" + reason);
        }
        else if (outcome.ConnectionState is { } state)
        {
            LogRunSkipped(logger, runId, "Connection:" + state);
        }
        else if (outcome.Failed)
        {
            LogRunFailed(logger, runId, outcome.Error ?? string.Empty);
        }
        else
        {
            LogRunCompleted(logger, runId, outcome.ProcessedCount ?? 0);
        }
    }

    [LoggerMessage(
        EventId = 5121,
        EventName = "SyncRunStarted",
        Level = LogLevel.Information,
        Message = "Synchronization run {RunId} started")]
    private static partial void LogRunStarted(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 5122,
        EventName = "SyncRunCompleted",
        Level = LogLevel.Information,
        Message = "Synchronization run {RunId} completed, {ProcessedCount} processed")]
    private static partial void LogRunCompleted(ILogger logger, Guid runId, int processedCount);

    [LoggerMessage(
        EventId = 5123,
        EventName = "SyncRunFailed",
        Level = LogLevel.Error,
        Message = "Synchronization run {RunId} failed: {Diagnosis}")]
    private static partial void LogRunFailed(ILogger logger, Guid runId, string diagnosis);

    [LoggerMessage(
        EventId = 5124,
        EventName = "SyncRunSkipped",
        Level = LogLevel.Information,
        Message = "Synchronization run {RunId} skipped: {Reason}")]
    private static partial void LogRunSkipped(ILogger logger, Guid runId, string reason);

    [LoggerMessage(
        EventId = 5125,
        EventName = "SyncRunException",
        Level = LogLevel.Error,
        Message = "Synchronization run {RunId} failed with an unexpected {ExceptionType}")]
    private static partial void LogRunException(ILogger logger, Guid runId, string exceptionType);
}
