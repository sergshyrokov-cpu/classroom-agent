using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The Meet port of every US-031 test (TC-4, AC-015), in the Application-layer world and in the host alike: it answers
/// with the pages a test seeded, records each call with its window, and fails or stops where a test scripts it.
/// Nothing reaches Google; every event is synthetic (<see cref="MeetTestData"/>).
/// </summary>
public sealed class FakeMeetReportsReader : IMeetReportsReader
{
    private readonly List<MeetEventPage> _pages = [];

    /// <summary>Every call, in order: the impersonated account and the window asked for (spec FR-002, BR-015).</summary>
    public List<MeetReadCall> Calls { get; } = [];

    /// <summary>Thrown before any page when set (a configuration failure of the first request, spec FR-010).</summary>
    public Exception? FailOnFirstPage { get; set; }

    /// <summary>Thrown after <see cref="FailAfterPages"/> pages were yielded, when set.</summary>
    public Exception? FailAfterPagesWith { get; set; }

    public int FailAfterPages { get; set; }

    /// <summary>Called after each page is yielded, with its index — used to stop the host part-way (spec §8).</summary>
    public Action<int>? AfterPage { get; set; }

    /// <summary>Called when a call starts — lets a test record the order of the run's steps (spec FR-001).</summary>
    public Action? OnCall { get; set; }

    /// <summary>Appends one page of events.</summary>
    public FakeMeetReportsReader WithPage(params MeetCallEndedEvent[] events) => WithPage(0, events);

    /// <summary>Appends one page of events with that many events the adapter could not read (spec FR-004).</summary>
    public FakeMeetReportsReader WithPage(int unreadable, params MeetCallEndedEvent[] events)
    {
        _pages.Add(new MeetEventPage(events, unreadable));
        return this;
    }

    /// <summary>Forgets the seeded pages and every scripted failure: what Google answers on the next run.</summary>
    public FakeMeetReportsReader Reset()
    {
        _pages.Clear();
        FailOnFirstPage = null;
        FailAfterPagesWith = null;
        FailAfterPages = 0;
        AfterPage = null;
        return this;
    }

    public async IAsyncEnumerable<MeetEventPage> ReadCallEndedAsync(
        string impersonationUser,
        DateTimeOffset from,
        DateTimeOffset to,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Calls.Add(new MeetReadCall(impersonationUser, from, to));
        OnCall?.Invoke();
        if (FailOnFirstPage is { } first)
        {
            throw first;
        }

        for (var index = 0; index < _pages.Count; index++)
        {
            if (FailAfterPagesWith is { } later && index == FailAfterPages)
            {
                throw later;
            }

            cancellationToken.ThrowIfCancellationRequested();
            yield return _pages[index];
            AfterPage?.Invoke(index);
            await Task.Yield();
        }

        if (FailAfterPagesWith is { } last && FailAfterPages >= _pages.Count)
        {
            throw last;
        }
    }
}
