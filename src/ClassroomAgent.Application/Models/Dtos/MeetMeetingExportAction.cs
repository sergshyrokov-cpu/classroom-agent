namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-044 entity model §2.5: what the meeting page's export action submits.</summary>
public sealed record MeetMeetingExportAction(long CourseId, long MeetingId);
