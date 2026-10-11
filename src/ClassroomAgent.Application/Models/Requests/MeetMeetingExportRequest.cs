namespace ClassroomAgent.Application.Models.Requests;

/// <summary>US-044 entity model §2.1: the raw body of the meeting-workbook export (openapi MeetMeetingExportRequest).</summary>
public sealed record MeetMeetingExportRequest(string? CourseId, string? MeetingId, string? Orientation);
