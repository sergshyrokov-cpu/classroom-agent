using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>The report page to render and how it answers.</summary>
public sealed record ReportPageResult(
    ReportPageOutcome Outcome,
    ReportPageModel Page);
