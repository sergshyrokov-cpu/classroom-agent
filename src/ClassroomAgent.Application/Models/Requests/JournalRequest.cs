namespace ClassroomAgent.Application.Models.Requests;

/// <summary>
/// The raw query of <c>GET /workspace/journal</c> (US-025 spec FR-001, §6): every occurrence of each parameter as the
/// host received it, so "exactly one value" (VR-001 … VR-004) is decided in <c>Application</c>. An empty list is an
/// absent parameter.
/// </summary>
public sealed record JournalRequest(
    IReadOnlyList<string?> CourseId,
    IReadOnlyList<string?> From,
    IReadOnlyList<string?> To,
    IReadOnlyList<string?> View);
