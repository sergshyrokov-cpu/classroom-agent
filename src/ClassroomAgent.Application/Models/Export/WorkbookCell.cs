namespace ClassroomAgent.Application.Models.Export;

/// <summary>US-028 entity model §3.2: one cell of a <see cref="WorksheetRow"/>. Skeleton (OD-005).</summary>
public sealed record WorkbookCell(
    WorkbookCellKind Kind,
    string? Text,
    decimal? Number,
    DateOnly? Date,
    bool IsHeading,
    bool QuotePrefix)
{
    public static WorkbookCell Empty() => throw new NotImplementedException("US-028 IMPLEMENTATION");

    public static WorkbookCell OfText(string text, bool isHeading = false) =>
        throw new NotImplementedException("US-028 IMPLEMENTATION");

    public static WorkbookCell OfNumber(decimal number) => throw new NotImplementedException("US-028 IMPLEMENTATION");

    public static WorkbookCell OfDate(DateOnly date) => throw new NotImplementedException("US-028 IMPLEMENTATION");
}
