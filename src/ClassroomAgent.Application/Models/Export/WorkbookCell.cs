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

    /// <summary>US-044 entity model §3.2: the value of a <see cref="WorkbookCellKind.Time"/> cell.</summary>
    public TimeOnly? Time { get; init; }

    /// <summary>US-044 entity model §3.2: the value of a <see cref="WorkbookCellKind.DateTime"/> cell — local wall-clock time, kind Unspecified.</summary>
    public DateTime? LocalDateTime { get; init; }

    /// <summary>US-044 entity model §3.2: the Excel number format of a number cell, e.g. <c>0.0</c>; null is Excel's General.</summary>
    public string? NumberFormat { get; init; }

    /// <summary>US-060 spec FR-007.3, FR-007.5: the cell gets the one light highlight fill of both layouts.</summary>
    public bool Highlighted { get; init; }

    /// <summary>US-060 spec FR-007, FR-008: a note (an Excel comment) on the cell; it is not printed and the value is unchanged.</summary>
    public string? Note { get; init; }

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

    /// <summary>US-044 entity model §3.2.</summary>
    public static WorkbookCell OfTime(TimeOnly time) =>
        new(WorkbookCellKind.Time, null, null, null, false, false) { Time = time };

    /// <summary>US-044 entity model §3.2.</summary>
    public static WorkbookCell OfDateTime(DateTime localDateTime) =>
        new(WorkbookCellKind.DateTime, null, null, null, false, false)
        {
            LocalDateTime = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified),
        };

    /// <summary>US-044 entity model §3.2: a number with its own number format.</summary>
    public static WorkbookCell OfNumber(decimal number, string? numberFormat) =>
        OfNumber(number) with { NumberFormat = numberFormat };
}
