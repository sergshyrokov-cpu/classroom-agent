using ClassroomAgent.Application.Models.Export;
using ClosedXML.Excel;

namespace ClassroomAgent.Infrastructure.Export;

/// <summary>
/// US-060 spec FR-007.3, FR-007.5, FR-008: the fill and the note of a marked cell, written the same way by both layouts.
/// One light colour (restrained tone, BR-047); a note is an Excel comment, which is not printed.
/// </summary>
internal static class ExportMarks
{
    /// <summary>The one highlight colour of both layouts: a light amber.</summary>
    internal static readonly XLColor Highlight = XLColor.FromHtml("#FFF2CC");

    /// <summary>Applies the cell's highlight and note; a cell without them is left as it is.</summary>
    internal static void Apply(IXLCell target, WorkbookCell cell)
    {
        if (cell.Highlighted)
        {
            target.Style.Fill.PatternType = XLFillPatternValues.Solid;
            target.Style.Fill.BackgroundColor = Highlight;
        }

        if (!string.IsNullOrEmpty(cell.Note))
        {
            // No author: the comment must not carry the account name of the machine that rendered it (US-043 INFO-2).
            var comment = target.CreateComment();
            comment.Author = string.Empty;
            comment.AddText(cell.Note);
        }
    }
}
