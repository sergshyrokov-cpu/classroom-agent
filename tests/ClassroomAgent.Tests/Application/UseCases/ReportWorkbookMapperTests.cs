using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-028 <c>ReportWorkbookMapper</c> (spec FR-004, FR-007, VR-003; OD-003 a; entity model §2.1, §2.2): the two sheets,
/// the header block, the Grading and Lesson topics tables, which cell is a number, the empty states, the formula guard
/// and the Excel text limit. Program text is recognised by the <see cref="FakeReportTexts"/> markers; everything else
/// must arrive exactly as the report holds it.
/// </summary>
public sealed class ReportWorkbookMapperTests
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en");

    private static readonly DateOnly Lesson = new(2026, 9, 10);

    private const int HeaderRow = 5;

    private static string D(DateOnly date, CultureInfo? culture = null) => date.ToString("d", culture ?? Uk);

    private static PersonName Named(string name) => new(name, ReportNameKind.Profile);

    private static readonly PersonName Unnamed = new(null, ReportNameKind.Unnamed);

    private static ReportHeader Header(bool builtIn = true, string? templateName = null, string? section = "7-А", params PersonName[] teachers) =>
        new(builtIn, templateName, "Алгебра", section, JournalTestData.Period.From, JournalTestData.Period.To,
            teachers.Length == 0 ? [Named("Тестова Ірина")] : teachers);

    private static ReportCell Cell(
        ReportCellContent content = ReportCellContent.Empty,
        ReportGrade? grade = null,
        ReportCellMark? mark = null,
        string? rawState = null,
        ReportCellMark? late = null,
        ReportGrade? draft = null,
        DateOnly? turnedInOn = null) =>
        new(content, grade, mark, rawState, late, draft, turnedInOn);

    private static ReportGrade Label(string label) => new(ReportGradeKind.ScaleLabel, label, null, null);

    private static ReportGrade Points(decimal points, decimal max) => new(ReportGradeKind.RawPoints, null, points, max);

    private static ReportCell GradeCell(ReportGrade grade, ReportCellMark? late = null, ReportGrade? draft = null, DateOnly? turnedInOn = null) =>
        Cell(ReportCellContent.Grade, grade: grade, late: late, draft: draft, turnedInOn: turnedInOn);

    private static Report Report(
        ReportHeader? header = null,
        IReadOnlyList<GradingColumn>? columns = null,
        IReadOnlyList<GradingRow>? rows = null,
        IReadOnlyList<LessonTopicRow>? topics = null) =>
        new(
            header ?? Header(),
            ReportView.Short,
            null,
            new GradingPart(columns ?? [new GradingColumn(Lesson, "Тема 1", false)], rows ?? [new GradingRow(Named("Тестова Олена"), [GradeCell(Label("12"))])]),
            new LessonTopicsPart(topics ?? [new LessonTopicRow(Lesson, "Тема 1", 2)]),
            ReportNameSource.Profile,
            NameSourceOrigin.Template);

    private static Workbook Map(Report report, CultureInfo? culture = null, PageOrientation orientation = PageOrientation.Portrait) =>
        ReportWorkbookMapper.Map(report, new FakeReportTexts(), culture ?? Uk, orientation);

    private static Worksheet Grading(Workbook workbook) => workbook.Sheets[0];

    private static Worksheet Topics(Workbook workbook) => workbook.Sheets[1];

    private static WorkbookCell At(Worksheet sheet, int row, int column) => sheet.Rows[row].Cells[column];

    private static WorkbookCell OnlyGradeCell(ReportCell cell) =>
        At(Grading(Map(Report(rows: [new GradingRow(Named("Тестова Олена"), [cell])]))), HeaderRow + 1, 1);

    private static void AssertText(WorkbookCell cell, string text)
    {
        Assert.Equal(WorkbookCellKind.Text, cell.Kind);
        Assert.Equal(text, cell.Text);
    }

    /// <summary>FR-004.1, FR-007: "Grading" then "Lesson topics", both named by the translation files.</summary>
    [Fact]
    public void TheWorkbook_HasGradingThenLessonTopics_WithTranslatedNames()
    {
        var workbook = Map(Report());

        Assert.Equal(
            [FakeReportTexts.Of(ReportText.SheetGrading), FakeReportTexts.Of(ReportText.SheetLessonTopics)],
            workbook.Sheets.Select(s => s.Name));
    }

    /// <summary>FR-004.2, FR-007: four labelled header rows and an empty row on both sheets; the built-in's name is translated.</summary>
    [Fact]
    public void BothSheets_BeginWithTheLabelledHeaderBlock_AndAnEmptyRow()
    {
        var workbook = Map(Report(header: Header(teachers: [Named("Тестова Ірина"), Unnamed])));

        foreach (var sheet in workbook.Sheets)
        {
            AssertText(At(sheet, 0, 0), FakeReportTexts.Of(ReportText.HeaderTemplate));
            AssertText(At(sheet, 0, 1), FakeReportTexts.Of(ReportText.BuiltInTemplateName));
            AssertText(At(sheet, 1, 0), FakeReportTexts.Of(ReportText.HeaderCourse));
            AssertText(At(sheet, 1, 1), "Алгебра — 7-А");
            AssertText(At(sheet, 2, 0), FakeReportTexts.Of(ReportText.HeaderPeriod));
            AssertText(At(sheet, 2, 1), D(JournalTestData.Period.From) + " – " + D(JournalTestData.Period.To));
            AssertText(At(sheet, 3, 0), FakeReportTexts.Of(ReportText.HeaderTeachers));
            AssertText(At(sheet, 3, 1), "Тестова Ірина, " + FakeReportTexts.Of(ReportText.TeacherUnnamed));
            Assert.True(At(sheet, 0, 0).IsHeading);
            Assert.All(sheet.Rows[4].Cells, c => Assert.Equal(WorkbookCellKind.Empty, c.Kind));
        }
    }

    /// <summary>FR-004.2, FR-007: a created template's name and a course without a section are written as they are.</summary>
    [Fact]
    public void ACreatedTemplateName_AndACourseWithoutSection_AreWrittenAsTheyAre()
    {
        var workbook = Map(Report(header: Header(builtIn: false, templateName: "Журнал 7-х класів", section: null)));

        AssertText(At(Grading(workbook), 0, 1), "Журнал 7-х класів");
        AssertText(At(Grading(workbook), 1, 1), "Алгебра");
    }

    /// <summary>FR-004.3: the student heading, then one heading per column — title, date and the material marker, as on screen.</summary>
    [Fact]
    public void TheGradingHeaderRow_HoldsTitleDateAndMaterialMarker_PerColumn()
    {
        var workbook = Map(Report(
            columns: [new GradingColumn(Lesson, "Тема 1", false), new GradingColumn(Lesson.AddDays(1), "Конспект", true)],
            rows: [new GradingRow(Named("Тестова Олена"), [Cell(), Cell()])]));

        var header = Grading(workbook).Rows[HeaderRow].Cells;
        AssertText(header[0], FakeReportTexts.Of(ReportText.StudentHeading));
        AssertText(header[1], "Тема 1\n" + D(Lesson));
        AssertText(header[2], "Конспект\n" + D(Lesson.AddDays(1)) + "\n" + FakeReportTexts.Of(ReportText.Material));
        Assert.All(header, c => Assert.True(c.IsHeading));
        Assert.Equal(HeaderRow, Grading(workbook).TableHeaderRowIndex);
        Assert.True(Grading(workbook).RepeatAndFreezeFirstColumn);
    }

    /// <summary>FR-004.3, US-042 FR-003: one row per student in the report's order; an unnamed student gets the translated label.</summary>
    [Fact]
    public void StudentRows_FollowTheReportOrder_AndAnUnnamedStudentIsLabelled()
    {
        var workbook = Map(Report(rows:
        [
            new GradingRow(Named("Тестова Олена"), [Cell()]),
            new GradingRow(Named("petro.t"), [Cell()]),
            new GradingRow(Unnamed, [Cell()]),
        ]));

        var names = Grading(workbook).Rows.Skip(HeaderRow + 1).Select(r => r.Cells[0].Text);
        Assert.Equal(["Тестова Олена", "petro.t", FakeReportTexts.Of(ReportText.StudentUnnamed)], names);
    }

    /// <summary>OD-003 a, AC-003: a whole-number scale label alone is a number Excel can calculate with.</summary>
    [Theory]
    [InlineData("12", 12)]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    public void AWholeNumberGradeAlone_IsANumber(string label, int expected)
    {
        var cell = OnlyGradeCell(GradeCell(Label(label)));

        Assert.Equal(WorkbookCellKind.Number, cell.Kind);
        Assert.Equal(expected, cell.Number);
        Assert.Null(cell.Text);
    }

    /// <summary>OD-003 a, AC-003: any other label stays text exactly as on screen — a leading zero too, so the cell shows "012".</summary>
    [Theory]
    [InlineData("012")]
    [InlineData("12.5")]
    [InlineData("-3")]
    [InlineData("зар.")]
    [InlineData("A")]
    [InlineData(" 12")]
    public void AnyOtherLabel_IsText_AsWritten(string label) =>
        AssertText(OnlyGradeCell(GradeCell(Label(label))), label);

    /// <summary>OD-003 a, US-027 FR-015: raw points are "points / max" in the UI culture, as text.</summary>
    [Fact]
    public void RawPoints_AreText_PointsOverMaximum()
    {
        var expected = 8.5m.ToString("0.############################", Uk) + " / 10";

        AssertText(OnlyGradeCell(GradeCell(Points(8.5m, 10m))), expected);
    }

    /// <summary>OD-003 a, FR-004.5: a grade with a late mark is text — the grade and the translated program mark on two lines.</summary>
    [Fact]
    public void AGradeWithALateMark_IsText_OnTwoLines()
    {
        var late = new ReportCellMark(ReportCellMarkKind.Program, "Late", null);

        AssertText(OnlyGradeCell(GradeCell(Label("12"), late: late)), "12\n" + FakeReportTexts.Mark("Late"));
    }

    /// <summary>AC-002, FR-007: a school's own late text is written as the school wrote it.</summary>
    [Fact]
    public void AnOwnLateMark_IsWrittenAsTheSchoolWroteIt()
    {
        var late = new ReportCellMark(ReportCellMarkKind.Own, null, "запізн.");

        AssertText(OnlyGradeCell(GradeCell(Label("12"), late: late)), "12\nзапізн.");
    }

    /// <summary>FR-004.5, US-027 FR-005.4: in the full view the draft grade and the turn-in date follow, in the screen's order.</summary>
    [Fact]
    public void TheFullView_AddsDraftAndTurnInDate_InTheScreenOrder()
    {
        var late = new ReportCellMark(ReportCellMarkKind.Program, "Late", null);
        var turnedIn = new DateOnly(2026, 9, 12);

        var cell = OnlyGradeCell(GradeCell(Label("10"), late: late, draft: Label("9"), turnedInOn: turnedIn));

        AssertText(
            cell,
            "10\n" + FakeReportTexts.Of(ReportText.Draft) + " 9\n" + FakeReportTexts.Mark("Late") + "\n"
            + FakeReportTexts.Of(ReportText.TurnedInOn) + " " + D(turnedIn));
    }

    /// <summary>AC-002, AC-003: a program mark is translated, an own mark and a raw state are written as they are.</summary>
    [Fact]
    public void Marks_AndRawStates_AreText()
    {
        AssertText(
            OnlyGradeCell(Cell(ReportCellContent.Mark, mark: new ReportCellMark(ReportCellMarkKind.Program, nameof(ReportCellState.NotAssigned), null))),
            FakeReportTexts.Mark(nameof(ReportCellState.NotAssigned)));
        AssertText(OnlyGradeCell(Cell(ReportCellContent.Mark, mark: new ReportCellMark(ReportCellMarkKind.Own, null, "н/з"))), "н/з");
        AssertText(OnlyGradeCell(Cell(ReportCellContent.RawState, rawState: "RECLAIMED_BY_STUDENT")), "RECLAIMED_BY_STUDENT");
    }

    /// <summary>FR-004.5: an empty report cell is an empty Excel cell.</summary>
    [Fact]
    public void AnEmptyReportCell_IsAnEmptyCell() =>
        Assert.Equal(WorkbookCellKind.Empty, OnlyGradeCell(Cell()).Kind);

    /// <summary>FR-004.4, I-4: Lesson topics — six translated headings; a real date, the title, hours as a number, teachers, two empty columns.</summary>
    [Fact]
    public void LessonTopics_HoldADateTheTitleTheHoursTheTeachers_AndTwoEmptyColumns()
    {
        var workbook = Map(Report(
            header: Header(teachers: [Named("Тестова Ірина"), Named("Тестовий Іван")]),
            topics: [new LessonTopicRow(Lesson, "Тема 1", 2)]));

        var sheet = Topics(workbook);
        Assert.Equal(
            new[] { ReportText.TopicDate, ReportText.TopicTitle, ReportText.TopicHours, ReportText.TopicTeacher, ReportText.TopicIndependentWork, ReportText.TopicSignature }
                .Select(FakeReportTexts.Of),
            sheet.Rows[HeaderRow].Cells.Select(c => c.Text));
        var row = sheet.Rows[HeaderRow + 1].Cells;
        Assert.Equal(WorkbookCellKind.Date, row[0].Kind);
        Assert.Equal(Lesson, row[0].Date);
        AssertText(row[1], "Тема 1");
        Assert.Equal(WorkbookCellKind.Number, row[2].Kind);
        Assert.Equal(2m, row[2].Number);
        AssertText(row[3], "Тестова Ірина, Тестовий Іван");
        Assert.Equal(WorkbookCellKind.Empty, row[4].Kind);
        Assert.Equal(WorkbookCellKind.Empty, row[5].Kind);
        Assert.Equal(HeaderRow, sheet.TableHeaderRowIndex);
        Assert.False(sheet.RepeatAndFreezeFirstColumn);
    }

    /// <summary>FR-004.6: nothing published — both sheets carry the header block and the translated message, no table.</summary>
    [Fact]
    public void NothingPublished_PutsTheMessageOnBothSheets_InPlaceOfTheTables()
    {
        var report = new Report(Header(), ReportView.Short, ReportEmptyStateKey.NothingPublished, null, null, ReportNameSource.Profile, NameSourceOrigin.Template);

        var workbook = Map(report);

        Assert.Equal(2, workbook.Sheets.Count);
        foreach (var sheet in workbook.Sheets)
        {
            Assert.Equal(HeaderRow + 1, sheet.Rows.Count);
            AssertText(Assert.Single(sheet.Rows[HeaderRow].Cells), FakeReportTexts.Of(ReportText.NothingPublished));
            Assert.Null(sheet.TableHeaderRowIndex);
            Assert.False(sheet.RepeatAndFreezeFirstColumn);
        }
    }

    /// <summary>FR-004.6: items but no student — Grading says so, Lesson topics is filled.</summary>
    [Fact]
    public void NoStudents_PutsTheMessageOnGrading_AndFillsLessonTopics()
    {
        var workbook = Map(Report(rows: []));

        Assert.Equal(HeaderRow + 1, Grading(workbook).Rows.Count);
        AssertText(Assert.Single(Grading(workbook).Rows[HeaderRow].Cells), FakeReportTexts.Of(ReportText.NoStudents));
        Assert.Null(Grading(workbook).TableHeaderRowIndex);
        Assert.Equal(HeaderRow + 2, Topics(workbook).Rows.Count);
    }

    /// <summary>FR-004.7, S-08, I-5: text that Excel could take for a formula gets the quote prefix; its value is unchanged.</summary>
    [Theory]
    [InlineData("=SUM(A1:A9)")]
    [InlineData("+380")]
    [InlineData("-Олена")]
    [InlineData("@user")]
    [InlineData("\tТема")]
    [InlineData("\rТема")]
    public void FormulaLikeText_GetsTheQuotePrefix_AndKeepsItsValue(string text)
    {
        var workbook = Map(Report(
            rows: [new GradingRow(Named(text), [Cell()])],
            topics: [new LessonTopicRow(Lesson, text, 2)]));

        var name = At(Grading(workbook), HeaderRow + 1, 0);
        var title = At(Topics(workbook), HeaderRow + 1, 1);
        AssertText(name, text);
        Assert.True(name.QuotePrefix);
        AssertText(title, text);
        Assert.True(title.QuotePrefix);
    }

    /// <summary>FR-004.7: ordinary text carries no quote prefix.</summary>
    [Fact]
    public void OrdinaryText_HasNoQuotePrefix() =>
        Assert.False(At(Topics(Map(Report())), HeaderRow + 1, 1).QuotePrefix);

    /// <summary>FR-004.9, I-7: a text longer than Excel's cell limit is cut to 32 767 characters.</summary>
    [Fact]
    public void ALongText_IsCutToTheExcelLimit()
    {
        var title = new string('т', 40_000);

        var cell = At(Topics(Map(Report(topics: [new LessonTopicRow(Lesson, title, 2)]))), HeaderRow + 1, 1);

        Assert.Equal(32_767, cell.Text!.Length);
        Assert.Equal(title[..32_767], cell.Text);
    }

    /// <summary>OD-004 a: the orientation chosen at export is carried as is.</summary>
    [Theory]
    [InlineData(PageOrientation.Portrait)]
    [InlineData(PageOrientation.Landscape)]
    public void TheOrientation_IsCarried(PageOrientation orientation) =>
        Assert.Equal(orientation, Map(Report(), orientation: orientation).Orientation);

    /// <summary>I-4, entity model §2.2: the date format is the UI culture's short date with Excel's month letter.</summary>
    [Theory]
    [InlineData("uk-UA")]
    [InlineData("en")]
    public void TheDateFormat_IsTheCulturesShortDate_InExcelLetters(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);

        var workbook = Map(Report(), culture);

        Assert.Equal(culture.DateTimeFormat.ShortDatePattern.Replace('M', 'm'), workbook.DateFormat);
    }

    /// <summary>FR-007, NFR-073: dates inside text follow the UI language of the export.</summary>
    [Fact]
    public void DatesInText_FollowTheUiLanguage()
    {
        var workbook = Map(Report(), En);

        AssertText(At(Grading(workbook), HeaderRow, 1), "Тема 1\n" + D(Lesson, En));
    }
}
