namespace ClassroomAgent.Application.Models;

/// <summary>
/// What the Meet step of a run did (US-031 spec FR-012), for the host's log lines only: the window it asked for, the
/// events read, the meetings and connections stored new or updated, the events of meetings not of the school, and the
/// invalid events skipped per reason. Counts and instants only, never an event value (SC-10, AC-012).
/// </summary>
public sealed record MeetPullCounts(
    DateTimeOffset WindowFrom,
    DateTimeOffset WindowTo,
    int EventsRead,
    int SessionsAdded,
    int SessionsUpdated,
    int ParticipationsAdded,
    int ParticipationsUpdated,
    int NotOfTheSchool,
    IReadOnlyDictionary<MeetEventRejection, int> Skipped)
{
    /// <summary>Every event skipped as invalid, whatever the reason.</summary>
    public int SkippedTotal => Skipped.Values.Sum();
}
