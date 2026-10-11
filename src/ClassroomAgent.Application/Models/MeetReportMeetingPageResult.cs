using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>Report 4 to render and how it answers.</summary>
public sealed record MeetReportMeetingPageResult(MeetReportMeetingOutcome Outcome, MeetReportMeetingPageModel Page);
