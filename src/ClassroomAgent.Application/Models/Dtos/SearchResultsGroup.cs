namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-071 entity model §3: at most 20 rows of a group and its number of matches.</summary>
public sealed record SearchResultsGroup<TRow>(IReadOnlyList<TRow> Rows, int Total);
