namespace ClassroomAgent.Application.Models;

/// <summary>One connection; Email null = external guest or connection without an account (PC-12).</summary>
public sealed record MeetReportConnectionRecord(string? Email, int DurationSeconds);
