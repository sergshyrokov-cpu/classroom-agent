namespace ClassroomAgent.Application.Models;

/// <summary>
/// One page of <c>call_ended</c> events as the adapter mapped them (US-031 entity model §5), with the number of events
/// whose parameters could not be read into the model at all (spec FR-004).
/// </summary>
public sealed record MeetEventPage(IReadOnlyList<MeetCallEndedEvent> Events, int UnreadableCount);
