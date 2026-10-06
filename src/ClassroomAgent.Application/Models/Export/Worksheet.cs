namespace ClassroomAgent.Application.Models.Export;

/// <summary>US-028 entity model §3.2: one sheet of a <see cref="Workbook"/>.</summary>
public sealed record Worksheet(
    string Name,
    IReadOnlyList<WorksheetRow> Rows,
    int? TableHeaderRowIndex,
    bool RepeatAndFreezeFirstColumn);
