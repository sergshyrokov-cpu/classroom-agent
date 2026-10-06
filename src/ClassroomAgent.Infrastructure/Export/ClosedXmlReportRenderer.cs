using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;
using ClosedXML.Excel;

namespace ClassroomAgent.Infrastructure.Export;

/// <summary>
/// US-028 OD-001, entity model §3.1: writes the format-neutral <see cref="Workbook"/> as an <c>.xlsx</c> package in
/// memory with ClosedXML. A fixed layout and no business rule: every string is already final in the model. Nothing is
/// written to disk (spec FR-006, S-07) and no ClosedXML type leaves this class (AD-4).
/// </summary>
public sealed class ClosedXmlReportRenderer : IReportRenderer
{
    /// <summary>Entity model §3.1: columns follow their content up to this width, in characters.</summary>
    public const double MaxColumnWidth = 60;

    private const double MinColumnWidth = 8;

    public Task<byte[]> RenderAsync(Workbook workbook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        using var book = new XLWorkbook();
        foreach (var sheet in workbook.Sheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(book.Worksheets.Add(sheet.Name), sheet, workbook);
        }

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    private static void Write(IXLWorksheet target, Worksheet sheet, Workbook workbook)
    {
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var cells = sheet.Rows[r].Cells;
            for (var c = 0; c < cells.Count; c++)
            {
                Write(target.Cell(r + 1, c + 1), cells[c], workbook.DateFormat);
            }
        }

        // Spec FR-004.8: the chosen orientation, one page wide and as many pages tall as needed.
        target.PageSetup.PageOrientation = workbook.Orientation == PageOrientation.Landscape
            ? XLPageOrientation.Landscape
            : XLPageOrientation.Portrait;
        target.PageSetup.FitToPages(1, 0);

        // Spec FR-004.8: the table header row repeats on every printed page and stays in view; on "Grading" the
        // student-name column too.
        if (sheet.TableHeaderRowIndex is { } header)
        {
            var row = header + 1;
            target.PageSetup.SetRowsToRepeatAtTop(row, row);
            if (sheet.RepeatAndFreezeFirstColumn)
            {
                target.PageSetup.SetColumnsToRepeatAtLeft(1, 1);
                target.SheetView.Freeze(row, 1);
            }
            else
            {
                target.SheetView.FreezeRows(row);
            }
        }

        target.ColumnsUsed().AdjustToContents(MinColumnWidth, MaxColumnWidth);
    }

    private static void Write(IXLCell target, WorkbookCell cell, string dateFormat)
    {
        switch (cell.Kind)
        {
            case WorkbookCellKind.Text:
                // Spec FR-004.7: a string value, never a formula; the quote prefix keeps an edit from turning it into one.
                var text = cell.Text ?? string.Empty;
                target.SetValue(text);
                if (cell.QuotePrefix)
                {
                    target.Style.IncludeQuotePrefix = true;
                }

                if (cell.IsHeading)
                {
                    target.Style.Font.Bold = true;
                }

                if (cell.IsHeading || text.Contains('\n', StringComparison.Ordinal))
                {
                    target.Style.Alignment.WrapText = true;
                }

                break;
            case WorkbookCellKind.Number when cell.Number is { } number:
                target.SetValue(number);
                break;
            case WorkbookCellKind.Date when cell.Date is { } date:
                target.SetValue(date.ToDateTime(TimeOnly.MinValue));
                target.Style.NumberFormat.Format = dateFormat;
                break;
        }
    }
}
