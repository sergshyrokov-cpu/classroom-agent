using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using ClosedXML.Excel;
using static ClassroomAgent.Tests.TestInfrastructure.DataMarksHostExtensions;
using W = ClassroomAgent.Tests.TestInfrastructure.DataMarksWorld;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-060 in the «Бланк для групових занять» export over HTTP (TC-2; OD-001 a, spec FR-008): notes and the fill on the
/// form's own cells, its printed text unchanged — the same values as without the marks. One copy of each sheet: five
/// students and two lessons fit the default per-copy sizes.
/// </summary>
public sealed class BlankFormMarksExportTests(PostgreSqlFixture database)
{
    public static TheoryData<string> Languages => new("uk", "en");

    private async Task<(XLWorkbook Blank, XLWorkbook Program, DataMarksTexts Texts)> ExportAsync(
        string language, CourseHeaderVariant variant = CourseHeaderVariant.None)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct, variant);

        var blank = await client.ExportAsync(GroupLessonsBlank.Body(world.CourseId, template: world.TemplateId), ct);
        var program = await client.ExportAsync(JournalExportHostExtensions.September(world.CourseId, world.TemplateId), ct);

        Assert.Equal(HttpStatusCode.OK, blank.Status);
        Assert.Equal(HttpStatusCode.OK, program.Status);
        Assert.Equal(0, host.GoogleCallCount());
        return (blank.Workbook(), program.Workbook(), DataMarksTexts.Of(language));
    }

    private static IXLWorksheet Grading(XLWorkbook workbook) => workbook.Worksheet(GroupLessonsBlank.GradingSheet);

    private static IXLWorksheet Topics(XLWorkbook workbook) => workbook.Worksheet(GroupLessonsBlank.TopicsSheet);

    /// <summary>
    /// AC-004, AC-009, FR-008.1, FR-007.5: the grade cell of 15 of 12 keeps its value, carries the same fill as the
    /// program layout's cell and the note "Points: 15 of 12"; the draft cell carries "Draft: 13 of 12"; the grade 12 of
    /// 12 has neither.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task AnAboveMaximumGradeCell_IsColouredLikeTheProgramLayout_AndCarriesTheNote(string language)
    {
        var (blank, program, texts) = await ExportAsync(language);
        using var _blank = blank;
        using var _program = program;
        var sheet = Grading(blank);

        var over = BlankGrade(sheet, W.Over, W.GradedDate);
        var draft = BlankGrade(sheet, W.Draft, W.GradedDate);
        var equal = BlankGrade(sheet, W.Equal, W.GradedDate);
        Assert.Equal(W.Full, over.GetString());
        Assert.Equal(W.Full, equal.GetString());
        Assert.True(IsColouredUnlike(over, equal), "The above-maximum grade cell must be coloured.");
        Assert.True(IsColouredUnlike(draft, equal), "The above-maximum draft cell must be coloured.");
        Assert.Equal(
            ProgramCell(program.Worksheet(1), W.Over, W.GradedTitle).Style.Fill.BackgroundColor,
            over.Style.Fill.BackgroundColor);
        Assert.Contains(texts.Note(texts.NoteGrade, W.OverGrade, W.GradedMaximum), Note(over), StringComparison.Ordinal);
        Assert.Contains(texts.Note(texts.NoteDraft, W.DraftGrade, W.GradedMaximum), Note(draft), StringComparison.Ordinal);
        Assert.Null(Note(equal));
    }

    /// <summary>AC-008, AC-009, FR-008.2: the name cells keep the names; the leaver's and the submitter's carry the "left" note.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task ALeaversNameCell_KeepsTheName_AndCarriesTheLeftNote(string language)
    {
        var (blank, program, texts) = await ExportAsync(language);
        using var _blank = blank;
        using var _program = program;
        var sheet = Grading(blank);

        var gone = BlankName(sheet, W.Gone);
        var submitter = BlankName(sheet, W.Submitter);
        Assert.Equal(ProgramName(program.Worksheet(1), W.Gone).GetString(), gone.GetString());
        Assert.Equal(ProgramName(program.Worksheet(1), W.Submitter).GetString(), submitter.GetString());
        Assert.Contains(texts.With(texts.LeftOn, W.LeftLastSeen), Note(gone), StringComparison.Ordinal);
        var submitterNote = Note(submitter);
        Assert.NotNull(submitterNote);
        Assert.True(HasUndatedLeft(submitterNote, texts), submitterNote);
        Assert.Null(Note(BlankName(sheet, W.Over)));
    }

    /// <summary>
    /// AC-006, AC-007, AC-011, AC-012, FR-008.3, I-4: the <c>{{course}}</c> cell keeps the course text and carries the
    /// header line as the first line of its note; no header line where the variant has none (US-063 adds its two lines
    /// after it).
    /// </summary>
    [Theory]
    [InlineData(CourseHeaderVariant.None, "uk")]
    [InlineData(CourseHeaderVariant.NotInClassroom, "uk")]
    [InlineData(CourseHeaderVariant.NotInClassroom, "en")]
    [InlineData(CourseHeaderVariant.NotVisible, "uk")]
    [InlineData(CourseHeaderVariant.Skipped, "uk")]
    [InlineData(CourseHeaderVariant.SkippedNeverRead, "uk")]
    [InlineData(CourseHeaderVariant.NotInClassroomAndSkipped, "uk")]
    [InlineData(CourseHeaderVariant.ArchivedUnchanged, "uk")]
    public async Task TheCourseCell_CarriesTheHeaderLineAsANote(CourseHeaderVariant variant, string language)
    {
        var (blank, program, texts) = await ExportAsync(language, variant);
        using var _blank = blank;
        using var _program = program;
        var course = Grading(blank).Cell(GroupLessonsBlank.CourseRow, GroupLessonsBlank.CourseColumn);
        var line = W.HeaderLine(variant, texts);

        // US-063 FR-005.2, I-5: the note now also carries "generated" and "data from Google as of" after the header line,
        // so the header line is the note's first line, and a variant without one leaves none of its stems in the note.
        Assert.Equal(W.CourseName, course.GetString());
        var note = Note(course);
        Assert.NotNull(note);
        var lines = note.Replace("\r\n", "\n", StringComparison.Ordinal).Trim().Split('\n');
        if (line is null)
        {
            Assert.StartsWith(GeneratedLinesTexts.Of(texts.Language).Stems[0], lines[0], StringComparison.Ordinal);
            foreach (var stem in W.HeaderStems(texts))
            {
                Assert.DoesNotContain(stem, note, StringComparison.Ordinal);
            }
        }
        else
        {
            Assert.Equal(line, lines[0]);
            Assert.Equal(3, lines.Length);
        }
    }

    /// <summary>
    /// AC-005, FR-008.4, FR-008.5: the deleted item's topic title keeps its text and carries the note "not in Classroom";
    /// the ordinary topic has none; no cell of the form changes its printed text.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheDeletedItemsTopic_CarriesTheMarkAsANote_AndThePrintedTextIsUnchanged(string language)
    {
        var (blank, program, texts) = await ExportAsync(language);
        using var _blank = blank;
        using var _program = program;
        var topics = Topics(blank);

        Assert.Equal(texts.ItemNotInClassroom, Note(BlankTopic(topics, W.DeletedTitle))?.Trim());
        Assert.Null(Note(BlankTopic(topics, W.GradedTitle)));
        foreach (var sheet in blank.Worksheets)
        {
            var printed = string.Join('\n', sheet.CellsUsed().Select(c => c.GetString()));
            Assert.DoesNotContain(texts.ItemNotInClassroom, printed, StringComparison.Ordinal);
            Assert.DoesNotContain(texts.AboveMaximum, printed, StringComparison.Ordinal);
            Assert.DoesNotContain(texts.With(texts.LeftOn, W.LeftLastSeen), printed, StringComparison.Ordinal);
        }
    }
}
