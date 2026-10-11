namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Reports 1–3 with the delay notice (US-033 openapi MeetReports).</summary>
public sealed record MeetReports(
    MeetDelayNotice DelayNotice,
    MeetSummary Summary,
    IReadOnlyList<MeetListRow> Meetings,
    IReadOnlyList<StudentParticipationRow> Students);
