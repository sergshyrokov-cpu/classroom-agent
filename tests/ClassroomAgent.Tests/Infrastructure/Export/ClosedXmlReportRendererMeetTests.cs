using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Infrastructure.Export;
using ClassroomAgent.Tests.TestInfrastructure;
using ClosedXML.Excel;

namespace ClassroomAgent.Tests.Infrastructure.Export;

/// <summary>
/// US-044 entity model §3 and spec FR-007 (AC-005, AC-006): the ClosedXML renderer writes time and date-time cells as
/// Excel date values with the workbook's formats, applies a number format, and sets the autofilter from the table header
/// row to the last table row. The bytes are read back with ClosedXML itself; no database and no Google access is involved.
/// </summary>
public sealed class ClosedXmlReportRendererMeetTests
{
    private const string DateFormat = "dd.mm.yyyy";

    private const string TimeFormat = "HH:mm";

    private const string DateTimeFormat = "dd.MM.yyyy HH:mm";

    private static WorksheetRow Row(params WorkbookCell[] cells) => new(cells);

    private static Workbook OneSheet(Worksheet sheet, string? timeFormat = TimeFormat, string? dateTimeFormat = DateTimeFormat) =>
        new([sheet], PageOrientation.Landscape, DateFormat)
        {
            TimeFormat = timeFormat,
            DateTimeFormat = dateTimeFormat,
        };

    private static Workbook OneRow(WorkbookCell cell, string? timeFormat = TimeFormat, string? dateTimeFormat = DateTimeFormat) =>
        OneSheet(new Worksheet("Sheet", [Row(cell)], null, false), timeFormat, dateTimeFormat);

    private static async Task<XLWorkbook> RenderAsync(Workbook workbook)
    {
        var bytes = await new ClosedXmlReportRenderer().RenderAsync(workbook, TestContext.Current.CancellationToken);
        return new XLWorkbook(new MemoryStream(bytes));
    }

    private static WorkbookCell RawCell(WorkbookCellKind kind) =>
        new WorkbookCell(kind, null, null, null, false, false)
        {
            Time = new TimeOnly(10, 30),
            LocalDateTime = new DateTime(2026, 11, 1, 0, 40, 0),
        };

    private static Worksheet TextSheet(int rows, int? headerRowIndex, bool autoFilter, int? lastRowIndex) =>
        new(
            "Sheet",
            Enumerable.Range(0, rows)
                .Select(r => Row(WorkbookCell.OfText($"a{r}"), WorkbookCell.OfText($"b{r}"), WorkbookCell.OfText($"c{r}")))
                .ToList(),
            headerRowIndex,
            false)
        {
            AutoFilter = autoFilter,
            TableLastRowIndex = lastRowIndex,
        };

    /// <summary>AC-006: a time of day is an Excel time value with the workbook's time format.</summary>
    [Fact]
    public async Task TimeCell_IsATimeValue_WithTheWorkbookTimeFormat()
    {
        using var result = await RenderAsync(OneRow(WorkbookCell.OfTime(new TimeOnly(10, 30))));

        var cell = result.Worksheet(1).Cell(1, 1);
        MeetExportHostExtensions.AssertSerial(10.5 / 24, cell);
        Assert.Equal(TimeFormat, cell.Style.NumberFormat.Format);
    }

    /// <summary>AC-006, AC-017: a local date-time is an Excel date-time value with the workbook's date-time format.</summary>
    [Fact]
    public async Task DateTimeCell_IsADateTimeValue_WithTheWorkbookDateTimeFormat()
    {
        var local = new DateTime(2026, 11, 1, 0, 40, 0);

        using var result = await RenderAsync(OneRow(WorkbookCell.OfDateTime(local)));

        var cell = result.Worksheet(1).Cell(1, 1);
        MeetExportHostExtensions.AssertSerial(MeetExportHostExtensions.SerialOf(local), cell);
        Assert.Equal(DateTimeFormat, cell.Style.NumberFormat.Format);
    }

    /// <summary>AC-006: a number with its own format keeps the value and carries the format.</summary>
    [Fact]
    public async Task NumberCell_WithAFormat_CarriesTheFormat()
    {
        using var result = await RenderAsync(OneRow(WorkbookCell.OfNumber(1.5m, "0.0")));

        var cell = result.Worksheet(1).Cell(1, 1);
        Assert.Equal(XLDataType.Number, cell.DataType);
        Assert.Equal(1.5d, cell.GetDouble());
        Assert.Equal("0.0", cell.Style.NumberFormat.Format);
    }

    /// <summary>AC-006: a number without a format stays in Excel's General.</summary>
    [Fact]
    public async Task NumberCell_WithoutAFormat_StaysGeneral()
    {
        using var result = await RenderAsync(OneRow(WorkbookCell.OfNumber(2m)));

        var cell = result.Worksheet(1).Cell(1, 1);
        Assert.Equal(2d, cell.GetDouble());
        Assert.Equal(0, cell.Style.NumberFormat.NumberFormatId);
    }

    /// <summary>Entity model §3.4: a time cell without the workbook's time format is a mapper defect.</summary>
    [Fact]
    public async Task TimeCell_WithoutATimeFormat_Throws() =>
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RenderAsync(OneRow(RawCell(WorkbookCellKind.Time), timeFormat: null)));

    /// <summary>Entity model §3.4: a date-time cell without the workbook's date-time format is a mapper defect.</summary>
    [Fact]
    public async Task DateTimeCell_WithoutADateTimeFormat_Throws() =>
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RenderAsync(OneRow(RawCell(WorkbookCellKind.DateTime), dateTimeFormat: null)));

    /// <summary>AC-005, entity model §3.3: the autofilter runs from the header row to <c>TableLastRowIndex</c>, inclusive.</summary>
    [Fact]
    public async Task AutoFilter_RunsFromTheHeaderRow_ToTheTableLastRow()
    {
        using var result = await RenderAsync(OneSheet(TextSheet(7, headerRowIndex: 2, autoFilter: true, lastRowIndex: 4)));

        Assert.Equal("A3:C5", MeetExportHostExtensions.AutoFilterRange(result.Worksheet(1)));
    }

    /// <summary>AC-005, entity model §3.3: without a last table row the filter runs to the sheet's last row.</summary>
    [Fact]
    public async Task AutoFilter_WithoutALastTableRow_RunsToTheSheetsLastRow()
    {
        using var result = await RenderAsync(OneSheet(TextSheet(7, headerRowIndex: 2, autoFilter: true, lastRowIndex: null)));

        Assert.Equal("A3:C7", MeetExportHostExtensions.AutoFilterRange(result.Worksheet(1)));
    }

    /// <summary>AC-005, AC-016, I-7: with <c>AutoFilter</c> false (the journal) the sheet has no autofilter.</summary>
    [Fact]
    public async Task AutoFilter_False_SetsNoFilter()
    {
        using var result = await RenderAsync(OneSheet(TextSheet(7, headerRowIndex: 2, autoFilter: false, lastRowIndex: 4)));

        Assert.Null(MeetExportHostExtensions.AutoFilterRange(result.Worksheet(1)));
    }
}
