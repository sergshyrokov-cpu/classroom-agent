using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>The reports page to render and how it answers.</summary>
public sealed record MeetReportsPageResult(MeetReportsPageOutcome Outcome, MeetReportsPageModel Page);
