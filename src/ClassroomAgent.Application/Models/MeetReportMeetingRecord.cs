namespace ClassroomAgent.Application.Models;

/// <summary>
/// One stored meeting of a course with its connections (US-033 entity model §4). A null organizer was erased or is
/// excluded (US-038 spec I-9): "organizer unknown".
/// </summary>
public sealed record MeetReportMeetingRecord(
    long Id,
    string MeetingCode,
    string? OrganizerEmail,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    IReadOnlyList<MeetReportConnectionRecord> Connections);
