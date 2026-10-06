namespace ClassroomAgent.Application.Models.Export;

/// <summary>
/// US-028 entity model §2: one cell of a <see cref="WorksheetRow"/>. The factories hold the two text rules of the file —
/// Excel's cell limit (spec FR-004.9) and the quote prefix of formula-like text (spec FR-004.7) — once.
/// </summary>
public sealed record WorkbookCell(
    WorkbookCellKind Kind,
    string? Text,
    decimal? Number,
    DateOnly? Date,
    bool IsHeading,
    bool QuotePrefix)
{
    /// <summary>Excel's maximum number of characters in one cell (spec FR-004.9, I-7).</summary>
    public const int MaxTextLength = 32_767;

    private static readonly char[] FormulaLeads = ['=', '+', '-', '@', '\t', '\r'];

    public static WorkbookCell Empty() => new(WorkbookCellKind.Empty, null, null, null, false, false);

    /// <summary>A text value, cut to <see cref="MaxTextLength"/>; formula-like text keeps its value and gains the quote prefix.</summary>
    public static WorkbookCell OfText(string text, bool isHeading = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        var value = text.Length > MaxTextLength ? text[..MaxTextLength] : text;
        var quotePrefix = value.Length > 0 && Array.IndexOf(FormulaLeads, value[0]) >= 0;
        return new WorkbookCell(WorkbookCellKind.Text, value, null, null, isHeading, quotePrefix);
    }

    public static WorkbookCell OfNumber(decimal number) => new(WorkbookCellKind.Number, null, number, null, false, false);

    public static WorkbookCell OfDate(DateOnly date) => new(WorkbookCellKind.Date, null, null, date, false, false);
}
