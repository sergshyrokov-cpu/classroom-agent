namespace ClassroomAgent.Application.Models.Requests;

/// <summary>US-044 entity model §2.1: the raw body of the course-workbook export (openapi MeetReportsExportRequest).</summary>
public sealed record MeetReportsExportRequest(string? CourseId, string? From, string? To, string? Orientation);
