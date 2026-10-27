namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// US-060 spec FR-004, FR-007.3: the raw points of a report cell above its item's maximum, for the label on screen and
/// the note in the export. A null side is not above the maximum.
/// </summary>
public sealed record ReportAboveMaximum(decimal? Points, decimal? DraftPoints, decimal MaxPoints);
