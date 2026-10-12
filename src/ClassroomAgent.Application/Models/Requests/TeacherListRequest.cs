namespace ClassroomAgent.Application.Models.Requests;

/// <summary>
/// The raw query of <c>GET /workspace/teachers</c> (US-022 api-design §2.3): every occurrence of <c>status</c> and
/// <c>page</c> as the host received it, so "exactly one value" (spec VR-001, VR-002) is decided in <c>Application</c>.
/// An empty list is an absent parameter. Other parameters are not collected (spec VR-003).
/// </summary>
public sealed record TeacherListRequest(IReadOnlyList<string?> Status, IReadOnlyList<string?> Page);
