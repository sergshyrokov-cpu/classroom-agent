using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ClassroomAgent.Tests.TestInfrastructure;
using Blank = ClassroomAgent.Tests.TestInfrastructure.GroupLessonsBlank;

namespace ClassroomAgent.Tests.Infrastructure.Export;

/// <summary>
/// US-043 AC-013, FR-001 … FR-003, S-05, S-06: the shipped form is the school's blank with exactly the markers, the two
/// date formats and the footer margin added; every marker occurs once; the capacity areas lie inside the drawn lines;
/// and the program embeds this very file (I-1). Read cell by cell, never compared by bytes except for the embedding.
/// </summary>
public sealed class GroupLessonsBlankFormTests
{
    private const string GradingRange = "A1:AB45";

    private const string TopicsRange = "A1:H32";

    private static readonly Regex Marker = new(@"\{\{[^}]*\}\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>FR-002: the cell that carries each marker and nothing else, the date marker cells and the replaced numbers.</summary>
    private static readonly HashSet<string> MarkerCells = [.. Blank.Markers.Values.Select(m => m.Sheet + "!" + m.Cell)];

    private static readonly HashSet<string> DateCells = [Blank.GradingSheet + "!C8", Blank.TopicsSheet + "!B4"];

    /// <summary>FR-001, OD-009 a: the form is versioned next to the reference.</summary>
    [Fact]
    public void TheShippedForm_Exists()
    {
        Assert.True(File.Exists(Blank.FormPath()), "docs/product/report-templates/group-lessons-blank.xlsx must be shipped.");
    }

    /// <summary>FR-002: every marker exactly once in the file, on its sheet and at its cell; no other marker text.</summary>
    [Fact]
    public void EveryMarker_OccursExactlyOnce_AtItsCell()
    {
        using var form = Blank.Open(Blank.FormPath());

        var found = form.Worksheets
            .SelectMany(w => w.CellsUsed().Select(c => (Sheet: w.Name, Cell: c.Address.ToString()!, Text: c.GetString())))
            .Where(c => Marker.IsMatch(c.Text))
            .ToList();

        Assert.Equal(Blank.Markers.Count, found.Count);
        foreach (var (marker, (sheet, cell)) in Blank.Markers)
        {
            var hit = Assert.Single(found, f => f.Text == marker);
            Assert.Equal(sheet, hit.Sheet);
            Assert.Equal(cell, hit.Cell);
        }
    }

    /// <summary>FR-001, FR-006: two sheets, the reference's names and order.</summary>
    [Fact]
    public void TheSheets_AreTheReferencesSheets()
    {
        using var form = Blank.Open(Blank.FormPath());
        using var reference = Blank.Open(Blank.ReferencePath());

        Assert.Equal([Blank.GradingSheet, Blank.TopicsSheet], form.Worksheets.Select(w => w.Name));
        Assert.Equal(reference.Worksheets.Select(w => w.Name), form.Worksheets.Select(w => w.Name));
    }

    /// <summary>
    /// AC-013, S-06: apart from the marker cells every cell holds the reference's value and formula (so no personal
    /// data), and every cell keeps the reference's lines, font, alignment and number format — except the two date cells,
    /// whose format is <c>dd.mm.yyyy</c> (I-4).
    /// </summary>
    [Theory]
    [InlineData(Blank.GradingSheet, GradingRange)]
    [InlineData(Blank.TopicsSheet, TopicsRange)]
    public void EveryOtherCell_IsTheReferences(string sheetName, string range)
    {
        using var form = Blank.Open(Blank.FormPath());
        using var reference = Blank.Open(Blank.ReferencePath());
        var sheet = form.Worksheet(sheetName);
        var original = reference.Worksheet(sheetName);

        foreach (var expected in original.Range(range).Cells())
        {
            var key = sheetName + "!" + expected.Address;
            var actual = sheet.Cell(expected.Address.RowNumber, expected.Address.ColumnNumber);
            if (!MarkerCells.Contains(key))
            {
                Assert.Equal(expected.HasFormula, actual.HasFormula);
                Assert.Equal(expected.HasFormula ? expected.FormulaA1 : expected.GetString(), actual.HasFormula ? actual.FormulaA1 : actual.GetString());
            }

            GroupLessonsBlankHostExtensions.AssertSameBorder(expected, actual, sheetName);
            Assert.Equal(expected.Style.Font.FontName, actual.Style.Font.FontName);
            Assert.Equal(expected.Style.Font.FontSize, actual.Style.Font.FontSize);
            Assert.Equal(expected.Style.Font.Bold, actual.Style.Font.Bold);
            Assert.Equal(expected.Style.Alignment.WrapText, actual.Style.Alignment.WrapText);
            Assert.Equal(expected.Style.Alignment.TextRotation, actual.Style.Alignment.TextRotation);
            Assert.Equal(expected.Style.Alignment.Horizontal, actual.Style.Alignment.Horizontal);
            Assert.Equal(expected.Style.Alignment.Vertical, actual.Style.Alignment.Vertical);
            Assert.Equal(
                DateCells.Contains(key) ? Blank.DateFormat : expected.Style.NumberFormat.Format,
                actual.Style.NumberFormat.Format);
        }
    }

    /// <summary>FR-005.3: the course and teachers cells do not wrap, so the text runs over the empty cells to the right.</summary>
    [Fact]
    public void TheCourseAndTeachersCells_DoNotWrap()
    {
        using var form = Blank.Open(Blank.FormPath());

        Assert.False(form.Worksheet(Blank.GradingSheet).Cell(Blank.CourseRow, Blank.CourseColumn).Style.Alignment.WrapText);
        Assert.False(form.Worksheet(Blank.TopicsSheet).Cell(Blank.TeachersRow, Blank.TeachersColumn).Style.Alignment.WrapText);
    }

    /// <summary>AC-013: widths, heights and page setup are the reference's; the only print change is the 0.5 cm footer margin (I-14).</summary>
    [Theory]
    [InlineData(Blank.GradingSheet, 28, 45)]
    [InlineData(Blank.TopicsSheet, 8, 32)]
    public void WidthsHeightsAndPageSetup_AreTheReferences_ExceptTheFooterMargin(string sheetName, int columns, int rows)
    {
        using var form = Blank.Open(Blank.FormPath());
        using var reference = Blank.Open(Blank.ReferencePath());
        var sheet = form.Worksheet(sheetName);
        var original = reference.Worksheet(sheetName);

        for (var column = 1; column <= columns; column++)
        {
            Assert.Equal(original.Column(column).Width, sheet.Column(column).Width, 3);
        }

        for (var row = 1; row <= rows; row++)
        {
            Assert.Equal(original.Row(row).Height, sheet.Row(row).Height, 3);
        }

        Assert.Equal(original.PageSetup.PageOrientation, sheet.PageSetup.PageOrientation);
        Assert.Equal(original.PageSetup.PaperSize, sheet.PageSetup.PaperSize);
        Assert.Equal(original.PageSetup.Margins.Left, sheet.PageSetup.Margins.Left, 4);
        Assert.Equal(original.PageSetup.Margins.Right, sheet.PageSetup.Margins.Right, 4);
        Assert.Equal(original.PageSetup.Margins.Top, sheet.PageSetup.Margins.Top, 4);
        Assert.Equal(original.PageSetup.Margins.Bottom, sheet.PageSetup.Margins.Bottom, 4);
        Assert.Equal(original.PageSetup.Margins.Header, sheet.PageSetup.Margins.Header, 4);
        Assert.Equal(Blank.FooterMarginInches, sheet.PageSetup.Margins.Footer, 3);
        Assert.True(sheet.PageSetup.Margins.Footer < sheet.PageSetup.Margins.Bottom, "The footer must sit inside the bottom margin.");
        Assert.Equal(original.PageSetup.Scale, sheet.PageSetup.Scale);
        Assert.Empty(sheet.MergedRanges);
        Assert.Equal(string.Empty, sheet.PageSetup.Footer.Left.GetText(XLHFOccurrence.AllPages));
        Assert.Equal(string.Empty, sheet.PageSetup.Footer.Right.GetText(XLHFOccurrence.AllPages));
    }

    /// <summary>
    /// FR-003, AC-013: the student (30 rows), lesson (24 columns) and topic (23 rows) areas starting at the markers lie
    /// inside the form's drawn lines, and reach their last line exactly.
    /// </summary>
    [Fact]
    public void TheCapacityAreas_LieInsideTheDrawnLines()
    {
        using var form = Blank.Open(Blank.FormPath());

        var grading = Lined(form.Worksheet(Blank.GradingSheet), GradingRange);
        Assert.True(grading.FirstRow <= Blank.LessonDateRow && grading.FirstColumn <= Blank.StudentNumberColumn);
        Assert.Equal(Blank.FirstStudentRow + Blank.StudentRows - 1, grading.LastRow);
        Assert.Equal(Blank.FirstLessonColumn + Blank.LessonColumns - 1, grading.LastColumn);

        var topics = Lined(form.Worksheet(Blank.TopicsSheet), TopicsRange);
        Assert.True(topics.FirstRow < Blank.FirstTopicRow && topics.FirstColumn <= Blank.TopicNumberColumn);
        Assert.Equal(Blank.FirstTopicRow + Blank.TopicRows - 1, topics.LastRow);
        Assert.True(topics.LastColumn >= Blank.TopicSignatureColumn);
    }

    /// <summary>I-1, S-05: the program embeds this very file — the only form it can fill.</summary>
    [Fact]
    public void TheInfrastructureAssembly_EmbedsTheShippedForm()
    {
        var assembly = typeof(ClassroomAgent.Infrastructure.Persistence.ClassroomAgentDbContext).Assembly;
        var name = Assert.Single(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith("group-lessons-blank.xlsx", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        Assert.Equal(File.ReadAllBytes(Blank.FormPath()), copy.ToArray());
    }

    /// <summary>The bounding box of every cell that carries any line.</summary>
    private static (int FirstRow, int FirstColumn, int LastRow, int LastColumn) Lined(IXLWorksheet sheet, string range)
    {
        var cells = sheet.Range(range).Cells()
            .Where(c => c.Style.Border.TopBorder != XLBorderStyleValues.None
                || c.Style.Border.BottomBorder != XLBorderStyleValues.None
                || c.Style.Border.LeftBorder != XLBorderStyleValues.None
                || c.Style.Border.RightBorder != XLBorderStyleValues.None)
            .ToList();
        Assert.NotEmpty(cells);
        return (
            cells.Min(c => c.Address.RowNumber),
            cells.Min(c => c.Address.ColumnNumber),
            cells.Max(c => c.Address.RowNumber),
            cells.Max(c => c.Address.ColumnNumber));
    }
}
