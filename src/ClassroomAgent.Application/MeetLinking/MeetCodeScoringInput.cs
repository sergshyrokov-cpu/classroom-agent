namespace ClassroomAgent.Application.MeetLinking;

/// <summary>Everything the scorer needs for one code (US-032 spec FR-003, db-design §5.2).</summary>
public sealed record MeetCodeScoringInput(
    string MeetingCode,
    IReadOnlyList<MeetCodeMeeting> Meetings,
    IReadOnlyList<RosterMembership> Memberships)
{
    /// <summary>US-061 spec FR-001, VR-002: courses with a stored mark (US-059); never candidates.</summary>
    public IReadOnlySet<long> MarkedCourseIds { get; init; } = new HashSet<long>();
}
