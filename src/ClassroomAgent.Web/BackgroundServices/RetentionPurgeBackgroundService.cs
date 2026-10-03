using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Web.BackgroundServices;

/// <summary>
/// Runs the retention purge once shortly after start and then every 24 hours (US-037 spec FR-013, OD-001), never
/// alongside a synchronization run (FR-014, OD-002), stopping between units on shutdown (FR-015).
/// </summary>
/// <remarks>
/// The first run is due at once (I-2); each next one 24 hours after the previous run started (I-1). A purge that
/// falls due while a synchronization run holds the gate waits for it and says so once. The use case cannot log
/// (OD-007), so its outcome is written here: start, completion with the counts, and one <c>Error</c> per failed unit
/// with internal identifiers and exception type names only (SC-10). Nothing a run does stops the service.
/// </remarks>
public sealed partial class RetentionPurgeBackgroundService(
    IServiceScopeFactory scopes,
    SyncRunCoordinator runs,
    TimeProvider timeProvider,
    RetentionSettings retention,
    ILogger<RetentionPurgeBackgroundService> logger) : BackgroundService
{
    /// <summary>The interval of spec FR-013, counted from the previous run's start (I-1).</summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var dueAt = timeProvider.GetUtcNow();
            while (true)
            {
                await WaitUntilAsync(dueAt, stoppingToken);
                await AcquireGateAsync(stoppingToken);
                var startedAt = timeProvider.GetUtcNow();
                try
                {
                    await RunOnceAsync(startedAt, stoppingToken);
                }
                finally
                {
                    runs.PurgeCompleted();
                }

                dueAt = startedAt + Interval;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown: no new run starts; a unit in flight has committed or rolled back whole (FR-015).
        }
    }

    private async Task WaitUntilAsync(DateTimeOffset dueAt, CancellationToken stoppingToken)
    {
        var remaining = dueAt - timeProvider.GetUtcNow();
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, timeProvider, stoppingToken);
        }
    }

    /// <summary>FR-014: take the gate shared with synchronization, waiting for a run in progress to end.</summary>
    private async Task AcquireGateAsync(CancellationToken stoppingToken)
    {
        var logged = false;
        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();

            // Read the signal before trying, so an end that happens between the two is not missed.
            var changed = runs.Changed;
            if (runs.TryStartPurge())
            {
                return;
            }

            if (!logged)
            {
                LogWaitingForSync(logger);
                logged = true;
            }

            await changed.WaitAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(DateTimeOffset startedAt, CancellationToken stoppingToken)
    {
        try
        {
            // Inside the handler (US-037 OD-008, security review N-3): nothing a run does stops the service or the host.
            LogStarted(logger, RetentionRule.Cutoff(startedAt, retention.Years));
            await using var scope = scopes.CreateAsyncScope();
            var outcome = await scope.ServiceProvider
                .GetRequiredService<RunRetentionPurgeUseCase>()
                .ExecuteAsync(stoppingToken);

            foreach (var failure in outcome.Failures)
            {
                LogUnitFailed(logger, failure.Step.ToString(), failure.CourseId, failure.ExceptionType);
            }

            LogCompleted(
                logger,
                outcome.Cutoff,
                outcome.Counts.Courses,
                outcome.Counts.LeaverMemberships,
                outcome.Counts.Participants,
                outcome.Counts.Accounts,
                outcome.Counts.AuditRows,
                outcome.Failures.Count);
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            // A failure outside every unit (the first read, the scope): the next run tries again.
            LogRunFailed(logger, failure.GetType().Name);
        }
    }

    [LoggerMessage(
        EventId = 5201,
        EventName = "RetentionPurgeStarted",
        Level = LogLevel.Information,
        Message = "Retention purge started, cutoff {Cutoff}")]
    private static partial void LogStarted(ILogger logger, DateTimeOffset cutoff);

    [LoggerMessage(
        EventId = 5202,
        EventName = "RetentionPurgeCompleted",
        Level = LogLevel.Information,
        Message = "Retention purge completed, cutoff {Cutoff}: {Courses} courses, {LeaverMemberships} leaver memberships, "
                  + "{Participants} participants, {Accounts} accounts, {AuditRows} audit rows removed; {FailedUnits} failed units")]
    private static partial void LogCompleted(
        ILogger logger,
        DateTimeOffset cutoff,
        int courses,
        int leaverMemberships,
        int participants,
        int accounts,
        int auditRows,
        int failedUnits);

    [LoggerMessage(
        EventId = 5203,
        EventName = "RetentionPurgeUnitFailed",
        Level = LogLevel.Error,
        Message = "Retention purge step {Step} failed for course {CourseId} with {ExceptionType}; it was rolled back and the next run retries it")]
    private static partial void LogUnitFailed(ILogger logger, string step, long? courseId, string exceptionType);

    [LoggerMessage(
        EventId = 5204,
        EventName = "RetentionPurgeWaitingForSync",
        Level = LogLevel.Information,
        Message = "Retention purge is due and waits for the synchronization run in progress")]
    private static partial void LogWaitingForSync(ILogger logger);

    [LoggerMessage(
        EventId = 5205,
        EventName = "RetentionPurgeRunFailed",
        Level = LogLevel.Error,
        Message = "Retention purge run failed with {ExceptionType}; the next run tries again")]
    private static partial void LogRunFailed(ILogger logger, string exceptionType);
}
