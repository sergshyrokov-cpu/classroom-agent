using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using ClosedXML.Excel;
using static ClassroomAgent.Tests.TestInfrastructure.DataMarksHostExtensions;
using W = ClassroomAgent.Tests.TestInfrastructure.DataMarksWorld;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-063 in the Excel export over HTTP (TC-2; openapi <c>exportJournalXlsx</c>, contract unchanged): the "generated"
/// and "data from Google as of" lines in a note on the course-name cell — the program layout's Course value cell on both
/// sheets (FR-004, I-4) and every <c>{{course}}</c> cell of the blank form (FR-005, OD-003 a) — after an existing note
/// (FR-006.2, I-5), never in a printed cell. The clock moves seven minutes after seeding, so G2 is the moment of export.
/// </summary>
public sealed class GeneratedLinesExportTests(PostgreSqlFixture database)
{
    /// <summary>The Course row of the program layout's header block and its value cell (label in A, course in B).</summary>
    private const int CourseRow = 2;

    private const int CourseValueColumn = 2;

    private const int HeaderLineColumn = 3;

    private static readonly TimeSpan ToExport = TimeSpan.FromMinutes(7);

    public static TheoryData<string> Languages => new("uk", "en");

    private sealed record Exported(XLWorkbook Program, XLWorkbook Blank, DateTimeOffset G2, GeneratedLinesTexts Texts) : IDisposable
    {
        public void Dispose()
        {
            Program.Dispose();
            Blank.Dispose();
        }
    }

    private async Task<Exported> ExportAsync(
        string language,
        CourseHeaderVariant variant = CourseHeaderVariant.None,
        bool lastReadKnown = true,
        int? studentsPerCopy = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct, variant);
        if (!lastReadKnown)
        {
            await host.ExecuteAsync(
                "UPDATE course SET last_complete_read_at = NULL WHERE id = @id", ct, ("id", world.CourseId));
        }

        host.Time.Advance(ToExport);
        var g2 = host.Time.GetUtcNow();
        var program = await client.ExportAsync(JournalExportHostExtensions.September(world.CourseId, world.TemplateId), ct);
        var blank = await client.ExportAsync(
            GroupLessonsBlank.Body(world.CourseId, studentsPerCopy: studentsPerCopy, template: world.TemplateId), ct);

        Assert.Equal(HttpStatusCode.OK, program.Status);
        Assert.Equal(HttpStatusCode.OK, blank.Status);
        Assert.Equal(g2, host.Time.GetUtcNow());
        Assert.Equal(0, host.GoogleCallCount());
        return new Exported(program.Workbook(), blank.Workbook(), g2, GeneratedLinesTexts.Of(language));
    }

    /// <summary>
    /// AC-003, AC-005, FR-004.1, FR-004.2, I-4: the Course value cell of both program sheets keeps the course name and
    /// carries exactly the two lines (G2, R2), one per line; the label cell and the third cell stay as they were.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheProgramLayout_CarriesBothLines_InANoteOnTheCourseCellOfBothSheets(string language)
    {
        using var exported = await ExportAsync(language);
        var texts = exported.Texts;
        Assert.Equal(2, exported.Program.Worksheets.Count);

        foreach (var sheet in exported.Program.Worksheets)
        {
            var course = sheet.Cell(CourseRow, CourseValueColumn);
            Assert.Equal(W.CourseName, course.GetString());
            Assert.Equal([texts.GeneratedLine(exported.G2), texts.DataAsOfLine(W.R2)], NoteLines(course));
            Assert.False(sheet.Cell(CourseRow, 1).HasComment);
            Assert.True(sheet.Cell(CourseRow, HeaderLineColumn).IsEmpty());
        }
    }

    /// <summary>
    /// AC-003, AC-005, FR-005.1, FR-005.4: the blank's <c>{{course}}</c> cell keeps the course name and carries the two
    /// lines; the topics copy has no course cell and no new note.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheBlankForm_CarriesBothLines_InANoteOnTheCourseCell(string language)
    {
        using var exported = await ExportAsync(language);
        var texts = exported.Texts;
        var course = exported.Blank.Worksheet(GroupLessonsBlank.GradingSheet)
            .Cell(GroupLessonsBlank.CourseRow, GroupLessonsBlank.CourseColumn);

        Assert.Equal(W.CourseName, course.GetString());
        Assert.Equal([texts.GeneratedLine(exported.G2), texts.DataAsOfLine(W.R2)], NoteLines(course));
        foreach (var cell in exported.Blank.Worksheet(GroupLessonsBlank.TopicsSheet).CellsUsed(c => c.HasComment))
        {
            Assert.DoesNotContain(texts.Stems[0], cell.GetComment().Text, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-003, FR-004.1, FR-005.4: no printed cell of either file holds either line or its label.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task NoPrintedCell_HoldsEitherLine(string language)
    {
        using var exported = await ExportAsync(language);
        var texts = exported.Texts;
        var forbidden = texts.Stems
            .Append(texts.Instant(exported.G2))
            .Append(texts.Instant(W.R2))
            .ToList();

        foreach (var sheet in exported.Program.Worksheets.Concat(exported.Blank.Worksheets))
        {
            foreach (var cell in sheet.CellsUsed())
            {
                foreach (var text in forbidden)
                {
                    Assert.DoesNotContain(text, cell.GetString(), StringComparison.Ordinal);
                }
            }
        }
    }

    /// <summary>
    /// AC-004, FR-005.2, FR-006.2, VR-004, I-5: the blank's course cell already carries US-060's header line as a note —
    /// the note holds that line first, then both lines; the program layout keeps its header line in the printed third
    /// cell and its course cell holds only the two lines.
    /// </summary>
    [Fact]
    public async Task AnExistingNote_IsKeptFirst_ThenBothLines()
    {
        using var exported = await ExportAsync("uk", CourseHeaderVariant.NotInClassroom);
        var texts = exported.Texts;
        var headerLine = W.HeaderLine(CourseHeaderVariant.NotInClassroom, DataMarksTexts.Uk)!;
        var blankCourse = exported.Blank.Worksheet(GroupLessonsBlank.GradingSheet)
            .Cell(GroupLessonsBlank.CourseRow, GroupLessonsBlank.CourseColumn);
        var programSheet = exported.Program.Worksheet(1);

        Assert.Equal(
            [headerLine, texts.GeneratedLine(exported.G2), texts.DataAsOfLine(W.R2)],
            NoteLines(blankCourse));
        Assert.Equal(headerLine, programSheet.Cell(CourseRow, HeaderLineColumn).GetString());
        Assert.Equal(
            [texts.GeneratedLine(exported.G2), texts.DataAsOfLine(W.R2)],
            NoteLines(programSheet.Cell(CourseRow, CourseValueColumn)));
    }

    /// <summary>AC-006, FR-002.3, OD-001 a: no last read recorded — both notes say "unknown".</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task WithNoLastReadRecorded_TheNotesSayUnknown(string language)
    {
        using var exported = await ExportAsync(language, lastReadKnown: false);
        var texts = exported.Texts;
        var expected = new[] { texts.GeneratedLine(exported.G2), texts.DataAsOfLine(null) };

        Assert.Equal(expected, NoteLines(exported.Program.Worksheet(1).Cell(CourseRow, CourseValueColumn)));
        Assert.Equal(
            expected,
            NoteLines(exported.Blank.Worksheet(GroupLessonsBlank.GradingSheet)
                .Cell(GroupLessonsBlank.CourseRow, GroupLessonsBlank.CourseColumn)));
    }

    /// <summary>
    /// AC-008, FR-005.3, OD-003 a: two students per copy split the five students into three grading copies — every
    /// copy's <c>{{course}}</c> cell carries the note.
    /// </summary>
    [Fact]
    public async Task EveryGradingCopy_CarriesTheNote()
    {
        using var exported = await ExportAsync("uk", studentsPerCopy: 2);
        var texts = exported.Texts;
        var copies = exported.Blank.Worksheets
            .Where(s => s.Name.StartsWith(GroupLessonsBlank.GradingSheet, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, copies.Count);
        foreach (var copy in copies)
        {
            var course = copy.Cell(GroupLessonsBlank.CourseRow, GroupLessonsBlank.CourseColumn);
            Assert.Equal(W.CourseName, course.GetString());
            Assert.Equal([texts.GeneratedLine(exported.G2), texts.DataAsOfLine(W.R2)], NoteLines(course));
        }
    }

    /// <summary>A note's lines (VR-004: no empty line), or none when the cell has no note.</summary>
    private static string[] NoteLines(IXLCell cell) =>
        Note(cell) is { } note ? note.Replace("\r\n", "\n", StringComparison.Ordinal).Trim().Split('\n') : [];
}
