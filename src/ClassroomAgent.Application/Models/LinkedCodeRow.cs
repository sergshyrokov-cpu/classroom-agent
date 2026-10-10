namespace ClassroomAgent.Application.Models;

/// <summary>A code linked to a course (US-032 db-design §5.3).</summary>
public sealed record LinkedCodeRow(
    string MeetingCode,
    long CourseId,
    string CourseName,
    string? CourseSection,
    bool LinkedAutomatically,
    AccountRef? LinkedBy,
    DateTimeOffset LinkedAt,
    AccountRef? ConfirmedBy,
    DateTimeOffset? ConfirmedAt,
    int MeetingCount,
    DateTimeOffset? LastStartedAt);
