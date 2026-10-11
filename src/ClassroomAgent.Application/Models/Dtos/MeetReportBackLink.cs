namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>The query of report 4's link back; from/to only when they were valid dates (api-design §2.4).</summary>
public sealed record MeetReportBackLink(long? CourseId, DateOnly? From, DateOnly? To);
