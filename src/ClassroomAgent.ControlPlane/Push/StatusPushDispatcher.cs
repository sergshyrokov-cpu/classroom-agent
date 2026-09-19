using System.Collections.Concurrent;

namespace ClassroomAgent.ControlPlane.Push;

/// <summary>
/// Sends status-change pushes outside the Owner's request (US-006 spec FR-005 … FR-007; api-design §6).
/// At most one push per <c>Installation</c> is in progress: a newer one cancels the older, an attempt in
/// flight included, and starts again at attempt 1 (spec I-9). Pushes to different schools run concurrently.
/// Everything lives in memory: stopping the Control Plane cancels the pending retries and nothing is
/// persisted, so the periodic check delivers the status instead.
/// </summary>
public sealed partial class StatusPushDispatcher(
    IStatusPushClient client,
    TimeProvider timeProvider,
    ILogger<StatusPushDispatcher> logger) : IDisposable
{
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _inProgress = new();
    private readonly CancellationTokenSource _stopping = new();
    private bool _disposed;

    /// <summary>
    /// Starts a push in the background and returns at once; the Owner's request never waits for it
    /// (spec FR-005). Enqueuing for an installation that already has one cancels that one.
    /// </summary>
    public void Enqueue(long installationId, Guid identifier, string address)
    {
        if (_disposed)
        {
            return;
        }

        var push = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        _inProgress.AddOrUpdate(
            installationId,
            push,
            (_, previous) =>
            {
                Cancel(previous);
                return push;
            });

        _ = Task.Run(() => DeliverAsync(installationId, identifier, address, push), CancellationToken.None);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel(_stopping);
        foreach (var push in _inProgress.Values)
        {
            Cancel(push);
        }

        _inProgress.Clear();
        _stopping.Dispose();
    }

    private async Task DeliverAsync(long installationId, Guid identifier, string address, CancellationTokenSource push)
    {
        var cancellationToken = push.Token;
        try
        {
            for (var attempt = 1; attempt <= StatusPushDelivery.MaximumAttempts; attempt++)
            {
                // Read before the attempt: the pause is an absolute instant derived from it, so a clock that
                // moves while the attempt is being classified cannot postpone the retry.
                var startedAt = timeProvider.GetUtcNow();
                var result = await client.SendAsync(address, identifier, cancellationToken);
                if (result.Result == StatusPushAttemptResult.Delivered)
                {
                    LogDelivered(logger, installationId);
                    return;
                }

                if (result.Result == StatusPushAttemptResult.Refused)
                {
                    LogRefused(logger, installationId);
                    return;
                }

                // The pause is measured from the end of the failed attempt (spec I-8): an attempt ends when it
                // is answered, or exactly at the timeout when it is not.
                var endedAt = startedAt + (result.Result == StatusPushAttemptResult.Timeout ? StatusPushDelivery.AttemptTimeout : TimeSpan.Zero);
                var retryAt = attempt < StatusPushDelivery.MaximumAttempts
                    ? endedAt + StatusPushDelivery.RetryPauses[attempt - 1]
                    : (DateTimeOffset?)null;

                LogAttemptFailed(logger, installationId, attempt, result.Result, result.StatusCode);
                if (retryAt is not { } due)
                {
                    LogAbandoned(logger, installationId);
                    return;
                }

                await WaitUntilAsync(due, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Replaced by a newer push, or the Control Plane is stopping: nothing of this one is logged (spec I-9).
        }
        catch (Exception exception)
        {
            // Only the type: a message may carry the address or remote detail (SC-10).
            LogSenderException(logger, installationId, exception.GetType().Name);
        }
        finally
        {
            _inProgress.TryRemove(new KeyValuePair<long, CancellationTokenSource>(installationId, push));
            push.Dispose();
        }
    }

    private async Task WaitUntilAsync(DateTimeOffset due, CancellationToken cancellationToken)
    {
        while (due - timeProvider.GetUtcNow() is { Ticks: > 0 } remaining)
        {
            await Task.Delay(remaining, timeProvider, cancellationToken);
        }
    }

    private static void Cancel(CancellationTokenSource source)
    {
        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The push it belonged to has already finished.
        }
    }

    [LoggerMessage(EventId = 5011, EventName = "StatusPushDelivered", Level = LogLevel.Information, Message = "Status push delivered to installation {InstallationId}")]
    private static partial void LogDelivered(ILogger logger, long installationId);

    [LoggerMessage(
        EventId = 5012,
        EventName = "StatusPushAttemptFailed",
        Level = LogLevel.Warning,
        Message = "Status push attempt {Attempt} to installation {InstallationId} failed: {Category} {StatusCode}")]
    private static partial void LogAttemptFailed(
        ILogger logger,
        long installationId,
        int attempt,
        StatusPushAttemptResult category,
        int? statusCode);

    [LoggerMessage(EventId = 5013, EventName = "StatusPushAbandoned", Level = LogLevel.Warning, Message = "Status push to installation {InstallationId} abandoned: retries exhausted")]
    private static partial void LogAbandoned(ILogger logger, long installationId);

    [LoggerMessage(EventId = 5014, EventName = "StatusPushRefused", Level = LogLevel.Warning, Message = "Status push refused by installation {InstallationId}")]
    private static partial void LogRefused(ILogger logger, long installationId);

    [LoggerMessage(EventId = 5017, EventName = "StatusPushSenderException", Level = LogLevel.Error, Message = "Status push to installation {InstallationId} failed with an unexpected {ExceptionType}")]
    private static partial void LogSenderException(ILogger logger, long installationId, string exceptionType);
}
