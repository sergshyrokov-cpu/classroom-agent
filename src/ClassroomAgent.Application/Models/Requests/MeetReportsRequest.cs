namespace ClassroomAgent.Application.Models.Requests;

/// <summary>The raw query of GET /workspace/meet-reports: every occurrence of each parameter; an empty list is absent (spec §6).</summary>
public sealed record MeetReportsRequest(
    IReadOnlyList<string?> CourseId,
    IReadOnlyList<string?> From,
    IReadOnlyList<string?> To);
