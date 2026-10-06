namespace ClassroomAgent.Application.Models.Export;

/// <summary>US-028 entity model §3.2: one row of a <see cref="Worksheet"/>. Skeleton (OD-005).</summary>
public sealed record WorksheetRow(IReadOnlyList<WorkbookCell> Cells);
