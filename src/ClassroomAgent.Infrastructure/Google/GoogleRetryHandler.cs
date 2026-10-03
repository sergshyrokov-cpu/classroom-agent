namespace ClassroomAgent.Infrastructure.Google;

/// <summary>
/// The transport under the Google client library that repeats a failed HTTP request (US-017 spec FR-002, FR-003): one
/// page or one token request, at most four attempts, pauses of 2 s, 8 s and 30 s times the jitter factor, or the usable
/// <c>Retry-After</c> capped at two minutes. It wraps a shared transport it does not own. When the attempts are used up
/// it fails with a bare <see cref="HttpRequestException"/> — never the answer — so nothing Google sent travels on.
/// </summary>
internal sealed class GoogleRetryHandler(
    HttpMessageHandler inner,
    TimeProvider timeProvider,
    IGoogleRetryJitter jitter,
    Action<int, TimeSpan, int?> onRetry,
    TimeSpan? attemptTimeout = null) : HttpMessageHandler
{
    private const int MaxAttempts = 4;

    /// <summary>
    /// Spec FR-002: how long one attempt, answer headers and body together, may take (the production default; the
    /// constructor can shorten it for a test). The library's HttpClient timeout, formerly 100 s, is switched off
    /// because it would bound the whole retry sequence including the pauses; this bounds each attempt instead. It runs on the system clock, not the injected provider: an in-flight attempt's
    /// timer on a manual test clock would be indistinguishable from the retry pause the tests observe there.
    /// </summary>
    public static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromSeconds(100);

    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(120);

    private static readonly TimeSpan[] NominalPauses =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(30),
    ];

    private readonly TimeSpan _attemptTimeout = attemptTimeout ?? DefaultAttemptTimeout;

    private readonly HttpMessageInvoker _invoker = new(inner, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var attemptTimeout = new CancellationTokenSource(_attemptTimeout, TimeProvider.System);
                using var attemptToken = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, attemptTimeout.Token);
                response = await _invoker.SendAsync(request, attemptToken.Token);

                // The body is read here, under the same bound: with the library's own timeout off, an unbuffered
                // body that stalls would otherwise hang the run (spec FR-002).
                try
                {
                    await response.Content.LoadIntoBufferAsync(attemptToken.Token);
                }
                catch
                {
                    response.Dispose();
                    throw;
                }
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                await PauseAsync(attempt, null, null, cancellationToken);
                continue;
            }
            catch (OperationCanceledException) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                // The attempt timed out (or the transport did); the caller's cancellation is not caught here.
                await PauseAsync(attempt, null, null, cancellationToken);
                continue;
            }

            if (!GoogleFailureClassifier.IsUnavailable(response.StatusCode))
            {
                return response;
            }

            var status = response.StatusCode;
            var retryAfter = response.Headers.RetryAfter;
            response.Dispose();
            if (attempt >= MaxAttempts)
            {
                throw new HttpRequestException("Google is unavailable.", inner: null, status);
            }

            await PauseAsync(attempt, retryAfter, (int)status, cancellationToken);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _invoker.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task PauseAsync(
        int failedAttempt,
        System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter,
        int? status,
        CancellationToken cancellationToken)
    {
        var pause = Pause(failedAttempt, retryAfter);
        onRetry(failedAttempt, pause, status);
        await Task.Delay(pause, timeProvider, cancellationToken);
    }

    private TimeSpan Pause(int failedAttempt, System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter)
    {
        if (UsableRetryAfter(retryAfter) is { } given)
        {
            return given > MaxRetryAfter ? MaxRetryAfter : given;
        }

        return TimeSpan.FromSeconds(NominalPauses[failedAttempt - 1].TotalSeconds * jitter.NextFactor());
    }

    /// <summary>Delta seconds of at least zero, or an HTTP date still ahead; anything else is not usable (VR-001).</summary>
    private TimeSpan? UsableRetryAfter(System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter?.Delta is { } delta)
        {
            return delta >= TimeSpan.Zero ? delta : null;
        }

        if (retryAfter?.Date is { } date)
        {
            var wait = date - timeProvider.GetUtcNow();
            return wait > TimeSpan.Zero ? wait : null;
        }

        return null;
    }
}
