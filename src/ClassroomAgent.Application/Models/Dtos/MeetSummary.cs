namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Report 1 (spec FR-007).</summary>
public sealed record MeetSummary(
    int MeetingCount,
    long TotalDurationSeconds,
    long AverageDurationSeconds,
    decimal AverageStudentsPerMeeting);
