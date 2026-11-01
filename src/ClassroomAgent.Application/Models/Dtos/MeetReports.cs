namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Reports 1–3 with the delay notice (US-033 openapi MeetReports) and, for a marked course, its Meet line (US-061 spec
/// FR-005).
/// </summary>
public sealed record MeetReports(
    MeetDelayNotice DelayNotice,
    MeetSummary Summary,
    IReadOnlyList<MeetListRow> Meetings,
    IReadOnlyList<StudentParticipationRow> Students,
    MeetCourseNotice? CourseNotice = null);
