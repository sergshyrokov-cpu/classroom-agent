namespace ClassroomAgent.Application.Models.Requests;

/// <summary>The raw query of <c>GET /reports</c> (US-027 spec FR-011): every occurrence of each parameter; an empty list is absent.</summary>
public sealed record ReportRequest(
    IReadOnlyList<string?> Template,
    IReadOnlyList<string?> CourseId,
    IReadOnlyList<string?> From,
    IReadOnlyList<string?> To,
    IReadOnlyList<string?>? Names = null);
