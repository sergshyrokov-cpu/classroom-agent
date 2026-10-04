namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One cell (US-025 openapi <c>JournalCell</c>, spec FR-006).</summary>
public sealed record JournalCell(
    JournalCellState State,
    decimal? Points,
    decimal? MaxPoints,
    string? RawState,
    bool Late,
    decimal? DraftPoints,
    DateOnly? TurnedInOn);
