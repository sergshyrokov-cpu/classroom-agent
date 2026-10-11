namespace ClassroomAgent.Application.Models;

/// <summary>
/// US-044 spec FR-014: the facts of a successful Meet export's log line — internal ids, the period or the meeting, the
/// row counts and the orientation. Never a name, an email, a meeting code or any file content (SC-10).
/// </summary>
public sealed record MeetExportSummary(
    long CourseId,
    DateOnly? From,
    DateOnly? To,
    long? MeetingId,
    int Rows,
    int? Meetings,
    string Orientation);
