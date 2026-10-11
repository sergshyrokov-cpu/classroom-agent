namespace ClassroomAgent.Application.Models.Requests;

/// <summary>
/// The raw query of <c>GET /workspace/courses</c> (US-020 api-design §2.3): every occurrence of each of the four
/// parameters as the host received it, so "exactly one value" (spec VR-001 … VR-004) is decided in <c>Application</c>.
/// An empty list is an absent parameter. Unknown parameters are not collected (spec VR-005).
/// </summary>
public sealed record CourseListRequest(
    IReadOnlyList<string?> Status,
    IReadOnlyList<string?> TeacherId,
    IReadOnlyList<string?> Q,
    IReadOnlyList<string?> Page);
