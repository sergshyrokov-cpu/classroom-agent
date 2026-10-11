namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-044 entity model §2.5: what the reports page's export action submits — validated values, effective period.</summary>
public sealed record MeetReportsExportAction(long CourseId, DateOnly From, DateOnly To);
