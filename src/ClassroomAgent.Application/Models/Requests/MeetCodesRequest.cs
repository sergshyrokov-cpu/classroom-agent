namespace ClassroomAgent.Application.Models.Requests;

/// <summary>The raw query of <c>GET /workspace/meet-codes</c> (US-032 VR-004): every occurrence of each parameter, so "exactly one value" is decided in <c>Application</c>. An empty list is an absent parameter.</summary>
public sealed record MeetCodesRequest(
    IReadOnlyList<string?> List,
    IReadOnlyList<string?> Page,
    IReadOnlyList<string?> Size);
