namespace ClassroomAgent.Application.Models.Export;

/// <summary>
/// US-043 spec FR-005: one value of a copy, placed <see cref="Down"/> rows below and <see cref="Right"/> columns right
/// of its marker's cell. An <see cref="WorkbookCellKind.Empty"/> value clears the cell — value and formula — and keeps its
/// lines (FR-005.5).
/// </summary>
public sealed record BlankFormPlacement(BlankFormMarker Marker, int Down, int Right, WorkbookCell Value);
