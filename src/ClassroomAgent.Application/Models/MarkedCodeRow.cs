namespace ClassroomAgent.Application.Models;

/// <summary>A code marked "not a course" (US-032 db-design §5.3).</summary>
public sealed record MarkedCodeRow(
    string MeetingCode,
    AccountRef MarkedBy,
    DateTimeOffset MarkedAt,
    int MeetingCount,
    DateTimeOffset? LastStartedAt);
