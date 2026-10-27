using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using ClosedXML.Excel;
using static ClassroomAgent.Tests.TestInfrastructure.DataMarksHostExtensions;
using W = ClassroomAgent.Tests.TestInfrastructure.DataMarksWorld;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-060 in the program-layout export over HTTP (TC-2; openapi <c>exportJournalXlsx</c>, contract unchanged), the file
/// read back with ClosedXML from the response bytes: colour and note on an above-maximum cell (FR-007.3), the item mark
/// after a line break in its heading and topic (FR-007.2), the header line in the third cell of the Course row
/// (FR-007.1, I-3) and a note on a leaver's name cell (FR-007.4), Ukrainian and English (FR-009).
/// </summary>
public sealed class ReportMarksExportTests(PostgreSqlFixture database)
{
    /// <summary>The Course row of the header block (row 2) and the cell after its label and value.</summary>
    private const int CourseRow = 2;

    private const int HeaderLineColumn = 3;

    public static TheoryData<string> Languages => new("uk", "en");

    private async Task<(XLWorkbook Workbook, DataMarksTexts Texts)> ExportAsync(
        string language, CourseHeaderVariant variant = CourseHeaderVariant.None)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct, variant);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(world.CourseId, world.TemplateId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(0, host.GoogleCallCount());
        return (response.Workbook(), DataMarksTexts.Of(language));
    }

    /// <summary>
    /// AC-004, AC-009, FR-007.3: the grade 15 of 12 keeps the scale value, is coloured and carries "Points: 15 of 12";
    /// the draft-only cell keeps its text, is coloured the same and carries "Draft: 13 of 12"; the grade 12 of 12 has
    /// neither colour nor note.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task AnAboveMaximumCell_KeepsItsValue_IsColoured_AndCarriesTheRawPoints(string language)
    {
        var (workbook, texts) = await ExportAsync(language);
        using var _workbook = workbook;
        var sheet = workbook.Worksheet(1);

        var over = ProgramCell(sheet, W.Over, W.GradedTitle);
        var draft = ProgramCell(sheet, W.Draft, W.GradedTitle);
        var equal = ProgramCell(sheet, W.Equal, W.GradedTitle);
        Assert.Equal(W.Full, over.GetString());
        Assert.Contains(W.Full, draft.GetString(), StringComparison.Ordinal);
        Assert.Equal(W.Full, equal.GetString());

        Assert.True(IsColouredUnlike(over, equal), "The above-maximum grade cell must be coloured.");
        Assert.True(IsColouredUnlike(draft, equal), "The above-maximum draft cell must be coloured.");
        Assert.Equal(over.Style.Fill.BackgroundColor, draft.Style.Fill.BackgroundColor);
        Assert.Contains(texts.Note(texts.NoteGrade, W.OverGrade, W.GradedMaximum), Note(over), StringComparison.Ordinal);
        Assert.Contains(texts.Note(texts.NoteDraft, W.DraftGrade, W.GradedMaximum), Note(draft), StringComparison.Ordinal);
        Assert.DoesNotContain(texts.Note(texts.NoteGrade, W.DraftGrade, W.GradedMaximum), Note(draft) ?? string.Empty, StringComparison.Ordinal);
        Assert.Null(Note(equal));
        Assert.Equal(XLFillPatternValues.None, equal.Style.Fill.PatternType);
        Assert.Null(Note(ProgramCell(sheet, W.Gone, W.DeletedTitle)));
    }

    /// <summary>
    /// AC-005, AC-009, FR-005, FR-007.2: the deleted item's heading is its title, a line break, then "not in Classroom"
    /// (after the existing date line); its grades are there; on «Теми занять» its topic is the title, a line break, then
    /// the mark; the ordinary item has neither.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheDeletedItem_IsMarkedInItsHeadingAndTopic(string language)
    {
        var (workbook, texts) = await ExportAsync(language);
        using var _workbook = workbook;
        var grading = workbook.Worksheet(1);
        var topics = workbook.Worksheet(2);

        var deletedHeading = ProgramHeading(grading, W.DeletedTitle).GetString().Split('\n');
        Assert.Equal(W.DeletedTitle, deletedHeading[0]);
        Assert.Contains(texts.ItemNotInClassroom, deletedHeading.Skip(1));
        Assert.DoesNotContain(texts.ItemNotInClassroom, ProgramHeading(grading, W.GradedTitle).GetString(), StringComparison.Ordinal);
        Assert.Equal("TSMID", ProgramCell(grading, W.Gone, W.DeletedTitle).GetString());

        Assert.Equal(W.DeletedTitle + "\n" + texts.ItemNotInClassroom, ProgramTopic(topics, W.DeletedTitle).GetString());
        Assert.Equal(W.GradedTitle, ProgramTopic(topics, W.GradedTitle).GetString());
    }

    /// <summary>
    /// AC-008, AC-009, FR-007.4: the names are unchanged; the leaver's name cell carries the note "left 10.09.2026", the
    /// submitter's "left"; names on the roster carry no note.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task ALeaversNameCell_KeepsTheName_AndCarriesTheLeftNote(string language)
    {
        var (workbook, texts) = await ExportAsync(language);
        using var _workbook = workbook;
        var sheet = workbook.Worksheet(1);

        var gone = ProgramName(sheet, W.Gone);
        var submitter = ProgramName(sheet, W.Submitter);
        Assert.DoesNotContain(texts.Left, gone.GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(texts.Left, submitter.GetString(), StringComparison.Ordinal);
        Assert.Contains(texts.With(texts.LeftOn, W.LeftLastSeen), Note(gone), StringComparison.Ordinal);
        var submitterNote = Note(submitter);
        Assert.NotNull(submitterNote);
        Assert.True(HasUndatedLeft(submitterNote, texts), submitterNote);
        Assert.DoesNotContain(texts.Date(W.SubmitterSeen), submitterNote, StringComparison.Ordinal);
        foreach (var onRoster in new[] { W.Over, W.Draft, W.Equal })
        {
            Assert.Null(Note(ProgramName(sheet, onRoster)));
        }
    }

    /// <summary>
    /// AC-006, AC-007, AC-011, AC-012, FR-007.1, I-3: the header line of each stored state is the third cell of the
    /// Course row; the table does not move (its heading stays on row 6); no line where the variant has none.
    /// </summary>
    [Theory]
    [InlineData(CourseHeaderVariant.None, "uk")]
    [InlineData(CourseHeaderVariant.NotInClassroom, "uk")]
    [InlineData(CourseHeaderVariant.NotInClassroom, "en")]
    [InlineData(CourseHeaderVariant.NotVisible, "uk")]
    [InlineData(CourseHeaderVariant.Skipped, "uk")]
    [InlineData(CourseHeaderVariant.Skipped, "en")]
    [InlineData(CourseHeaderVariant.SkippedNeverRead, "uk")]
    [InlineData(CourseHeaderVariant.NotInClassroomAndSkipped, "uk")]
    [InlineData(CourseHeaderVariant.ArchivedUnchanged, "uk")]
    public async Task TheCourseHeaderLine_IsTheThirdCellOfTheCourseRow(CourseHeaderVariant variant, string language)
    {
        var (workbook, texts) = await ExportAsync(language, variant);
        using var _workbook = workbook;
        var sheet = workbook.Worksheet(1);
        var line = W.HeaderLine(variant, texts);

        Assert.Equal(W.CourseName, sheet.Cell(CourseRow, 2).GetString());
        if (line is null)
        {
            Assert.True(sheet.Cell(CourseRow, HeaderLineColumn).IsEmpty());
        }
        else
        {
            Assert.Equal(line, sheet.Cell(CourseRow, HeaderLineColumn).GetString());
        }

        var all = string.Join('\n', sheet.CellsUsed().Select(c => c.GetString()));
        var rest = line is null ? all : all.Replace(line, string.Empty, StringComparison.Ordinal);
        foreach (var stem in W.HeaderStems(texts))
        {
            Assert.DoesNotContain(stem, rest, StringComparison.Ordinal);
        }

        Assert.Equal(W.Full, ProgramCell(sheet, W.Over, W.GradedTitle).GetString());
    }
}
