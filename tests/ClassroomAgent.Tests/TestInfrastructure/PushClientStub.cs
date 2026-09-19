using System.Net;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The network behind the Control Plane's push HTTP client (test strategy §3): the primary handler of the
/// named client <see cref="PushTestData.HttpClientName"/> is replaced by this one, so US-006 tests observe
/// every attempt without a real installation (TC-4 isolation).
/// </summary>
public sealed class PushClientStub(TimeProvider time) : HttpMessageHandler
{
    private readonly Lock _gate = new();
    private readonly List<Attempt> _attempts = [];
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _replies = new();
    private Func<CancellationToken, Task<HttpResponseMessage>> _default = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    private TaskCompletionSource _received = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<Attempt> Attempts
    {
        get
        {
            lock (_gate)
            {
                return _attempts.ToList();
            }
        }
    }

    /// <summary>Every attempt answers this status until a scripted reply is queued.</summary>
    public PushClientStub AlwaysAnswer(HttpStatusCode status)
    {
        lock (_gate)
        {
            _default = _ => Task.FromResult(new HttpResponseMessage(status));
        }

        return this;
    }

    /// <summary>The next attempt answers this status; scripted replies are used in order.</summary>
    public PushClientStub Answer(HttpStatusCode status) =>
        Enqueue(_ => Task.FromResult(new HttpResponseMessage(status)));

    /// <summary>The next attempt fails to connect.</summary>
    public PushClientStub FailToConnect() =>
        Enqueue(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException("connection refused")));

    /// <summary>The next attempt never answers until it is cancelled — the timeout case.</summary>
    public PushClientStub NeverAnswer() =>
        Enqueue(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });

    /// <summary>Waits (real time, bounded) until at least that many attempts have been made.</summary>
    public async Task WaitForAttemptsAsync(int count, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(ManualTimeProvider.RealTimeLimit);
        while (true)
        {
            Task next;
            lock (_gate)
            {
                if (_attempts.Count >= count)
                {
                    return;
                }

                next = _received.Task;
            }

            try
            {
                await next.WaitAsync(TimeSpan.FromMilliseconds(100), limit.Token);
            }
            catch (TimeoutException)
            {
                // Re-check: the sender may have recorded an attempt between the two reads.
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Assert.Fail($"Timed out waiting for {count} push attempt(s); {Attempts.Count} made.");
            }
        }
    }

    /// <summary>Waits (real time, bounded) until no further attempt arrives for a short while.</summary>
    public async Task AssertNoAttemptAsync(CancellationToken cancellationToken, int expected = 0)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        Assert.Equal(expected, Attempts.Count);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Func<CancellationToken, Task<HttpResponseMessage>> reply;
        TaskCompletionSource received;
        lock (_gate)
        {
            _attempts.Add(new Attempt(
                time.GetUtcNow(),
                request.Method,
                request.RequestUri!,
                request.Content?.Headers.ContentType?.ToString(),
                body,
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase)));
            reply = _replies.Count > 0 ? _replies.Dequeue() : _default;
            received = _received;
            _received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        received.TrySetResult();
        return await reply(cancellationToken);
    }

    private PushClientStub Enqueue(Func<CancellationToken, Task<HttpResponseMessage>> reply)
    {
        lock (_gate)
        {
            _replies.Enqueue(reply);
        }

        return this;
    }

    public sealed record Attempt(
        DateTimeOffset At,
        HttpMethod Method,
        Uri Uri,
        string? ContentType,
        string Body,
        IReadOnlyDictionary<string, string> Headers);
}
