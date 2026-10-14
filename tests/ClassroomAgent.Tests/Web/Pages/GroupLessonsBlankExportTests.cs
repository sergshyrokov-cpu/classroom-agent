using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Blank = ClassroomAgent.Tests.TestInfrastructure.GroupLessonsBlank;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-043 over HTTP against PostgreSQL (TC-2; openapi <c>exportJournalXlsx</c> with <c>layout</c>): the shipped form
/// filled copy by copy, read back from the response bytes by sheets and cells (AC-011) and compared with the screen's
/// report — taken from the US-028 export of the same request — and with the shipped form itself.
/// </summary>
public sealed class GroupLessonsBlankExportTests(PostgreSqlFixture database)
{
    private static async Task<(InstallationTestHost Host, FormClient Client)> StartAsync(
        PostgreSqlFixture database, CancellationToken ct, Actor actor = Actor.Admin, ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly) =>
        await JournalHostExtensions.StartAsync(database, actor, ct, cause);

    /// <summary>Every copy of the file against the plan of spec FR-006, the screen and the shipped form.</summary>
    private static void AssertFile(XLWorkbook workbook, GroupLessonsBlankHostExtensions.Screen screen, int perCopyStudents, int perCopyLessons)
    {
        var plan = Blank.Plan(screen.Students.Count, screen.Lessons.Count, perCopyStudents, perCopyLessons);
        Assert.Equal(plan.Select(p => p.Name), workbook.Worksheets.Select(w => w.Name));
        using var form = Blank.Open(Blank.FormPath());
        for (var i = 0; i < plan.Count; i++)
        {
            var sheet = workbook.Worksheet(i + 1);
            if (plan[i].IsGrading)
            {
                GroupLessonsBlankHostExtensions.AssertGradingCopy(sheet, screen, plan[i], perCopyStudents, perCopyLessons);
                GroupLessonsBlankHostExtensions.AssertUntouched(sheet, form.Worksheet(Blank.GradingSheet), isGrading: true);
            }
            else
            {
                GroupLessonsBlankHostExtensions.AssertTopicsCopy(sheet, screen, plan[i], perCopyLessons);
                GroupLessonsBlankHostExtensions.AssertUntouched(sheet, form.Worksheet(Blank.TopicsSheet), isGrading: false);
            }
        }
    }

    private static void AssertRefused(BinaryResponse response, string? field, string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.StartsWith("application/json", response.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        var json = response.Json();
        Assert.Equal(400, json.GetProperty("status").GetInt32());
        Assert.Equal(message, json.GetProperty("message").GetString());
        Assert.Equal(JournalExportHostExtensions.ExportPath, json.GetProperty("path").GetString());
        var errors = json.TryGetProperty("fieldErrors", out var e) && e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray().ToList()
            : [];
        if (field is null)
        {
            Assert.Empty(errors);
        }
        else
        {
            var error = Assert.Single(errors);
            Assert.Equal(field, error.GetProperty("field").GetString());
            Assert.Equal(message, error.GetProperty("message").GetString());
        }
    }

    /// <summary>
    /// AC-001, AC-012, AC-015, FR-005, FR-006.1, FR-007: a small report with the defaults is one "Оцінювання" and one
    /// "Теми занять" — the screen's values in their places, whole-number grades as numbers, the rest of the form
    /// untouched, footers numbered 1; the response is the US-028 file response.
    /// </summary>
    [Fact]
    public async Task ASmallReport_FillsOneCopyOfEachSheet_WithTheScreensValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 6, lessons: 5, ct);
        var template = await host.InsertScaleTemplateAsync(ct);
        var screen = await host.ScreenAsync(client, course, ct, template);
        var program = await client.ExportAsync(JournalExportHostExtensions.September(course, template), ct);

        var response = await client.ExportAsync(Blank.Body(course, template: template), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.StartsWith(JournalExportHostExtensions.XlsxType, response.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("attachment", response.Header("Content-Disposition"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", response.Header("Cache-Control"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(program.FileNameStar(), response.FileNameStar());
        using var workbook = response.Workbook();
        Assert.Equal([Blank.GradingSheet, Blank.TopicsSheet], workbook.Worksheets.Select(w => w.Name));
        Assert.Equal(6, screen.Students.Count);
        Assert.Equal(5, screen.Lessons.Count);
        Assert.Contains(screen.Grades.SelectMany(g => g), c => c.Type == XLDataType.Number);
        Assert.Contains(screen.Grades.SelectMany(g => g), c => c.Type == XLDataType.Text && !c.IsBlank);
        Assert.Contains(GroupLessonsBlankHostExtensions.TeacherSurname, screen.Teachers, StringComparison.Ordinal);
        AssertFile(workbook, screen, Blank.StudentsPerCopyMax, Blank.LessonsPerCopyMax);
    }

    /// <summary>
    /// AC-002, AC-015, FR-006: 25 students and 30 lessons by 10 / 23 — the eight sheets of the Story table in order and
    /// with those names, numbering continuing across copies, every student and lesson once per block.
    /// </summary>
    [Fact]
    public async Task ALargeReport_IsSplitIntoTheStoryTablesEightSheets()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 25, lessons: 30, ct);
        var template = await host.InsertScaleTemplateAsync(ct);
        var screen = await host.ScreenAsync(client, course, ct, template);

        var response = await client.ExportAsync(Blank.Body(course, 10, 23, template), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal(
            ["Оцінювання 1", "Оцінювання 2", "Оцінювання 3", "Теми занять 1", "Оцінювання 4", "Оцінювання 5", "Оцінювання 6", "Теми занять 2"],
            workbook.Worksheets.Select(w => w.Name));
        Assert.Equal(25, screen.Students.Count);
        Assert.Equal(30, screen.Lessons.Count);
        AssertFile(workbook, screen, 10, 23);
    }

    /// <summary>
    /// AC-003, FR-005.5: a partly filled copy and a size below the capacities — rows past the chosen students, columns
    /// and topic rows past the chosen lessons are empty with no number, and the lines stay.
    /// </summary>
    [Fact]
    public async Task APartlyFilledCopy_LeavesUnusedRowsAndColumnsEmpty_AndKeepsTheLines()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 7, lessons: 5, ct);
        var screen = await host.ScreenAsync(client, course, ct);

        var response = await client.ExportAsync(Blank.Body(course, 5, 3), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal(
            ["Оцінювання 1", "Оцінювання 2", "Теми занять 1", "Оцінювання 3", "Оцінювання 4", "Теми занять 2"],
            workbook.Worksheets.Select(w => w.Name));
        AssertFile(workbook, screen, 5, 3);
    }

    /// <summary>VR-001 boundaries: one student and one lesson per copy is a copy per student and lesson.</summary>
    [Fact]
    public async Task OnePerCopy_IsTheLowerBoundary()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 2, lessons: 2, ct);
        var screen = await host.ScreenAsync(client, course, ct);

        var response = await client.ExportAsync(Blank.Body(course, 1, 1), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        AssertFile(workbook, screen, 1, 1);
    }

    /// <summary>VR-001 upper boundaries 30 / 23 given explicitly, and an explicit JSON <c>null</c> as the default (I-12).</summary>
    [Theory]
    [InlineData("30", "23")]
    [InlineData("null", "null")]
    [InlineData("30.0", "23.0")]
    public async Task TheUpperBoundaries_AndNull_AreAccepted(string students, string lessons)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 3, lessons: 2, ct);
        var screen = await host.ScreenAsync(client, course, ct);

        var response = await client.ExportAsync(
            Blank.RawBody(course, ("layout", "\"" + Blank.Layout + "\""), ("studentsPerCopy", students), ("lessonsPerCopy", lessons)), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        AssertFile(workbook, screen, Blank.StudentsPerCopyMax, Blank.LessonsPerCopyMax);
    }

    /// <summary>
    /// AC-004, FR-007, I-10: no layout member, <c>program</c>, an empty or <c>null</c> layout, and <c>program</c> with junk
    /// per-copy members all give exactly the US-028 file.
    /// </summary>
    [Theory]
    [InlineData("\"program\"", null, null)]
    [InlineData("\"\"", null, null)]
    [InlineData("null", null, null)]
    [InlineData("\"program\"", "\"zz\"", "true")]
    [InlineData("\"program\"", "0", "999")]
    public async Task TheProgramLayout_IsExactlyTheUs028File(string layout, string? students, string? lessons)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var members = new List<(string, string)> { ("layout", layout) };
        if (students is not null)
        {
            members.Add(("studentsPerCopy", students));
        }

        if (lessons is not null)
        {
            members.Add(("lessonsPerCopy", lessons));
        }

        var baseline = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);
        var response = await client.ExportAsync(Blank.RawBody(seeded.CourseId, [.. members]), ct);

        Assert.Equal(HttpStatusCode.OK, baseline.Status);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(baseline.FileNameStar(), response.FileNameStar());
        using var expected = baseline.Workbook();
        using var actual = response.Workbook();
        Assert.Equal(expected.Worksheets.Select(w => w.Name), actual.Worksheets.Select(w => w.Name));
        foreach (var sheet in expected.Worksheets)
        {
            var other = actual.Worksheet(sheet.Name);
            Assert.Equal(sheet.PageSetup.PageOrientation, other.PageSetup.PageOrientation);
            foreach (var cell in sheet.CellsUsed())
            {
                var twin = other.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber);
                Assert.Equal(cell.DataType, twin.DataType);
                Assert.Equal(cell.GetFormattedString(), twin.GetFormattedString());
            }

            Assert.Equal(sheet.CellsUsed().Count(), other.CellsUsed().Count());
        }
    }

    /// <summary>AC-005, VR-001: a per-copy value that is not a whole number in range is refused naming its field.</summary>
    [Theory]
    [InlineData("studentsPerCopy", "0", "Export.Validation.StudentsPerCopyInvalid")]
    [InlineData("studentsPerCopy", "31", "Export.Validation.StudentsPerCopyInvalid")]
    [InlineData("studentsPerCopy", "-1", "Export.Validation.StudentsPerCopyInvalid")]
    [InlineData("studentsPerCopy", "10.5", "Export.Validation.StudentsPerCopyInvalid")]
    [InlineData("studentsPerCopy", "\"10\"", "Export.Validation.StudentsPerCopyInvalid")]
    [InlineData("studentsPerCopy", "true", "Export.Validation.StudentsPerCopyInvalid")]
    [InlineData("studentsPerCopy", "[]", "Export.Validation.StudentsPerCopyInvalid")]
    [InlineData("studentsPerCopy", "{}", "Export.Validation.StudentsPerCopyInvalid")]
    [InlineData("lessonsPerCopy", "0", "Export.Validation.LessonsPerCopyInvalid")]
    [InlineData("lessonsPerCopy", "24", "Export.Validation.LessonsPerCopyInvalid")]
    [InlineData("lessonsPerCopy", "2.5", "Export.Validation.LessonsPerCopyInvalid")]
    [InlineData("lessonsPerCopy", "\"5\"", "Export.Validation.LessonsPerCopyInvalid")]
    [InlineData("lessonsPerCopy", "false", "Export.Validation.LessonsPerCopyInvalid")]
    public async Task AnInvalidPerCopyValue_Is400_NamingTheField_WithoutFileOrAudit(string field, string raw, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(
            Blank.RawBody(seeded.CourseId, ("layout", "\"" + Blank.Layout + "\""), (field, raw)), ct);

        AssertRefused(response, field, host.Text(key, "uk"));
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>AC-005, SC-10: the rejected value is never echoed in the response.</summary>
    [Fact]
    public async Task ARejectedValue_IsNotEchoed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(
            Blank.RawBody(seeded.CourseId, ("layout", "\"" + Blank.Layout + "\""), ("studentsPerCopy", "\"zz-students-zz\""), ("lessonsPerCopy", "7777")), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.DoesNotContain("zz-students-zz", response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("7777", response.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-005, VR-001: an unknown layout string is refused naming <c>layout</c>; the per-copy members are not read (api-design §2.4).</summary>
    [Theory]
    [InlineData("\"zz-layout-zz\"")]
    [InlineData("\"Group-Lessons-Blank\"")]
    [InlineData("\" group-lessons-blank\"")]
    [InlineData("\"PROGRAM\"")]
    public async Task AnUnknownLayout_Is400_NamingOnlyTheLayout(string layout)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(
            Blank.RawBody(seeded.CourseId, ("layout", layout), ("studentsPerCopy", "0"), ("lessonsPerCopy", "\"x\"")), ct);

        AssertRefused(response, "layout", host.Text("Export.Validation.LayoutInvalid", "uk"));
        Assert.DoesNotContain("zz-layout-zz", response.Text, StringComparison.Ordinal);
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>api-design §2.3: a layout that is not a JSON string is a malformed body, as every US-028 member.</summary>
    [Fact]
    public async Task ANonStringLayout_IsAMalformedBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(Blank.RawBody(seeded.CourseId, ("layout", "7")), ct);

        AssertRefused(response, null, host.Text("Export.Validation.RequestMalformed", "uk"));
    }

    /// <summary>api-design §2.4, openapi 400 (c′): failing fields in US-028 order, then the per-copy members; message = first.</summary>
    [Fact]
    public async Task SeveralFailures_AreListedInContractOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var body = Blank.RawBody(seeded.CourseId, ("layout", "\"" + Blank.Layout + "\""), ("studentsPerCopy", "0"), ("lessonsPerCopy", "0"))
            .Replace(JournalTestData.Period.FromText, "zz-bad-date", StringComparison.Ordinal);

        var response = await client.ExportAsync(body, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        var json = response.Json();
        Assert.Equal(
            ["from", "studentsPerCopy", "lessonsPerCopy"],
            json.GetProperty("fieldErrors").EnumerateArray().Select(e => e.GetProperty("field").GetString()));
        Assert.Equal(host.Text("Journal.Validation.FromMalformed", "uk"), json.GetProperty("message").GetString());
    }

    /// <summary>FR-007, I-8: with the form the orientation is validated as US-028 …</summary>
    [Fact]
    public async Task WithTheForm_AnUnknownOrientation_IsStillRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(Blank.Body(seeded.CourseId, orientation: "sideways"), ct);

        AssertRefused(response, "orientation", host.Text("Export.Validation.OrientationInvalid", "uk"));
    }

    /// <summary>… but not applied: the form's own portrait page setup rules.</summary>
    [Fact]
    public async Task WithTheForm_LandscapeIsNotApplied()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(Blank.Body(seeded.CourseId, orientation: "landscape"), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal([Blank.GradingSheet, Blank.TopicsSheet], workbook.Worksheets.Select(w => w.Name));
        Assert.All(workbook.Worksheets, w => Assert.Equal(XLPageOrientation.Portrait, w.PageSetup.PageOrientation));
    }

    /// <summary>
    /// AC-006, FR-005.1, FR-005.3: a template showing materials — no material reaches the form; marks, scale and late
    /// marks are the screen's.
    /// </summary>
    [Fact]
    public async Task ATemplateShowingMaterials_LeavesMaterialsOut_AndKeepsTheScreensMarks()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        // OD-013 T-2: 5 × 4 reaches s + l = 9, the seed's late mark, so a grade cell has two lines.
        var course = await host.SeedBlankCourseAsync(students: 5, lessons: 4, ct, materials: 3);
        var template = await host.InsertScaleTemplateAsync(ct, hideMaterials: false);
        var screen = await host.ScreenAsync(client, course, ct, template);

        var response = await client.ExportAsync(Blank.Body(course, template: template), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(3, screen.MaterialTitles.Count);
        Assert.Contains(screen.Grades.SelectMany(g => g), c => c.Type == XLDataType.Text && c.Text.Contains('\n', StringComparison.Ordinal));
        using var workbook = response.Workbook();
        var texts = workbook.Worksheets.SelectMany(w => w.CellsUsed()).Select(c => c.GetString()).ToList();
        Assert.All(screen.MaterialTitles, m => Assert.DoesNotContain(texts, t => t.Contains(m, StringComparison.Ordinal)));
        AssertFile(workbook, screen, Blank.StudentsPerCopyMax, Blank.LessonsPerCopyMax);
    }

    /// <summary>AC-007, FR-005.7, I-5: neither lessons nor students — one copy of each sheet, course and teachers written, nothing invented, audited.</summary>
    [Fact]
    public async Task AnEmptyReport_IsOneCopyOfEach_WithCourseAndTeachers_AndIsAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 0, lessons: 0, ct);
        var screen = await host.ScreenAsync(client, course, ct);
        var before = (await host.ExportAuditRowsAsync(ct)).Count;

        var response = await client.ExportAsync(Blank.Body(course), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal([Blank.GradingSheet, Blank.TopicsSheet], workbook.Worksheets.Select(w => w.Name));
        Assert.Contains(GroupLessonsBlankHostExtensions.CourseName, screen.Course, StringComparison.Ordinal);
        AssertFile(workbook, screen, Blank.StudentsPerCopyMax, Blank.LessonsPerCopyMax);
        using var form = Blank.Open(Blank.FormPath());
        var formTexts = form.Worksheets.SelectMany(w => w.CellsUsed()).Select(c => c.GetString()).ToHashSet(StringComparer.Ordinal);
        var invented = workbook.Worksheets.SelectMany(w => w.CellsUsed()).Select(c => c.GetString())
            .Where(t => t.Length > 0 && !formTexts.Contains(t) && t != screen.Course && t != screen.Teachers)
            .ToList();
        Assert.Empty(invented);
        var rows = await host.ExportAuditRowsAsync(ct);
        Assert.Equal(before + 1, rows.Count);
        Assert.Equal(0, rows[^1].Rows);
    }

    /// <summary>
    /// AC-007, OD-013 T-3: students on the roster but no lesson in the period — the report is "nothing published" and
    /// lists no student (US-027 FR-005.7), so the file is one copy of each sheet with the course and teachers only.
    /// </summary>
    [Fact]
    public async Task NoLessonInThePeriod_IsOneCopyOfEach_WithoutStudents()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 25, lessons: 0, ct);
        var screen = await host.ScreenAsync(client, course, ct);
        Assert.Empty(screen.Students);

        var response = await client.ExportAsync(Blank.Body(course, 10), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal([Blank.GradingSheet, Blank.TopicsSheet], workbook.Worksheets.Select(w => w.Name));
        AssertFile(workbook, screen, 10, Blank.LessonsPerCopyMax);
    }

    /// <summary>AC-007: lessons but no students — the blocks' copies with empty student areas.</summary>
    [Fact]
    public async Task LessonsWithoutStudents_FillTheDatesAndTopics()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 0, lessons: 5, ct);
        var screen = await host.ScreenAsync(client, course, ct);

        var response = await client.ExportAsync(Blank.Body(course, lessonsPerCopy: 4), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal(["Оцінювання 1", "Теми занять 1", "Оцінювання 2", "Теми занять 2"], workbook.Worksheets.Select(w => w.Name));
        AssertFile(workbook, screen, Blank.StudentsPerCopyMax, 4);
    }

    /// <summary>AC-008, FR-009, BR-026: in each read-only cause the form export works, is audited and calls no Google API.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheFormExportWorks_IsAudited_AndCallsNoGoogleApi(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct, cause: cause);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(Blank.Body(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        GroupLessonsBlankHostExtensions.AssertIsTheForm(workbook);
        Assert.Single(await host.ExportAuditRowsAsync(ct));
        Assert.Empty(host.Classroom.ImpersonatedAs);
    }

    /// <summary>AC-010, FR-005.3, S-09: text Excel would read as a formula is written as text with the quote prefix.</summary>
    [Fact]
    public async Task FormulaLikeText_IsWrittenAsQuotePrefixedText()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 1, lessons: 1, ct);
        await host.ExecuteAsync("UPDATE course SET name = @n, section = NULL WHERE id = @id", ct, ("n", "=SUM(1;2)"), ("id", course));
        await host.ExecuteAsync("UPDATE course_work SET title = @t WHERE course_id = @id", ct, ("t", "+Test Topic"), ("id", course));
        await host.ExecuteAsync(
            "UPDATE classroom_participant SET surname = @s WHERE surname = @old", ct, ("s", "@Студент"), ("old", GroupLessonsBlankHostExtensions.StudentSurname(1)));
        var screen = await host.ScreenAsync(client, course, ct);

        var response = await client.ExportAsync(Blank.Body(course), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var grading = workbook.Worksheet(Blank.GradingSheet);
        var topics = workbook.Worksheet(Blank.TopicsSheet);
        var courseCell = grading.Cell(Blank.CourseRow, Blank.CourseColumn);
        var nameCell = grading.Cell(Blank.FirstStudentRow, Blank.StudentNameColumn);
        var titleCell = topics.Cell(Blank.FirstTopicRow, Blank.TopicTitleColumn);
        foreach (var cell in new[] { courseCell, nameCell, titleCell })
        {
            Assert.False(cell.HasFormula, cell.Address.ToString());
            Assert.Equal(XLDataType.Text, cell.DataType);
            Assert.True(cell.Style.IncludeQuotePrefix, cell.Address.ToString());
        }

        Assert.Equal("=SUM(1;2)", courseCell.GetString());
        Assert.StartsWith("@Студент", nameCell.GetString(), StringComparison.Ordinal);
        Assert.Equal("+Test Topic", titleCell.GetString());
        AssertFile(workbook, screen, Blank.StudentsPerCopyMax, Blank.LessonsPerCopyMax);
    }

    /// <summary>FR-010, I-11: one audit row as US-028 — format xlsx, the student count, no personal data.</summary>
    [Fact]
    public async Task AFormExport_WritesOneAuditRow_WithoutPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct, Actor.Dean);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 12, lessons: 3, ct);

        var response = await client.ExportAsync(Blank.Body(course, 5), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using (var workbook = response.Workbook())
        {
            Assert.Equal(Blank.Plan(12, 3, 5, Blank.LessonsPerCopyMax).Select(p => p.Name), workbook.Worksheets.Select(w => w.Name));
        }

        var row = Assert.Single(await host.ExportAuditRowsAsync(ct));
        Assert.Equal("dean", row.ActorRole);
        Assert.Equal("course", row.TargetType);
        Assert.Equal(course, row.TargetId);
        Assert.Equal("succeeded", row.Outcome);
        Assert.Equal(JournalTestData.Period.From, row.From);
        Assert.Equal(JournalTestData.Period.To, row.To);
        Assert.True(row.BuiltIn);
        Assert.Equal(12, row.Rows);
        Assert.Equal("xlsx", row.Format);
        var json = string.Join('\n', await host.AuditRowsAsJsonAsync(ct));
        Assert.DoesNotContain(GroupLessonsBlankHostExtensions.StudentSurname(1), json, StringComparison.Ordinal);
        Assert.DoesNotContain(GroupLessonsBlankHostExtensions.CourseName, json, StringComparison.Ordinal);
        Assert.DoesNotContain(GroupLessonsBlankHostExtensions.LessonTitle(1), json, StringComparison.Ordinal);
        Assert.DoesNotContain(GroupLessonsBlankHostExtensions.TeacherSurname, json, StringComparison.Ordinal);
    }

    /// <summary>FR-012: an English user gets English program values, but the form's sheet names and printed text stay Ukrainian.</summary>
    [Fact]
    public async Task AnEnglishUser_KeepsTheFormsSheetNamesAndPrintedText()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertAppUserAsync(ct, uiLanguage: "en");
        host.ControlPlaneHandler = WorkspaceConnectionHostExtensions.ApprovingChannel();
        host.Start();
        var (client, callback) = await host.SignInWithGoogleAsync(ct);
        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        var course = await host.SeedBlankCourseAsync(students: 3, lessons: 3, ct);
        var screen = await host.ScreenAsync(client, course, ct, culture: "en");

        var response = await client.ExportAsync(Blank.Body(course), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal([Blank.GradingSheet, Blank.TopicsSheet], workbook.Worksheets.Select(w => w.Name));
        AssertFile(workbook, screen, Blank.StudentsPerCopyMax, Blank.LessonsPerCopyMax);
    }

    /// <summary>AC-014, VR-002, OD-011 a: more than 100 sheets is refused without a field; no file, no audit row.</summary>
    [Fact]
    public async Task MoreThanAHundredSheets_IsRefused_WithoutFileOrAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 10, lessons: 10, ct);

        var response = await client.ExportAsync(Blank.Body(course, 1, 1), ct);

        AssertRefused(response, null, host.Text("Export.Validation.TooManySheets", "uk"));
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>AC-014 boundary: exactly 100 sheets (9 × 10 + 10) is exported.</summary>
    [Fact]
    public async Task ExactlyAHundredSheets_IsExported()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await host.SeedBlankCourseAsync(students: 9, lessons: 10, ct);

        var response = await client.ExportAsync(Blank.Body(course, 1, 1), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal(Blank.SheetLimit, workbook.Worksheets.Count);
        Assert.Equal(Blank.Plan(9, 10, 1, 1).Select(p => p.Name), workbook.Worksheets.Select(w => w.Name));
    }

    /// <summary>openapi evaluation order: an unknown course is a 404 before the sheet limit is considered.</summary>
    [Fact]
    public async Task AnUnknownCourse_Is404_BeforeTheSheetLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, ct);
        await using var _host = host;
        await host.SeedBlankCourseAsync(students: 10, lessons: 10, ct);

        var response = await client.ExportAsync(Blank.Body(987654, 1, 1), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Equal(host.Text("Journal.Validation.CourseUnknown", "uk"), response.Json().GetProperty("message").GetString());
    }
}
