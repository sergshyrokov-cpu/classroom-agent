using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;

namespace ClassroomAgent.Web.BackgroundServices;

/// <summary>
/// The legitimacy check schedule (US-005 spec FR-003, FR-010, FR-012; AD-5): the first check right after
/// start, then 6 hours after a successful check or 15 minutes after an unsuccessful one, counted from its
/// completion; one check at a time; waiting runs on the injected clock. Nothing a check does stops the
/// service or the host. After startup and after every check the read-only determination is re-evaluated and
/// a change of mode or of check result is logged once. Log lines carry categories, reasons and states only.
/// </summary>
public sealed partial class LegitimacyCheckBackgroundService(
    IServiceScopeFactory scopes,
    LegitimacyCheckMemory memory,
    PushCheckCoordinator pushChecks,
    TimeProvider timeProvider,
    ILogger<LegitimacyCheckBackgroundService> logger) : BackgroundService
{
    public static readonly TimeSpan AfterSuccess = TimeSpan.FromHours(6);

    public static readonly TimeSpan AfterFailure = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await EvaluateModeAsync(stoppingToken);
            pushChecks.TryStartScheduledCheck();
            var succeeded = await CheckOnceAsync(stoppingToken);
            while (true)
            {
                // The wait restarts from the completion of every check, a push-triggered one included
                // (US-006 spec FR-010).
                var dueAt = timeProvider.GetUtcNow() + (succeeded ? AfterSuccess : AfterFailure);
                await WaitForNextCheckAsync(dueAt, stoppingToken);
                succeeded = await CheckOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown: no new check starts.
        }
    }

    /// <summary>
    /// Waits until the next check may start: the scheduled instant, a push that began one, or the remembered
    /// pending check becoming eligible (US-006 api-design §5). Returns with the check already begun in the
    /// coordinator, so no second check can start beside it.
    /// </summary>
    private async Task WaitForNextCheckAsync(DateTimeOffset dueAt, CancellationToken stoppingToken)
    {
        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();
            if (pushChecks.TryTakeStartedCheck())
            {
                return;
            }

            if (pushChecks.TryStartPendingCheck())
            {
                LogPendingPushCheckStarted(logger);
                return;
            }

            if (timeProvider.GetUtcNow() >= dueAt && pushChecks.TryStartScheduledCheck())
            {
                return;
            }

            var changed = pushChecks.Changed;
            var wakeAt = pushChecks.PendingDueAt is { } pendingDueAt && pendingDueAt < dueAt ? pendingDueAt : dueAt;
            var remaining = wakeAt - timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                continue;
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
        }
    }

    private async Task<bool> CheckOnceAsync(CancellationToken stoppingToken)
    {
        CheckOutcome outcome;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            outcome = await scope.ServiceProvider.GetRequiredService<CheckLegitimacyUseCase>().ExecuteAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Only the type: a message may carry remote detail (SC-10).
            LogCheckException(logger, exception.GetType().Name);
            outcome = CheckOutcome.Failed(LegitimacyCheckFailure.UnexpectedError);
        }
        finally
        {
            // Whatever the outcome, no check is running any more: a push may start the next one.
            pushChecks.CheckCompleted();
        }

        LogOutcome(outcome, memory.RememberOutcome(outcome));
        await EvaluateModeAsync(stoppingToken);
        return outcome.Succeeded;
    }

    private void LogOutcome(CheckOutcome outcome, CheckOutcome? previous)
    {
        if (outcome.Failure is { } failure)
        {
            LogCheckFailed(logger, failure);
        }
        else if (outcome.Compatibility == Domain.Enums.CompatibilityState.UpgradeRecommended)
        {
            LogUpgradeRecommended(logger);
        }

        if (previous is not null && previous != outcome)
        {
            LogResultChanged(logger, outcome.Failure?.ToString() ?? "Succeeded", outcome.Status, outcome.Compatibility);
        }
    }

    private async Task EvaluateModeAsync(CancellationToken stoppingToken)
    {
        LegitimacyMode mode;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            mode = await scope.ServiceProvider.GetRequiredService<GetLegitimacyModeQuery>().ExecuteAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The database cannot be read: readiness reports it; the determination waits for the next check.
            return;
        }

        var wasReadOnly = memory.RememberReadOnly(mode.IsReadOnly);
        if (mode is { IsReadOnly: true, Reason: { } reason } && wasReadOnly != true)
        {
            LogReadOnlyModeEntered(logger, reason);
        }
        else if (!mode.IsReadOnly && wasReadOnly == true)
        {
            LogReadOnlyModeLeft(logger);
        }
    }

    [LoggerMessage(EventId = 5101, EventName = "LegitimacyCheckFailed", Level = LogLevel.Error, Message = "Legitimacy check unsuccessful: {Category}")]
    private static partial void LogCheckFailed(ILogger logger, LegitimacyCheckFailure category);

    [LoggerMessage(EventId = 5102, EventName = "LegitimacyUpgradeRecommended", Level = LogLevel.Warning, Message = "The Control Plane recommends upgrading the installation")]
    private static partial void LogUpgradeRecommended(ILogger logger);

    [LoggerMessage(
        EventId = 5103,
        EventName = "LegitimacyCheckResultChanged",
        Level = LogLevel.Information,
        Message = "Legitimacy check result changed: {Result}, status {Status}, compatibility {Compatibility}")]
    private static partial void LogResultChanged(
        ILogger logger,
        string result,
        Domain.Enums.InstallationStatus? status,
        Domain.Enums.CompatibilityState? compatibility);

    [LoggerMessage(EventId = 5104, EventName = "ReadOnlyModeEntered", Level = LogLevel.Warning, Message = "The installation entered read-only mode: {Reason}")]
    private static partial void LogReadOnlyModeEntered(ILogger logger, LegitimacyModeReason reason);

    [LoggerMessage(EventId = 5105, EventName = "ReadOnlyModeLeft", Level = LogLevel.Information, Message = "The installation left read-only mode")]
    private static partial void LogReadOnlyModeLeft(ILogger logger);

    [LoggerMessage(
        EventId = 5114,
        EventName = "PendingPushCheckStarted",
        Level = LogLevel.Information,
        Message = "The pending push legitimacy check started")]
    private static partial void LogPendingPushCheckStarted(ILogger logger);

    [LoggerMessage(EventId = 5106, EventName = "LegitimacyCheckException", Level = LogLevel.Error, Message = "Legitimacy check failed with an unexpected {ExceptionType}")]
    private static partial void LogCheckException(ILogger logger, string exceptionType);
}
