using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-019: the synchronization-request port, substituted. It records every request and answers with
/// <see cref="Timing"/>, so a test proves whether the use case enqueued anything (spec FR-006, S-03).
/// </summary>
public sealed class FakeSynchronizationRequests : ISynchronizationRequests
{
    private int _calls;

    /// <summary>What the next requests answer.</summary>
    public SynchronizationRequestTiming Timing { get; set; } = SynchronizationRequestTiming.StartsNow;

    /// <summary>How many requests reached the port.</summary>
    public int Calls => Volatile.Read(ref _calls);

    public Task<SynchronizationRequestTiming> RequestAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(Timing);
    }
}
