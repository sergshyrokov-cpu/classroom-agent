using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-060 over HTTP and PostgreSQL (TC-2): the marks course of <see cref="DataMarksWorld"/> and the four course
/// list courses seeded by SQL — the stored marks of US-058 / US-059 written straight into their columns, so the tests do
/// not depend on synchronization (TC-4: nothing reaches Google) — and the readers that find one cell of a page or a
/// file. Every name, title and id is invented.
/// </summary>
public static partial class DataMarksHostExtensions
{
    public const string ListAbsentName = "Test Marks List Absent";

    public const string ListHiddenName = "Test Marks List Hidden";

    public const string ListPlainName = "Test Marks List Plain";

    public const string ListTeacherlessName = "Test Marks List Teacherless";

    /// <summary>The present teacher's label as the course list shows it: surname, then given name (US-020 FR-007).</summary>
    public const string PresentTeacherName = "Present Test Teacher";

    /// <summary>The former teacher's label under the same rule.</summary>
    public const string FormerTeacherName = "Former Test Teacher";

    private const int GradingHeadingRow = 6;

    private static readonly DateTimeOffset SeenAt = JournalTestData.Period.StartUtc.AddDays(-30);

    /// <summary>A started host with a Dean signed in, the page language set to <paramref name="language"/>.</summary>
    public static async Task<(InstallationTestHost Host, FormClient Client)> StartAsync(
        PostgreSqlFixture database,
        string language,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, cancellationToken, cause);
        if (language != "uk")
        {
            var chosen = await client.ChooseLanguageAsync("/", language, cancellationToken);
            Assert.Equal(HttpStatusCode.Redirect, chosen.Status);
        }

        return (host, client);
    }

    /// <summary>
    /// The marks course: a teacher on the roster; the graded item (max 12) and the deleted item (max 10, marked "not in
    /// Classroom"); the five students of <see cref="DataMarksWorld"/>; a full-view scale template by the Dean. Then the
    /// stored course state of <paramref name="variant"/>.
    /// </summary>
    public static async Task<DataMarksWorld> SeedMarksCourseAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken,
        CourseHeaderVariant variant = CourseHeaderVariant.None)
    {
        // The retention purge runs at every host start (US-037); seeding after its first run keeps it from racing the inserts.
        await host.WaitForPurgeRunsAsync(1, cancellationToken);
        var googleId = CourseTestData.CourseId(60);
        var courseId = await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: googleId, name: DataMarksWorld.CourseName, createdAt: SeenAt);
        var teacher = await CourseRows.InsertParticipantAsync(
            host, cancellationToken, googleUserId: CourseTestData.UserId(600), email: CourseTestData.Email("teacher.marks"),
            fullName: "Test Teacher Marks", surname: "Marksteacher", givenName: "Test");
        await CourseRows.InsertMembershipAsync(
            host, courseId, teacher, cancellationToken, role: CourseTestData.Roles.Teacher, firstSeenAt: SeenAt);

        var graded = await CourseWorkRows.InsertCourseWorkAsync(
            host, courseId, cancellationToken, googleId: CourseWorkTestData.ItemId(601), title: DataMarksWorld.GradedTitle,
            itemDate: DataMarksWorld.GradedDate, dueAt: DataMarksWorld.GradedDate, maxPoints: DataMarksWorld.GradedMaximum);
        var deleted = await CourseWorkRows.InsertCourseWorkAsync(
            host, courseId, cancellationToken, googleId: CourseWorkTestData.ItemId(602), title: DataMarksWorld.DeletedTitle,
            itemDate: DataMarksWorld.DeletedDate, dueAt: DataMarksWorld.DeletedDate, maxPoints: 10m);
        await host.ExecuteAsync(
            "UPDATE course_work SET not_in_classroom_since = @since WHERE id = @id",
            cancellationToken,
            ("since", DataMarksWorld.DeletedSince),
            ("id", deleted));

        var over = await StudentAsync(host, courseId, 601, DataMarksWorld.Over, true, SeenAt, null, cancellationToken);
        var draft = await StudentAsync(host, courseId, 602, DataMarksWorld.Draft, true, SeenAt, null, cancellationToken);
        var equal = await StudentAsync(host, courseId, 603, DataMarksWorld.Equal, true, SeenAt, null, cancellationToken);
        var gone = await StudentAsync(
            host, courseId, 604, DataMarksWorld.Gone, false, SeenAt, DataMarksWorld.LeftLastSeen, cancellationToken);
        var submitter = await StudentAsync(
            host, courseId, 605, DataMarksWorld.Submitter, false, DataMarksWorld.SubmitterSeen, DataMarksWorld.SubmitterSeen,
            cancellationToken);

        var returned = CourseWorkTestData.StateCodes.Returned;
        var turnedIn = CourseWorkTestData.StateCodes.TurnedIn;
        await CourseWorkRows.InsertSubmissionAsync(
            host, graded, over, cancellationToken, googleId: CourseWorkTestData.SubmissionId(601), state: returned,
            assignedGrade: DataMarksWorld.OverGrade);
        await CourseWorkRows.InsertSubmissionAsync(
            host, graded, draft, cancellationToken, googleId: CourseWorkTestData.SubmissionId(602), state: turnedIn,
            draftGrade: DataMarksWorld.DraftGrade);
        await CourseWorkRows.InsertSubmissionAsync(
            host, graded, equal, cancellationToken, googleId: CourseWorkTestData.SubmissionId(603), state: returned,
            assignedGrade: DataMarksWorld.GradedMaximum);
        await CourseWorkRows.InsertSubmissionAsync(
            host, deleted, equal, cancellationToken, googleId: CourseWorkTestData.SubmissionId(604), state: returned,
            assignedGrade: 7m);
        await CourseWorkRows.InsertSubmissionAsync(
            host, deleted, gone, cancellationToken, googleId: CourseWorkTestData.SubmissionId(605), state: returned,
            assignedGrade: 8m);
        await CourseWorkRows.InsertSubmissionAsync(
            host, graded, submitter, cancellationToken, googleId: CourseWorkTestData.SubmissionId(606), state: turnedIn,
            turnedInAt: DataMarksWorld.SubmitterSeen);

        await host.ApplyVariantAsync(courseId, googleId, variant, cancellationToken);

        var author = await host.AccountIdAsync(DeanAccountTestData.DeanEmail, cancellationToken);
        var template = await host.InsertTemplateAsync(
            cancellationToken, "Test Template Marks", author, view: "full", scale: DataMarksWorld.Scale);
        return new DataMarksWorld(courseId, googleId, template.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Writes the stored course state of a variant (spec FR-003.1) into the columns US-058 / US-059 own.</summary>
    public static async Task ApplyVariantAsync(
        this InstallationTestHost host,
        long courseId,
        string courseGoogleId,
        CourseHeaderVariant variant,
        CancellationToken cancellationToken)
    {
        var lastRead = variant switch
        {
            CourseHeaderVariant.NotVisible => (DateTimeOffset?)DataMarksWorld.R1,
            CourseHeaderVariant.SkippedNeverRead => null,
            _ => DataMarksWorld.R2,
        };
        await host.ExecuteAsync(
            "UPDATE course SET last_complete_read_at = @read WHERE id = @id",
            cancellationToken,
            ("read", lastRead),
            ("id", courseId));

        if (variant is CourseHeaderVariant.NotInClassroom or CourseHeaderVariant.NotInClassroomAndSkipped)
        {
            await host.MarkCourseAsync(courseId, "not_in_classroom", DataMarksWorld.D1, cancellationToken);
        }

        if (variant == CourseHeaderVariant.NotVisible)
        {
            await host.MarkCourseAsync(courseId, "not_visible_to_technical_account", DataMarksWorld.D2, cancellationToken);
        }

        if (variant is CourseHeaderVariant.Skipped or CourseHeaderVariant.SkippedNeverRead or CourseHeaderVariant.NotInClassroomAndSkipped)
        {
            await host.ExecuteAsync(
                """
                INSERT INTO sync_skipped_course (course_google_id, course_name, reason, skipped_since, created_at, updated_at)
                VALUES (@googleId, @name, 'value_out_of_range', @since, @since, @since)
                """,
                cancellationToken,
                ("googleId", courseGoogleId),
                ("name", DataMarksWorld.CourseName),
                ("since", DataMarksWorld.R2.AddDays(1)));
        }

        if (variant == CourseHeaderVariant.ArchivedUnchanged)
        {
            await host.ExecuteAsync(
                "UPDATE course SET course_state = @state WHERE id = @id",
                cancellationToken,
                ("state", CourseTestData.States.Archived),
                ("id", courseId));
        }
    }

    /// <summary>Stores a course mark of BR-048 / BR-049 (db-design of US-059: kind and instant together).</summary>
    public static Task<int> MarkCourseAsync(
        this InstallationTestHost host, long courseId, string mark, DateTimeOffset since, CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            "UPDATE course SET absence_mark = @mark, absent_since = @since WHERE id = @id",
            cancellationToken,
            ("mark", mark),
            ("since", since),
            ("id", courseId));

    /// <summary>
    /// The four course list courses of AC-001 / AC-002, all active: Absent (marked "not in Classroom" since D1) and Hidden
    /// (marked "not visible" since D2), each with the present teacher; Plain (no mark, the present teacher); Teacherless
    /// (no mark, its only teacher off the roster). Returns their ids in that order.
    /// </summary>
    public static async Task<(long Absent, long Hidden, long Plain, long Teacherless)> SeedListCoursesAsync(
        this InstallationTestHost host, CancellationToken cancellationToken)
    {
        await host.WaitForPurgeRunsAsync(1, cancellationToken);
        var present = await CourseRows.InsertParticipantAsync(
            host, cancellationToken, googleUserId: CourseTestData.UserId(610), email: CourseTestData.Email("teacher.present"),
            fullName: PresentTeacherName, surname: "Present", givenName: "Test Teacher");
        var former = await CourseRows.InsertParticipantAsync(
            host, cancellationToken, googleUserId: CourseTestData.UserId(611), email: CourseTestData.Email("teacher.former"),
            fullName: FormerTeacherName, surname: "Former", givenName: "Test Teacher");

        var absent = await ListCourseAsync(host, 61, ListAbsentName, present, true, cancellationToken);
        var hidden = await ListCourseAsync(host, 62, ListHiddenName, present, true, cancellationToken);
        var plain = await ListCourseAsync(host, 63, ListPlainName, present, true, cancellationToken);
        var teacherless = await ListCourseAsync(host, 64, ListTeacherlessName, former, false, cancellationToken);
        await host.MarkCourseAsync(absent, "not_in_classroom", DataMarksWorld.D1, cancellationToken);
        await host.MarkCourseAsync(hidden, "not_visible_to_technical_account", DataMarksWorld.D2, cancellationToken);
        return (absent, hidden, plain, teacherless);
    }

    // ---- HTML ---------------------------------------------------------------------------------------------------

    /// <summary>The inner HTML of the table with that id; fails when the page has none.</summary>
    public static string Table(string html, string tableId)
    {
        var match = Regex.Match(
            html,
            @"<table[^>]*\bid=""" + Regex.Escape(tableId) + @"""[^>]*>(?<body>.*?)</table>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"The page has no table #{tableId}.");
        return match.Groups["body"].Value;
    }

    /// <summary>The inner HTML of the element with that id (any tag), or null when the page has none.</summary>
    public static string? Element(string html, string id)
    {
        var match = Regex.Match(
            html,
            @"<(?<tag>[a-z0-9]+)[^>]*\bid=""" + Regex.Escape(id) + @"""[^>]*>(?<body>.*?)</\k<tag>>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["body"].Value : null;
    }

    /// <summary>The column headings of a table, in order (the student heading first).</summary>
    public static IReadOnlyList<string> ColumnHeadings(string tableHtml) =>
        ColumnHeading().Matches(tableHtml).Select(m => m.Groups["body"].Value).ToList();

    /// <summary>The heading of the one column whose title is <paramref name="title"/>.</summary>
    public static string ColumnHeadingOf(string tableHtml, string title) =>
        Assert.Single(ColumnHeadings(tableHtml), h => Text(h).Contains(title, StringComparison.Ordinal));

    /// <summary>The body rows of a table (rows inside <c>tbody</c>), inner HTML.</summary>
    public static IReadOnlyList<string> BodyRows(string tableHtml)
    {
        var body = Regex.Match(tableHtml, @"<tbody[^>]*>(?<body>.*?)</tbody>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.True(body.Success, "The table has no body.");
        return RowElement().Matches(body.Groups["body"].Value).Select(m => m.Groups["body"].Value).ToList();
    }

    /// <summary>The one body row whose row heading names <paramref name="token"/>.</summary>
    public static string RowOf(string tableHtml, string token) =>
        Assert.Single(BodyRows(tableHtml), r => RowHeading(r).Contains(token, StringComparison.Ordinal));

    /// <summary>The text of a row's heading cell (the student's name and whatever stands next to it).</summary>
    public static string RowHeading(string rowHtml)
    {
        var match = Regex.Match(rowHtml, @"<th[^>]*>(?<body>.*?)</th>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? Text(match.Groups["body"].Value) : string.Empty;
    }

    /// <summary>The data cells of a row: the attributes of the <c>td</c> tag and its inner HTML.</summary>
    public static IReadOnlyList<(string Attributes, string Body)> DataCells(string rowHtml) =>
        DataCell().Matches(rowHtml).Select(m => (m.Groups["attrs"].Value, m.Groups["body"].Value)).ToList();

    /// <summary>The data cell of the student <paramref name="token"/> under the column titled <paramref name="title"/>.</summary>
    public static (string Attributes, string Body) CellOf(string tableHtml, string token, string title)
    {
        var headings = ColumnHeadings(tableHtml);
        var index = headings.ToList().FindIndex(h => Text(h).Contains(title, StringComparison.Ordinal));
        Assert.True(index > 0, $"No column titled {title}.");
        var cells = DataCells(RowOf(tableHtml, token));
        return cells[index - 1];
    }

    /// <summary>The <c>class</c> attribute of a tag's attributes, or null when it has none.</summary>
    public static string? ClassOf(string attributes)
    {
        var match = Regex.Match(attributes, @"\bclass\s*=\s*""(?<v>[^""]*)""", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["v"].Value.Trim() : null;
    }

    /// <summary>A fragment's text: tags replaced by spaces, entities decoded, white space collapsed.</summary>
    public static string Text(string fragment) =>
        Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(fragment, " ")), " ").Trim();

    /// <summary>
    /// "left" with no date: the text holds the undated mark and no dated one — the undated text is not followed by a
    /// digit (the dated mark is the same text, a space and the date).
    /// </summary>
    public static bool HasUndatedLeft(string text, DataMarksTexts texts) =>
        Regex.IsMatch(text, Regex.Escape(texts.Left) + @"(?!\s*\d)");

    // ---- Workbooks ----------------------------------------------------------------------------------------------

    /// <summary>The program layout's heading cell (row 6) of the column whose title is <paramref name="title"/>.</summary>
    public static IXLCell ProgramHeading(IXLWorksheet sheet, string title) =>
        Assert.Single(
            sheet.Row(GradingHeadingRow).CellsUsed(),
            c => c.Address.ColumnNumber > 1 && c.GetString().StartsWith(title, StringComparison.Ordinal));

    /// <summary>The program layout's name cell (column A, below the heading) of the student <paramref name="token"/>.</summary>
    public static IXLCell ProgramName(IXLWorksheet sheet, string token) =>
        Assert.Single(
            sheet.Column(1).CellsUsed(),
            c => c.Address.RowNumber > GradingHeadingRow && c.GetString().Contains(token, StringComparison.Ordinal));

    /// <summary>The program layout's grading cell of the student under the column titled <paramref name="title"/>.</summary>
    public static IXLCell ProgramCell(IXLWorksheet sheet, string token, string title) =>
        sheet.Cell(ProgramName(sheet, token).Address.RowNumber, ProgramHeading(sheet, title).Address.ColumnNumber);

    /// <summary>The «Теми занять» sheet's topic cell (column B, below the heading) whose text begins with the title.</summary>
    public static IXLCell ProgramTopic(IXLWorksheet sheet, string title) =>
        Assert.Single(
            sheet.Column(2).CellsUsed(),
            c => c.Address.RowNumber > GradingHeadingRow && c.GetString().StartsWith(title, StringComparison.Ordinal));

    /// <summary>The blank form's name cell (column B from row 9) of the student <paramref name="token"/>.</summary>
    public static IXLCell BlankName(IXLWorksheet sheet, string token) =>
        Assert.Single(
            sheet.Column(GroupLessonsBlank.StudentNameColumn).CellsUsed(),
            c => c.Address.RowNumber >= GroupLessonsBlank.FirstStudentRow && c.GetString().Contains(token, StringComparison.Ordinal));

    /// <summary>The blank form's grade cell of the student on the lesson dated <paramref name="lessonDate"/> (row 8).</summary>
    public static IXLCell BlankGrade(IXLWorksheet sheet, string token, DateTimeOffset lessonDate)
    {
        var day = TimeZoneInfo.ConvertTime(lessonDate, JournalTestData.Kyiv).Date;
        var heading = Assert.Single(
            sheet.Row(GroupLessonsBlank.LessonDateRow).CellsUsed(),
            c => c.Address.ColumnNumber >= GroupLessonsBlank.FirstLessonColumn
                 && c.DataType == XLDataType.DateTime
                 && c.GetDateTime().Date == day);
        return sheet.Cell(BlankName(sheet, token).Address.RowNumber, heading.Address.ColumnNumber);
    }

    /// <summary>The blank form's topic title cell (column D from row 4) holding exactly the title.</summary>
    public static IXLCell BlankTopic(IXLWorksheet sheet, string title) =>
        Assert.Single(
            sheet.Column(GroupLessonsBlank.TopicTitleColumn).CellsUsed(),
            c => c.Address.RowNumber >= GroupLessonsBlank.FirstTopicRow && c.GetString() == title);

    /// <summary>The text of a cell's note, or null when it has none.</summary>
    public static string? Note(IXLCell cell) => cell.HasComment ? cell.GetComment().Text : null;

    /// <summary>Spec FR-007.3: a cell carries a fill an ordinary cell of the same table does not.</summary>
    public static bool IsColouredUnlike(IXLCell cell, IXLCell ordinary) =>
        cell.Style.Fill.PatternType != XLFillPatternValues.None
        && !(ordinary.Style.Fill.PatternType == cell.Style.Fill.PatternType
             && Equals(ordinary.Style.Fill.BackgroundColor, cell.Style.Fill.BackgroundColor));

    private static async Task<long> StudentAsync(
        InstallationTestHost host,
        long courseId,
        int ordinal,
        string surname,
        bool onRoster,
        DateTimeOffset firstSeen,
        DateTimeOffset? lastSeen,
        CancellationToken cancellationToken)
    {
        var id = await CourseRows.InsertParticipantAsync(
            host, cancellationToken, googleUserId: CourseTestData.UserId(ordinal),
            email: CourseTestData.Email("student." + surname.ToLowerInvariant()),
            fullName: "Test " + surname, surname: surname, givenName: "Test");
        await CourseRows.InsertMembershipAsync(
            host, courseId, id, cancellationToken, firstSeenAt: firstSeen, lastSeenAt: lastSeen ?? host.Time.GetUtcNow(),
            onRoster: onRoster);
        return id;
    }

    private static async Task<long> ListCourseAsync(
        InstallationTestHost host, int ordinal, string name, long teacher, bool teacherOnRoster, CancellationToken cancellationToken)
    {
        var id = await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: CourseTestData.CourseId(ordinal), name: name, createdAt: SeenAt);
        await CourseRows.InsertMembershipAsync(
            host, id, teacher, cancellationToken, role: CourseTestData.Roles.Teacher, firstSeenAt: SeenAt,
            lastSeenAt: teacherOnRoster ? null : DataMarksWorld.LeftLastSeen, onRoster: teacherOnRoster);
        return id;
    }

    [GeneratedRegex(@"<th[^>]*\bscope=""col""[^>]*>(?<body>.*?)</th>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ColumnHeading();

    [GeneratedRegex(@"<tr[^>]*>(?<body>.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex RowElement();

    [GeneratedRegex(@"<td(?<attrs>[^>]*)>(?<body>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex DataCell();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
