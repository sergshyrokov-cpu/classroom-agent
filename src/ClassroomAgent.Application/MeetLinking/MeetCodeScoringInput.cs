namespace ClassroomAgent.Application.MeetLinking;

/// <summary>Everything the scorer needs for one code (US-032 spec FR-003, db-design §5.2).</summary>
public sealed record MeetCodeScoringInput(
    string MeetingCode,
    IReadOnlyList<MeetCodeMeeting> Meetings,
    IReadOnlyList<RosterMembership> Memberships);
