using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The substituted <see cref="IControlPlaneClient"/> of the installation tests (TC-4, US-005 test
/// strategy §3). Replies are scripted in order; with no scripted reply left a call waits until it is
/// cancelled, so a test controls exactly how many checks complete.
/// </summary>
public sealed class FakeControlPlaneClient(TimeProvider time) : IControlPlaneClient
{
    private readonly Lock _gate = new();
    private readonly Queue<Func<CancellationToken, Task<ControlPlaneCheckReply>>> _replies = new();
    private readonly List<CheckCall> _calls = [];
    private TaskCompletionSource _called = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<CheckCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return _calls.ToList();
            }
        }
    }

    public static ControlPlaneCheckReply.Answer Answer(
        InstallationStatus status = InstallationStatus.Active,
        CompatibilityState compatibility = CompatibilityState.Supported,
        string domain = InstallationTestData.Domain,
        string clientId = InstallationTestData.ClientId) =>
        new(status, compatibility, domain, clientId);

    public FakeControlPlaneClient Reply(ControlPlaneCheckReply reply) =>
        Enqueue(_ => Task.FromResult(reply));

    public FakeControlPlaneClient ReplySuccess() => Reply(Answer());

    public FakeControlPlaneClient ReplyFailure(CheckFailureCategory category) =>
        Reply(new ControlPlaneCheckReply.Failure(category));

    public FakeControlPlaneClient Throw(Exception exception) =>
        Enqueue(_ => Task.FromException<ControlPlaneCheckReply>(exception));

    /// <summary>A reply the test releases later — for "one check at a time" and "stored state before the first check".</summary>
    public TaskCompletionSource<ControlPlaneCheckReply> ReplyLater()
    {
        var pending = new TaskCompletionSource<ControlPlaneCheckReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(ct => pending.Task.WaitAsync(ct));
        return pending;
    }

    public async Task<ControlPlaneCheckReply> CheckAsync(
        Guid installationId,
        string applicationVersion,
        int contractVersion,
        CancellationToken cancellationToken)
    {
        Func<CancellationToken, Task<ControlPlaneCheckReply>>? reply;
        TaskCompletionSource called;
        lock (_gate)
        {
            _calls.Add(new CheckCall(time.GetUtcNow(), installationId, applicationVersion, contractVersion));
            reply = _replies.Count > 0 ? _replies.Dequeue() : null;
            called = _called;
            _called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        called.TrySetResult();
        if (reply is null)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return await reply!(cancellationToken);
    }

    /// <summary>Waits (real time, bounded) until at least that many calls have started.</summary>
    public async Task WaitForCallsAsync(int count, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(ManualTimeProvider.RealTimeLimit);
        while (true)
        {
            Task next;
            lock (_gate)
            {
                if (_calls.Count >= count)
                {
                    return;
                }

                next = _called.Task;
            }

            try
            {
                await next.WaitAsync(limit.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Assert.Fail($"Timed out waiting for {count} Control Plane call(s); {Calls.Count} made.");
            }
        }
    }

    private FakeControlPlaneClient Enqueue(Func<CancellationToken, Task<ControlPlaneCheckReply>> reply)
    {
        lock (_gate)
        {
            _replies.Enqueue(reply);
        }

        return this;
    }

    public sealed record CheckCall(DateTimeOffset At, Guid InstallationId, string ApplicationVersion, int ContractVersion);
}
