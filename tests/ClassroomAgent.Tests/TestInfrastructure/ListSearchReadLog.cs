using System.Collections.Concurrent;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-070 AC-007: what a list source was asked over HTTP, one entry per port call, with the search text it received. A
/// refused text must leave it empty, and the same page with an accepted text must fill it, so "nothing was read" is
/// never free (the control makes it fail while nothing passes the text on).
/// </summary>
public sealed class ListSearchReadLog
{
    private readonly ConcurrentQueue<(string Read, string? Search)> _reads = new();

    public IReadOnlyList<(string Read, string? Search)> Reads => _reads.ToArray();

    public void Add(string read, string? search) => _reads.Enqueue((read, search));
}
