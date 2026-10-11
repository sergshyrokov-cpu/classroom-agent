namespace ClassroomAgent.Application.Models.Export;

/// <summary>US-028 entity model §3.2: a format-neutral workbook the renderer turns into a file.</summary>
public sealed record Workbook(IReadOnlyList<Worksheet> Sheets, PageOrientation Orientation, string DateFormat)
{
    /// <summary>US-044 entity model §3.4: the UI language's short time format, e.g. <c>HH:mm</c>.</summary>
    public string? TimeFormat { get; init; }

    /// <summary>US-044 entity model §3.4: the UI language's short date and time format.</summary>
    public string? DateTimeFormat { get; init; }
}
