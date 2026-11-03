namespace ClassroomAgent.Application.Models.Requests;

/// <summary>US-071 entity model §4: every occurrence of <c>q</c>; the query decides "exactly one value" (VR-001).</summary>
public sealed record SearchResultsRequest(IReadOnlyList<string?> Q);
