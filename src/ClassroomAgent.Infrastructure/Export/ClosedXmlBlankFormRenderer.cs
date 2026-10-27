using System.Globalization;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;
using ClosedXML.Excel;

namespace ClassroomAgent.Infrastructure.Export;

/// <summary>
/// US-043 spec FR-001 … FR-006.1: fills copies of the shipped form "Бланк для групових занять" with ClosedXML. The form
/// is the embedded resource built from <c>docs/product/report-templates/group-lessons-blank.xlsx</c> (I-1), opened for
/// each export and never written back; every field is found by its marker text, never by a cell address (OD-008 a). No
/// business rule: values, copy order, numbers and footer sections are final in the <see cref="BlankFormFill"/>. The file
/// is built in memory (S-07) and no ClosedXML type leaves this class (AD-4).
/// </summary>
public sealed class ClosedXmlBlankFormRenderer : IBlankFormRenderer
{
    /// <summary>The logical name the project file gives the embedded form.</summary>
    public const string ResourceName = "ClassroomAgent.Infrastructure.Export.group-lessons-blank.xlsx";

    /// <summary>Spec FR-002: the marker text of each field, the whole text of its cell.</summary>
    private static readonly IReadOnlyDictionary<string, BlankFormMarker> Markers = new Dictionary<string, BlankFormMarker>(StringComparer.Ordinal)
    {
        ["{{course}}"] = BlankFormMarker.Course,
        ["{{student.no}}"] = BlankFormMarker.StudentNumber,
        ["{{student.name}}"] = BlankFormMarker.StudentName,
        ["{{lesson.date}}"] = BlankFormMarker.LessonDate,
        ["{{grade}}"] = BlankFormMarker.Grade,
        ["{{teachers}}"] = BlankFormMarker.Teachers,
        ["{{topic.no}}"] = BlankFormMarker.TopicNumber,
        ["{{topic.date}}"] = BlankFormMarker.TopicDate,
        ["{{topic.hours}}"] = BlankFormMarker.TopicHours,
        ["{{topic.title}}"] = BlankFormMarker.TopicTitle,
    };

    public Task<byte[]> RenderAsync(BlankFormFill fill, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fill);

        using var resource = typeof(ClosedXmlBlankFormRenderer).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded group-lessons blank form is missing.");
        using var book = new XLWorkbook(resource);
        var forms = book.Worksheets.ToList();
        var places = FindMarkers(forms);
        var sources = new Dictionary<BlankFormSheet, IXLWorksheet>
        {
            [BlankFormSheet.Grading] = places[BlankFormMarker.Course].Sheet,
            [BlankFormSheet.Topics] = places[BlankFormMarker.Teachers].Sheet,
        };

        // FR-006: each copy is a copy of its form sheet; the form sheets do not remain in the file.
        var copies = new List<(IXLWorksheet Sheet, string Name)>();
        for (var i = 0; i < fill.Copies.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var copy = fill.Copies[i];
            var source = sources[copy.Sheet];
            var target = source.CopyTo("~copy" + i.ToString(CultureInfo.InvariantCulture));
            foreach (var placement in copy.Placements)
            {
                var place = places[placement.Marker];
                if (!ReferenceEquals(place.Sheet, source))
                {
                    throw new InvalidOperationException("A marker of the group-lessons blank form is on the wrong sheet.");
                }

                var cell = target.Cell(place.Row + placement.Down, place.Column + placement.Right);
                Write(cell, placement.Value, place.NumberFormat);
                ExportMarks.Apply(cell, placement.Value);
            }

            // FR-006.1, I-13: the copy's number as fixed text in its footer section; the header and the rest stay empty.
            var number = copy.Number.ToString(CultureInfo.InvariantCulture);
            var section = copy.FooterSection == BlankFormFooterSection.Left ? target.PageSetup.Footer.Left : target.PageSetup.Footer.Right;
            section.AddText(number, XLHFOccurrence.AllPages);
            copies.Add((target, copy.IsNumbered ? source.Name + " " + number : source.Name));
        }

        foreach (var form in forms)
        {
            form.Delete();
        }

        foreach (var (sheet, name) in copies)
        {
            sheet.Name = name;
        }

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    /// <summary>Spec FR-003, I-2: every marker exactly once in the form; anything else is a program defect.</summary>
    private static Dictionary<BlankFormMarker, (IXLWorksheet Sheet, int Row, int Column, string NumberFormat)> FindMarkers(
        IReadOnlyList<IXLWorksheet> forms)
    {
        var places = new Dictionary<BlankFormMarker, (IXLWorksheet Sheet, int Row, int Column, string NumberFormat)>();
        foreach (var sheet in forms)
        {
            foreach (var cell in sheet.CellsUsed(c => c.DataType == XLDataType.Text))
            {
                if (Markers.TryGetValue(cell.GetString(), out var marker)
                    && !places.TryAdd(marker, (sheet, cell.Address.RowNumber, cell.Address.ColumnNumber, cell.Style.NumberFormat.Format)))
                {
                    throw new InvalidOperationException("A marker of the group-lessons blank form occurs more than once.");
                }
            }
        }

        if (places.Count != Markers.Count)
        {
            throw new InvalidOperationException("A marker of the group-lessons blank form is missing.");
        }

        return places;
    }

    /// <summary>
    /// FR-005.3: only the value is set — the cell keeps the form's own style — and on a date cell the marker cell's number
    /// format. Text is a string value, never a formula, with the quote prefix where the model asks for it (US-028
    /// FR-004.7). An empty value clears the value and any formula; the lines stay (FR-005.5, FR-005.6).
    /// </summary>
    private static void Write(IXLCell target, WorkbookCell cell, string dateFormat)
    {
        switch (cell.Kind)
        {
            case WorkbookCellKind.Text:
                target.SetValue(cell.Text ?? string.Empty);
                if (cell.QuotePrefix)
                {
                    target.Style.IncludeQuotePrefix = true;
                }

                break;
            case WorkbookCellKind.Number when cell.Number is { } number:
                target.SetValue(number);
                break;
            case WorkbookCellKind.Date when cell.Date is { } date:
                target.SetValue(date.ToDateTime(TimeOnly.MinValue));
                target.Style.NumberFormat.Format = dateFormat;
                break;
            case WorkbookCellKind.Empty:
                target.Clear(XLClearOptions.Contents);
                break;
            default:
                throw new InvalidOperationException("The group-lessons blank form holds text, numbers and dates only.");
        }
    }
}
