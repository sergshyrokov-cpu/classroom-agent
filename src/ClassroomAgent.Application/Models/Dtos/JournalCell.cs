namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One cell (US-025 openapi <c>JournalCell</c>, spec FR-006); the two flags are US-060 spec FR-004.</summary>
public sealed record JournalCell(
    JournalCellState State,
    decimal? Points,
    decimal? MaxPoints,
    string? RawState,
    bool Late,
    decimal? DraftPoints,
    DateOnly? TurnedInOn,
    bool PointsAboveMaximum = false,
    bool DraftAboveMaximum = false);
