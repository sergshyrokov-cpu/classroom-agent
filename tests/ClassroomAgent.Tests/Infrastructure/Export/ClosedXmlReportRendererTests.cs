using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Infrastructure.Export;
using ClosedXML.Excel;

namespace ClassroomAgent.Tests.Infrastructure.Export;

/// <summary>
/// US-028 entity model §2 and §3.1, spec FR-004.7 and S-08: the ClosedXML renderer turns the neutral workbook model into
/// an .xlsx package. The bytes are read back with ClosedXML itself; no database and no Google access is involved.
/// </summary>
public sealed class ClosedXmlReportRendererTests
{
    private const string DateFormat = "dd.mm.yyyy";

    private static WorkbookCell Text(string text, bool isHeading = false, bool quotePrefix = false) =>
        new(WorkbookCellKind.Text, text, null, null, isHeading, quotePrefix);

    private static WorkbookCell Number(decimal number) =>
        new(WorkbookCellKind.Number, null, number, null, false, false);

    private static WorkbookCell Date(DateOnly date) =>
        new(WorkbookCellKind.Date, null, null, date, false, false);

    private static WorkbookCell Empty() =>
        new(WorkbookCellKind.Empty, null, null, null, false, false);

    private static WorksheetRow Row(params WorkbookCell[] cells) => new(cells);

    private static Workbook OneSheet(
        IReadOnlyList<WorksheetRow> rows,
        PageOrientation orientation = PageOrientation.Portrait,
        int? tableHeaderRowIndex = null,
        bool repeatAndFreezeFirstColumn = false) =>
        new(
            [new Worksheet("Sheet", rows, tableHeaderRowIndex, repeatAndFreezeFirstColumn)],
            orientation,
            DateFormat);

    private static async Task<XLWorkbook> RenderAsync(Workbook workbook)
    {
        var bytes = await new ClosedXmlReportRenderer().RenderAsync(workbook, TestContext.Current.CancellationToken);
        return new XLWorkbook(new MemoryStream(bytes));
    }

    /// <summary>§2: sheets appear in model order under the model's names.</summary>
    [Fact]
    public async Task Sheets_AppearInModelOrderWithModelNames()
    {
        var workbook = new Workbook(
            [
                new Worksheet("Оцінювання", [Row(Text("a"))], null, false),
                new Worksheet("Теми занять", [Row(Text("b"))], null, false),
            ],
            PageOrientation.Portrait,
            DateFormat);

        using var result = await RenderAsync(workbook);

        Assert.Equal(["Оцінювання", "Теми занять"], result.Worksheets.Select(s => s.Name).ToArray());
    }

    /// <summary>§2: a Text cell is written as text in Excel row i+1, column j+1.</summary>
    [Fact]
    public async Task TextCell_IsWrittenAsTextAtItsPosition()
    {
        using var result = await RenderAsync(OneSheet([Row(Empty(), Text("x")), Row(Empty(), Empty(), Text("abc"))]));

        var cell = result.Worksheet(1).Cell(2, 3);
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.Equal("abc", cell.GetString());
    }

    /// <summary>§2: a Number cell is written as a number.</summary>
    [Fact]
    public async Task NumberCell_IsWrittenAsNumber()
    {
        using var result = await RenderAsync(OneSheet([Row(Number(12m))]));

        var cell = result.Worksheet(1).Cell(1, 1);
        Assert.Equal(XLDataType.Number, cell.DataType);
        Assert.Equal(12d, cell.GetDouble());
    }

    /// <summary>§2: a Date cell is a real date formatted with the workbook's date format.</summary>
    [Fact]
    public async Task DateCell_IsWrittenAsDateWithWorkbookDateFormat()
    {
        using var result = await RenderAsync(OneSheet([Row(Date(new DateOnly(2026, 9, 10)))]));

        var cell = result.Worksheet(1).Cell(1, 1);
        Assert.Equal(XLDataType.DateTime, cell.DataType);
        Assert.Equal(new DateTime(2026, 9, 10), cell.GetDateTime().Date);
        Assert.Equal(DateFormat, cell.Style.NumberFormat.Format);
    }

    /// <summary>§2: an Empty cell stays empty.</summary>
    [Fact]
    public async Task EmptyCell_StaysEmpty()
    {
        using var result = await RenderAsync(OneSheet([Row(Text("x"), Empty())]));

        Assert.True(result.Worksheet(1).Cell(1, 2).IsEmpty());
    }

    /// <summary>FR-004.7, S-08: text that looks like a formula is stored as text, never as a formula.</summary>
    [Fact]
    public async Task TextStartingWithEquals_IsNeverAFormula()
    {
        using var result = await RenderAsync(OneSheet([Row(Text("=SUM(A1:A2)"))]));

        var cell = result.Worksheet(1).Cell(1, 1);
        Assert.False(cell.HasFormula);
        Assert.Equal("=SUM(A1:A2)", cell.GetString());
    }

    /// <summary>FR-004.7, S-08: QuotePrefix=true sets the quote-prefix style on the cell.</summary>
    [Fact]
    public async Task QuotePrefixTrue_SetsIncludeQuotePrefix()
    {
        using var result = await RenderAsync(OneSheet([Row(Text("=SUM(A1:A2)", quotePrefix: true))]));

        Assert.True(result.Worksheet(1).Cell(1, 1).Style.IncludeQuotePrefix);
    }

    /// <summary>FR-004.7: QuotePrefix=false leaves the quote-prefix style off.</summary>
    [Fact]
    public async Task QuotePrefixFalse_LeavesIncludeQuotePrefixOff()
    {
        using var result = await RenderAsync(OneSheet([Row(Text("plain", quotePrefix: false))]));

        Assert.False(result.Worksheet(1).Cell(1, 1).Style.IncludeQuotePrefix);
    }

    /// <summary>§2: a heading cell is bold and wrapped.</summary>
    [Fact]
    public async Task HeadingCell_IsBoldAndWrapped()
    {
        using var result = await RenderAsync(OneSheet([Row(Text("Heading", isHeading: true))]));

        var style = result.Worksheet(1).Cell(1, 1).Style;
        Assert.True(style.Font.Bold);
        Assert.True(style.Alignment.WrapText);
    }

    /// <summary>§2: a non-heading text with a line break is wrapped.</summary>
    [Fact]
    public async Task MultilineText_IsWrapped()
    {
        using var result = await RenderAsync(OneSheet([Row(Text("line one\nline two"))]));

        Assert.True(result.Worksheet(1).Cell(1, 1).Style.Alignment.WrapText);
    }

    /// <summary>§2: the workbook orientation is applied to every sheet.</summary>
    [Theory]
    [InlineData(PageOrientation.Portrait, XLPageOrientation.Portrait)]
    [InlineData(PageOrientation.Landscape, XLPageOrientation.Landscape)]
    public async Task Orientation_IsAppliedToEverySheet(PageOrientation orientation, XLPageOrientation expected)
    {
        var workbook = new Workbook(
            [
                new Worksheet("One", [Row(Text("a"))], null, false),
                new Worksheet("Two", [Row(Text("b"))], null, false),
            ],
            orientation,
            DateFormat);

        using var result = await RenderAsync(workbook);

        Assert.All(result.Worksheets, ws => Assert.Equal(expected, ws.PageSetup.PageOrientation));
    }

    /// <summary>§2: every sheet is fitted to one page wide and as many pages tall as needed.</summary>
    [Fact]
    public async Task Sheets_FitToOnePageWide()
    {
        using var result = await RenderAsync(OneSheet([Row(Text("a"))]));

        var setup = result.Worksheet(1).PageSetup;
        Assert.Equal(1, setup.PagesWide);
        Assert.Equal(0, setup.PagesTall);
    }

    /// <summary>§2: the table header row repeats on print and the sheet is frozen below it.</summary>
    [Fact]
    public async Task TableHeaderRowIndex_RepeatsRowOnPrintAndFreezesBelowIt()
    {
        var rows = Enumerable.Range(0, 8).Select(i => Row(Text($"r{i}"))).ToList();

        using var result = await RenderAsync(OneSheet(rows, tableHeaderRowIndex: 5));

        var ws = result.Worksheet(1);
        Assert.Equal(6, ws.PageSetup.FirstRowToRepeatAtTop);
        Assert.Equal(6, ws.PageSetup.LastRowToRepeatAtTop);
        Assert.Equal(6, ws.SheetView.SplitRow);
    }

    /// <summary>§2: with RepeatAndFreezeFirstColumn the first column repeats on print and is frozen.</summary>
    [Fact]
    public async Task RepeatAndFreezeFirstColumn_True_RepeatsAndFreezesColumnOne()
    {
        var rows = Enumerable.Range(0, 8).Select(i => Row(Text($"r{i}"), Text("v"))).ToList();

        using var result = await RenderAsync(OneSheet(rows, tableHeaderRowIndex: 5, repeatAndFreezeFirstColumn: true));

        var ws = result.Worksheet(1);
        Assert.Equal(1, ws.PageSetup.FirstColumnToRepeatAtLeft);
        Assert.Equal(1, ws.PageSetup.LastColumnToRepeatAtLeft);
        Assert.Equal(1, ws.SheetView.SplitColumn);
    }

    /// <summary>§2: without RepeatAndFreezeFirstColumn no column repeats or freezes.</summary>
    [Fact]
    public async Task RepeatAndFreezeFirstColumn_False_LeavesColumnsAlone()
    {
        var rows = Enumerable.Range(0, 8).Select(i => Row(Text($"r{i}"), Text("v"))).ToList();

        using var result = await RenderAsync(OneSheet(rows, tableHeaderRowIndex: 5, repeatAndFreezeFirstColumn: false));

        var ws = result.Worksheet(1);
        Assert.Equal(0, ws.SheetView.SplitColumn);
        Assert.Equal(0, ws.PageSetup.FirstColumnToRepeatAtLeft);
    }

    /// <summary>§2: without a table header row nothing repeats or freezes.</summary>
    [Fact]
    public async Task NullTableHeaderRowIndex_RepeatsAndFreezesNothing()
    {
        using var result = await RenderAsync(OneSheet([Row(Text("a")), Row(Text("b"))], tableHeaderRowIndex: null));

        var ws = result.Worksheet(1);
        Assert.Equal(0, ws.PageSetup.FirstRowToRepeatAtTop);
        Assert.Equal(0, ws.SheetView.SplitRow);
    }

    /// <summary>§2: a very long text does not make its column wider than the cap.</summary>
    [Fact]
    public async Task ColumnWidth_IsCapped()
    {
        using var result = await RenderAsync(OneSheet([Row(Text(new string('x', 500)))]));

        Assert.True(result.Worksheet(1).Column(1).Width <= 60);
    }

    /// <summary>§3.1: the output is a valid zip package (.xlsx starts with "PK").</summary>
    [Fact]
    public async Task Output_IsAZipPackage()
    {
        var bytes = await new ClosedXmlReportRenderer().RenderAsync(
            OneSheet([Row(Text("a"))]),
            TestContext.Current.CancellationToken);

        Assert.True(bytes.Length > 0);
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }
}
