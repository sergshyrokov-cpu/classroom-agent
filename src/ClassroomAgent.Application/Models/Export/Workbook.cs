namespace ClassroomAgent.Application.Models.Export;

/// <summary>US-028 entity model §3.2: a format-neutral workbook the renderer turns into a file.</summary>
public sealed record Workbook(IReadOnlyList<Worksheet> Sheets, PageOrientation Orientation, string DateFormat);
