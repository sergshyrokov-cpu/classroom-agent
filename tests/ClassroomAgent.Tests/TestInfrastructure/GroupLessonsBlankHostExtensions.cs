using System.Globalization;
using System.Net;
using ClassroomAgent.Application.Models.Export;
using ClosedXML.Excel;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-043 over HTTP and PostgreSQL (TC-2): a synthetic course of any size (TC-4), the screen's values taken from
/// the US-028 program-layout export of the same request (spec FR-005: the form shows the same report, nothing computed
/// again), and the assertions that read a filled copy against those values and against the shipped form (AC-011:
/// sheets and cells, never bytes).
/// </summary>
public static class GroupLessonsBlankHostExtensions
{
    public const string CourseName = "Test Course Blank";

    public const string Section = "Test Section B";

    public const string TeacherSurname = "Викладачка";

    public const string TeacherGivenName = "Тестова";

    /// <summary>The scale of <see cref="InsertScaleTemplateAsync"/>: whole-number labels and one text label.</summary>
    public static readonly IReadOnlyList<(int From, int To, string Label)> Scale =
        [(0, 39, "2"), (40, 69, "добре"), (70, 100, "5")];

    public static string LessonTitle(int ordinal) => "Test Lesson " + ordinal.ToString("D2", CultureInfo.InvariantCulture);

    public static string MaterialTitle(int ordinal) => "Test Material " + ordinal.ToString("D2", CultureInfo.InvariantCulture);

    public static string StudentSurname(int ordinal) => "Студент" + ordinal.ToString("D2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Seeds one course: a named teacher; <paramref name="lessons"/> graded items (10 points) on days 1, 2, … of
    /// September in Kyiv; <paramref name="materials"/> materials on the same days; <paramref name="students"/> named
    /// students on the roster; returned grades on a fixed pattern (every third pair, one of them late). All names and
    /// titles are invented (TC-4). Returns the course id.
    /// </summary>
    public static async Task<long> SeedBlankCourseAsync(
        this InstallationTestHost host,
        int students,
        int lessons,
        CancellationToken cancellationToken,
        int materials = 0,
        bool withTeacher = true)
    {
        await host.WaitForPurgeRunsAsync(1, cancellationToken);
        var courseId = await CourseRows.InsertCourseAsync(host, cancellationToken, name: CourseName, section: Section);
        var seenAt = JournalTestData.Period.StartUtc.AddDays(-30);

        if (withTeacher)
        {
            var teacher = await CourseRows.InsertParticipantAsync(
                host, cancellationToken, googleUserId: CourseTestData.UserId(900), email: CourseTestData.Email("teacher.blank"),
                surname: TeacherSurname, givenName: TeacherGivenName);
            await CourseRows.InsertMembershipAsync(host, courseId, teacher, cancellationToken, role: CourseTestData.Roles.Teacher, firstSeenAt: seenAt);
        }

        var items = new List<long>();
        for (var i = 1; i <= lessons; i++)
        {
            var day = JournalTestData.Period.StartUtc.AddDays(i - 1).AddHours(12);
            items.Add(await CourseWorkRows.InsertCourseWorkAsync(
                host, courseId, cancellationToken, googleId: CourseWorkTestData.ItemId(100 + i), title: LessonTitle(i),
                itemDate: day, dueAt: day, maxPoints: 10m));
        }

        for (var i = 1; i <= materials; i++)
        {
            await CourseWorkRows.InsertCourseWorkAsync(
                host, courseId, cancellationToken, googleId: CourseWorkTestData.ItemId(300 + i), title: MaterialTitle(i),
                resource: CourseWorkTestData.ResourceCodes.CourseWorkMaterial,
                itemDate: JournalTestData.Period.StartUtc.AddDays(i - 1).AddHours(13));
        }

        var submission = 1000;
        for (var s = 1; s <= students; s++)
        {
            var student = await CourseRows.InsertParticipantAsync(
                host, cancellationToken, googleUserId: CourseTestData.UserId(100 + s),
                email: CourseTestData.Email("student." + s.ToString("D2", CultureInfo.InvariantCulture)),
                surname: StudentSurname(s), givenName: "Тест");
            await CourseRows.InsertMembershipAsync(host, courseId, student, cancellationToken, firstSeenAt: seenAt);

            for (var l = 1; l <= lessons; l++)
            {
                if ((s + l) % 3 != 0)
                {
                    continue;
                }

                await CourseWorkRows.InsertSubmissionAsync(
                    host, items[l - 1], student, cancellationToken, googleId: CourseWorkTestData.SubmissionId(++submission),
                    state: CourseWorkTestData.StateCodes.Returned, assignedGrade: (s + l) % 11, late: (s + l) % 9 == 0);
            }
        }

        return courseId;
    }

    /// <summary>A created template with the whole-number scale of <see cref="Scale"/>, so grades are numbers (FR-005.3).</summary>
    public static async Task<string> InsertScaleTemplateAsync(
        this InstallationTestHost host, CancellationToken cancellationToken, bool hideMaterials = false, int hours = 2)
    {
        var author = await host.AccountIdAsync(SignInTestData.AdminEmail, cancellationToken);
        var id = await host.InsertTemplateAsync(
            cancellationToken, "Test Template Blank", author, hideMaterials: hideMaterials, hours: hours, scale: Scale);
        return id.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>One cell of the screen's report as the US-028 file holds it.</summary>
    public sealed record ScreenCell(XLDataType Type, string Text, double? Number, bool QuotePrefix)
    {
        public bool IsBlank => Type == XLDataType.Blank || (Type == XLDataType.Text && Text.Length == 0);
    }

    public sealed record ScreenLesson(DateTime Date, string Title, double Hours);

    /// <summary>The report the screen shows, as the US-028 program layout exports it (spec FR-005).</summary>
    public sealed record Screen(
        string Course,
        string Teachers,
        IReadOnlyList<string> Students,
        IReadOnlyList<ScreenLesson> Lessons,
        IReadOnlyList<IReadOnlyList<ScreenCell>> Grades,
        IReadOnlyList<string> MaterialTitles);

    /// <summary>
    /// Exports the same request with the program layout and reads the report out of it (US-028 FR-004: header rows 1 … 4
    /// with the value in column B, the table from row 6; the grading heading's last line is the material marker).
    /// Lessons are the non-material columns, matched in order to the "Lesson topics" rows (spec FR-005.1).
    /// </summary>
    public static async Task<Screen> ScreenAsync(
        this InstallationTestHost host, FormClient client, long courseId, CancellationToken cancellationToken,
        string? template = ReportTemplateTestData.BuiltInKey, string culture = "uk", Dictionary<string, string?>? body = null)
    {
        var response = await client.ExportAsync(body ?? JournalExportHostExtensions.September(courseId, template), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var grading = workbook.Worksheet(1);
        var topics = workbook.Worksheet(2);
        var material = host.ReportText(ReportText.Material, culture);
        const int tableRow = 6;

        var lessons = new List<ScreenLesson>();
        for (var row = tableRow + 1; !topics.Cell(row, 1).IsEmpty(); row++)
        {
            if (topics.Cell(row, 1).DataType != XLDataType.DateTime)
            {
                break;
            }

            lessons.Add(new ScreenLesson(
                topics.Cell(row, 1).GetDateTime().Date, topics.Cell(row, 2).GetString(), topics.Cell(row, 3).GetDouble()));
        }

        var lessonColumns = new List<int>();
        var materials = new List<string>();
        var students = new List<string>();
        var grades = new List<IReadOnlyList<ScreenCell>>();
        var hasTable = grading.Cell(tableRow + 1, 1).GetString().Length > 0;
        if (hasTable)
        {
            var last = grading.Row(tableRow).LastCellUsed()?.Address.ColumnNumber ?? 1;
            for (var column = 2; column <= last; column++)
            {
                var heading = grading.Cell(tableRow, column).GetString().Split('\n');
                if (heading[^1] == material)
                {
                    materials.Add(heading[0]);
                }
                else
                {
                    lessonColumns.Add(column);
                }
            }

            for (var row = tableRow + 1; grading.Cell(row, 1).GetString().Length > 0; row++)
            {
                students.Add(grading.Cell(row, 1).GetString());
                grades.Add(lessonColumns.Select(c => Read(grading.Cell(row, c))).ToList());
            }
        }

        Assert.True(!hasTable || lessonColumns.Count == lessons.Count, "Every non-material grading column must be a lesson-topics row.");
        return new Screen(grading.Cell(2, 2).GetString(), grading.Cell(4, 2).GetString(), students, lessons, grades, materials);
    }

    private static ScreenCell Read(IXLCell cell) => new(
        cell.DataType,
        cell.DataType == XLDataType.Text ? cell.GetString() : cell.GetFormattedString(),
        cell.DataType == XLDataType.Number ? cell.GetDouble() : null,
        cell.Style.IncludeQuotePrefix);

    /// <summary>A file of one copy of each kind is the filled form, not the US-028 file: its copies carry the footer numbers.</summary>
    public static void AssertIsTheForm(XLWorkbook workbook)
    {
        Assert.Equal([GroupLessonsBlank.GradingSheet, GroupLessonsBlank.TopicsSheet], workbook.Worksheets.Select(w => w.Name));
        AssertFooter(workbook.Worksheet(1), new GroupLessonsBlank.PlannedSheet(true, GroupLessonsBlank.GradingSheet, 0, 0, 1));
        AssertFooter(workbook.Worksheet(2), new GroupLessonsBlank.PlannedSheet(false, GroupLessonsBlank.TopicsSheet, 0, 0, 1));
    }

    /// <summary>
    /// The footer sections a copy must carry (spec FR-006.1): the number on one side, the rest empty. Read as odd pages: a
    /// loaded file holds its footer per odd / even / first page, never under "all pages" (OD-013, T-1).
    /// </summary>
    public static void AssertFooter(IXLWorksheet sheet, GroupLessonsBlank.PlannedSheet planned)
    {
        var number = planned.Number.ToString(CultureInfo.InvariantCulture);
        var footer = sheet.PageSetup.Footer;
        var header = sheet.PageSetup.Header;
        Assert.Equal(planned.IsGrading ? number : string.Empty, footer.Right.GetText(XLHFOccurrence.OddPages));
        Assert.Equal(planned.IsGrading ? string.Empty : number, footer.Left.GetText(XLHFOccurrence.OddPages));
        Assert.Equal(string.Empty, footer.Center.GetText(XLHFOccurrence.OddPages));
        Assert.Equal(string.Empty, header.Left.GetText(XLHFOccurrence.OddPages));
        Assert.Equal(string.Empty, header.Center.GetText(XLHFOccurrence.OddPages));
        Assert.Equal(string.Empty, header.Right.GetText(XLHFOccurrence.OddPages));
    }

    /// <summary>
    /// Spec FR-005.2, FR-005.3, FR-005.5: one "Оцінювання" copy for group <c>g</c> and block <c>b</c> — course, student
    /// numbers continuing across groups, names, dates, grades equal to the screen; unused rows and columns cleared; the
    /// 24th lesson column always empty.
    /// </summary>
    public static void AssertGradingCopy(IXLWorksheet sheet, Screen screen, GroupLessonsBlank.PlannedSheet planned, int perCopyStudents, int perCopyLessons)
    {
        Assert.Equal(screen.Course, sheet.Cell(GroupLessonsBlank.CourseRow, GroupLessonsBlank.CourseColumn).GetString());
        Assert.False(sheet.Cell(GroupLessonsBlank.CourseRow, GroupLessonsBlank.CourseColumn).Style.Alignment.WrapText);

        for (var k = 0; k < GroupLessonsBlank.StudentRows; k++)
        {
            var row = GroupLessonsBlank.FirstStudentRow + k;
            var index = (planned.Group * perCopyStudents) + k;
            var holds = k < perCopyStudents && index < screen.Students.Count;
            var number = sheet.Cell(row, GroupLessonsBlank.StudentNumberColumn);
            var name = sheet.Cell(row, GroupLessonsBlank.StudentNameColumn);
            Assert.False(number.HasFormula, $"{sheet.Name} {number.Address}: no formula may remain.");
            if (holds)
            {
                Assert.Equal(XLDataType.Number, number.DataType);
                Assert.Equal(index + 1, number.GetDouble());
                Assert.Equal(screen.Students[index], name.GetString());
            }
            else
            {
                Assert.True(number.IsEmpty(), $"{sheet.Name} {number.Address} must be cleared.");
                Assert.True(name.IsEmpty(), $"{sheet.Name} {name.Address} must be cleared.");
            }
        }

        for (var j = 0; j < GroupLessonsBlank.LessonColumns; j++)
        {
            var column = GroupLessonsBlank.FirstLessonColumn + j;
            var lesson = (planned.Block * perCopyLessons) + j;
            var holdsLesson = j < perCopyLessons && j < GroupLessonsBlank.LessonsPerCopyMax && lesson < screen.Lessons.Count;
            var heading = sheet.Cell(GroupLessonsBlank.LessonDateRow, column);
            Assert.False(heading.HasFormula);
            if (holdsLesson)
            {
                Assert.Equal(XLDataType.DateTime, heading.DataType);
                Assert.Equal(screen.Lessons[lesson].Date, heading.GetDateTime().Date);
                Assert.Equal(GroupLessonsBlank.DateFormat, heading.Style.NumberFormat.Format);
            }
            else
            {
                Assert.True(heading.IsEmpty(), $"{sheet.Name} {heading.Address} must be empty.");
            }

            for (var k = 0; k < GroupLessonsBlank.StudentRows; k++)
            {
                var cell = sheet.Cell(GroupLessonsBlank.FirstStudentRow + k, column);
                var index = (planned.Group * perCopyStudents) + k;
                var holdsStudent = k < perCopyStudents && index < screen.Students.Count;
                Assert.False(cell.HasFormula);
                if (holdsLesson && holdsStudent)
                {
                    AssertSameCell(screen.Grades[index][lesson], cell);
                }
                else
                {
                    Assert.True(cell.IsEmpty(), $"{sheet.Name} {cell.Address} must be empty.");
                }
            }
        }

        AssertFooter(sheet, planned);
    }

    /// <summary>Spec FR-005.4: one "Теми занять" copy for block <c>b</c> — teachers, numbered topics, cleared rows.</summary>
    public static void AssertTopicsCopy(IXLWorksheet sheet, Screen screen, GroupLessonsBlank.PlannedSheet planned, int perCopyLessons)
    {
        Assert.Equal(screen.Teachers, sheet.Cell(GroupLessonsBlank.TeachersRow, GroupLessonsBlank.TeachersColumn).GetString());
        Assert.False(sheet.Cell(GroupLessonsBlank.TeachersRow, GroupLessonsBlank.TeachersColumn).Style.Alignment.WrapText);

        for (var r = 0; r < GroupLessonsBlank.TopicRows; r++)
        {
            var row = GroupLessonsBlank.FirstTopicRow + r;
            var lesson = (planned.Block * perCopyLessons) + r;
            var holds = r < perCopyLessons && lesson < screen.Lessons.Count;
            var number = sheet.Cell(row, GroupLessonsBlank.TopicNumberColumn);
            var date = sheet.Cell(row, GroupLessonsBlank.TopicDateColumn);
            var hours = sheet.Cell(row, GroupLessonsBlank.TopicHoursColumn);
            var title = sheet.Cell(row, GroupLessonsBlank.TopicTitleColumn);
            foreach (var cell in new[] { number, date, hours, title })
            {
                Assert.False(cell.HasFormula, $"{sheet.Name} {cell.Address}: no formula may remain.");
            }

            if (holds)
            {
                Assert.Equal(XLDataType.Number, number.DataType);
                Assert.Equal(lesson + 1, number.GetDouble());
                Assert.Equal(XLDataType.DateTime, date.DataType);
                Assert.Equal(screen.Lessons[lesson].Date, date.GetDateTime().Date);
                Assert.Equal(GroupLessonsBlank.DateFormat, date.Style.NumberFormat.Format);
                Assert.Equal(XLDataType.Number, hours.DataType);
                Assert.Equal(screen.Lessons[lesson].Hours, hours.GetDouble());
                Assert.Equal(screen.Lessons[lesson].Title, title.GetString());
            }
            else
            {
                Assert.True(number.IsEmpty() && date.IsEmpty() && hours.IsEmpty() && title.IsEmpty(), $"{sheet.Name} row {row} must be cleared.");
            }

            Assert.True(sheet.Cell(row, GroupLessonsBlank.TopicIndependentWorkColumn).IsEmpty());
            Assert.True(sheet.Cell(row, GroupLessonsBlank.TopicSignatureColumn).IsEmpty());
        }

        AssertFooter(sheet, planned);
    }

    /// <summary>US-028 FR-004.5 / FR-004.7 as the screen's file holds it: same type, same value, same quote prefix.</summary>
    public static void AssertSameCell(ScreenCell expected, IXLCell actual)
    {
        if (expected.IsBlank)
        {
            Assert.True(actual.IsEmpty(), $"{actual.Address} must be empty like the screen.");
            return;
        }

        Assert.Equal(expected.Type, actual.DataType);
        if (expected.Type == XLDataType.Number)
        {
            Assert.Equal(expected.Number, actual.GetDouble());
        }
        else
        {
            Assert.Equal(expected.Text, actual.GetString());
        }

        Assert.Equal(expected.QuotePrefix, actual.Style.IncludeQuotePrefix);
    }

    /// <summary>
    /// Spec FR-005.6, AC-001: everything outside the written areas equals the shipped form's sheet of the same kind —
    /// printed text, lines, widths, heights, page setup apart from the footer of FR-006.1; no formula, no marker left.
    /// </summary>
    public static void AssertUntouched(IXLWorksheet copy, IXLWorksheet form, bool isGrading)
    {
        var range = isGrading ? GroupLessonsBlank.GradingCompareRange : GroupLessonsBlank.TopicsCompareRange;
        foreach (var formCell in form.Range(range).Cells())
        {
            var cell = copy.Cell(formCell.Address.RowNumber, formCell.Address.ColumnNumber);
            Assert.False(cell.HasFormula, $"{copy.Name} {cell.Address}: no formula is written.");
            Assert.False(cell.GetString().StartsWith("{{", StringComparison.Ordinal), $"{copy.Name} {cell.Address}: a marker remains.");
            AssertSameBorder(formCell, cell, copy.Name);
            Assert.Equal(formCell.Style.Font.FontName, cell.Style.Font.FontName);
            Assert.Equal(formCell.Style.Font.FontSize, cell.Style.Font.FontSize);
            Assert.Equal(formCell.Style.Font.Bold, cell.Style.Font.Bold);
            Assert.Equal(formCell.Style.Alignment.TextRotation, cell.Style.Alignment.TextRotation);
            Assert.Equal(formCell.Style.Alignment.Horizontal, cell.Style.Alignment.Horizontal);
            Assert.Equal(formCell.Style.Alignment.Vertical, cell.Style.Alignment.Vertical);
            if (!IsWritten(formCell, isGrading))
            {
                Assert.Equal(formCell.GetString(), cell.GetString());
            }
        }

        var last = isGrading ? 28 : 8;
        for (var column = 1; column <= last; column++)
        {
            Assert.Equal(form.Column(column).Width, copy.Column(column).Width, 3);
        }

        for (var row = 1; row <= (isGrading ? 45 : 32); row++)
        {
            Assert.Equal(form.Row(row).Height, copy.Row(row).Height, 3);
        }

        Assert.Equal(XLPageOrientation.Portrait, copy.PageSetup.PageOrientation);
        Assert.Equal(form.PageSetup.PageOrientation, copy.PageSetup.PageOrientation);
        Assert.Equal(form.PageSetup.PaperSize, copy.PageSetup.PaperSize);
        Assert.Equal(form.PageSetup.Margins.Left, copy.PageSetup.Margins.Left, 4);
        Assert.Equal(form.PageSetup.Margins.Right, copy.PageSetup.Margins.Right, 4);
        Assert.Equal(form.PageSetup.Margins.Top, copy.PageSetup.Margins.Top, 4);
        Assert.Equal(form.PageSetup.Margins.Bottom, copy.PageSetup.Margins.Bottom, 4);
        Assert.Equal(form.PageSetup.Margins.Header, copy.PageSetup.Margins.Header, 4);
        Assert.Equal(form.PageSetup.Margins.Footer, copy.PageSetup.Margins.Footer, 4);
        Assert.Equal(form.PageSetup.Scale, copy.PageSetup.Scale);
        Assert.Equal(form.PageSetup.PagesWide, copy.PageSetup.PagesWide);
        Assert.Equal(form.PageSetup.PagesTall, copy.PageSetup.PagesTall);
        Assert.Empty(copy.MergedRanges);
    }

    /// <summary>The cells of the written areas of spec FR-002 (course / teachers, numbers, names, dates, grades, topics).</summary>
    private static bool IsWritten(IXLCell cell, bool isGrading)
    {
        var row = cell.Address.RowNumber;
        var column = cell.Address.ColumnNumber;
        if (isGrading)
        {
            var lastRow = GroupLessonsBlank.FirstStudentRow + GroupLessonsBlank.StudentRows - 1;
            var lastColumn = GroupLessonsBlank.FirstLessonColumn + GroupLessonsBlank.LessonColumns - 1;
            return (row == GroupLessonsBlank.CourseRow && column == GroupLessonsBlank.CourseColumn)
                || (row >= GroupLessonsBlank.FirstStudentRow && row <= lastRow && column <= lastColumn)
                || (row == GroupLessonsBlank.LessonDateRow && column >= GroupLessonsBlank.FirstLessonColumn && column <= lastColumn);
        }

        var lastTopicRow = GroupLessonsBlank.FirstTopicRow + GroupLessonsBlank.TopicRows - 1;
        return (row == GroupLessonsBlank.TeachersRow && column == GroupLessonsBlank.TeachersColumn)
            || (row >= GroupLessonsBlank.FirstTopicRow && row <= lastTopicRow && column <= GroupLessonsBlank.TopicTitleColumn);
    }

    public static void AssertSameBorder(IXLCell expected, IXLCell actual, string sheet)
    {
        var a = expected.Style.Border;
        var b = actual.Style.Border;
        Assert.True(
            a.TopBorder == b.TopBorder && a.BottomBorder == b.BottomBorder && a.LeftBorder == b.LeftBorder && a.RightBorder == b.RightBorder,
            $"{sheet} {actual.Address}: the form's lines must stay.");
    }
}
