namespace ClassroomAgent.Application.Models.Export;

/// <summary>US-028 entity model §3.2: one sheet of a <see cref="Workbook"/>.</summary>
public sealed record Worksheet(
    string Name,
    IReadOnlyList<WorksheetRow> Rows,
    int? TableHeaderRowIndex,
    bool RepeatAndFreezeFirstColumn)
{
    /// <summary>US-044 entity model §3.3: an autofilter on the table header row; false (the journal) sets none.</summary>
    public bool AutoFilter { get; init; }

    /// <summary>US-044 entity model §3.3: the last row (0-based, inclusive) of the table; null is the sheet's last row.</summary>
    public int? TableLastRowIndex { get; init; }
}
