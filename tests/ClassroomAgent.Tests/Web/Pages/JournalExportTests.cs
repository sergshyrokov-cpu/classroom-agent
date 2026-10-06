using System.Globalization;
using System.Net;
using ClassroomAgent.Application.Models.Export;
using ClosedXML.Excel;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-028 over HTTP against PostgreSQL (TC-2; openapi <c>exportJournalXlsx</c>, <c>getReport</c>): the file, its
/// headers and name, the workbook read back from the response bytes, parity with the report page, the language of the
/// exporting user, the audit row, read-only mode, the refusals and the export action on the report page.
/// </summary>
public sealed class JournalExportTests(PostgreSqlFixture database)
{
    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>The text of every non-empty cell of a sheet, row by row.</summary>
    private static IReadOnlyList<string> Texts(IXLWorksheet sheet) =>
        sheet.CellsUsed().Select(c => c.GetFormattedString()).Where(t => t.Length > 0).ToList();

    /// <summary>AC-001, AC-010, FR-006: Admin exports the built-in journal — xlsx, attachment, no-store, two sheets.</summary>
    [Fact]
    public async Task AnAdmin_ExportsTheBuiltInJournal_AsAnXlsxAttachment()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.StartsWith(JournalExportHostExtensions.XlsxType, response.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("attachment", response.Header("Content-Disposition"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", response.Header("Cache-Control"), StringComparison.OrdinalIgnoreCase);
        using var workbook = response.Workbook();
        Assert.Equal(
            [host.ReportText(ReportText.SheetGrading, "uk"), host.ReportText(ReportText.SheetLessonTopics, "uk")],
            workbook.Worksheets.Select(w => w.Name));
    }

    /// <summary>AC-008: a Dean is allowed as well.</summary>
    [Fact]
    public async Task ADean_ExportsTheJournal()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    /// <summary>AC-005, FR-006.1: course name and period only, the Cyrillic name intact through <c>filename*</c>, no person.</summary>
    [Fact]
    public async Task TheFileName_IsTheCourseAndPeriod_AndCarriesNoPerson()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        await host.ExecuteAsync("UPDATE course SET name = @n WHERE id = @id", ct, ("n", "Алгебра 7/А"), ("id", seeded.CourseId));

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Алгебра 7_А 2026-09-01–2026-09-30.xlsx", response.FileNameStar());
        var disposition = response.Header("Content-Disposition")!;
        Assert.DoesNotContain("student", disposition, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Test Student", disposition, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-001, FR-003: every text the file shows for the course, the items and the students is on the report page for
    /// the same inputs — the file never shows what the screen does not.
    /// </summary>
    [Fact]
    public async Task TheFile_ShowsWhatTheReportPageShows()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);
        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var grading = workbook.Worksheet(1);
        var cellTexts = grading.CellsUsed()
            .Where(c => c.Address.RowNumber > 6 && c.Address.ColumnNumber > 1)
            .SelectMany(c => c.GetFormattedString().Split('\n'))
            .Where(t => t.Length > 0)
            .ToList();
        Assert.NotEmpty(cellTexts);
        Assert.All(cellTexts, t => Assert.Contains(t, page.Text, StringComparison.Ordinal));
        var all = string.Join('\n', Texts(grading));
        Assert.Contains(SeededJournal.CourseName, all, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.GradedTitle, all, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.StudentName, all, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.TeacherName + "\n", all, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.OctoberTitle, all, StringComparison.Ordinal);
    }

    /// <summary>AC-002: a created template that hides materials — the material column is absent from the file as from the screen.</summary>
    [Fact]
    public async Task ACreatedTemplateHidingMaterials_LeavesTheMaterialOut()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var author = await host.AccountIdAsync(SignInTestData.AdminEmail, ct);
        var template = await host.InsertTemplateAsync(ct, "Test Template No Materials", author, hideMaterials: true);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId, Id(template)), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var all = string.Join('\n', Texts(workbook.Worksheet(1)));
        Assert.Contains("Test Template No Materials", all, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.GradedTitle, all, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.MaterialTitle, all, StringComparison.Ordinal);
    }

    /// <summary>AC-004, FR-007: an English-speaking user gets English program text; Google and school text stay as written.</summary>
    [Fact]
    public async Task AnEnglishUser_GetsEnglishProgramText_AndUntranslatedData()
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
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.Equal(host.ReportText(ReportText.SheetGrading, "en"), workbook.Worksheet(1).Name);
        var all = string.Join('\n', Texts(workbook.Worksheet(1)));
        Assert.Contains(host.Text("ReportTemplate.BuiltIn.AcademicJournal", "en"), all, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.CourseName, all, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.StudentName, all, StringComparison.Ordinal);
    }

    /// <summary>AC-007, FR-010: one audit row with ids, period, built-in marker, row count and format — and no personal data.</summary>
    [Fact]
    public async Task AnExport_WritesOneAuditRow_WithoutPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var adminId = await host.AccountIdAsync(SignInTestData.AdminEmail, ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var row = Assert.Single(await host.ExportAuditRowsAsync(ct));
        Assert.Equal("app_user", row.ActorType);
        Assert.Equal(adminId, row.ActorId);
        Assert.Equal("admin", row.ActorRole);
        Assert.Equal("course", row.TargetType);
        Assert.Equal(seeded.CourseId, row.TargetId);
        Assert.Equal("succeeded", row.Outcome);
        Assert.False(string.IsNullOrEmpty(row.RequestId));
        Assert.Equal(JournalTestData.Period.From, row.From);
        Assert.Equal(JournalTestData.Period.To, row.To);
        Assert.Null(row.TemplateId);
        Assert.True(row.BuiltIn);
        Assert.Equal(2, row.Rows);
        Assert.Equal("xlsx", row.Format);
        var json = string.Join('\n', await host.AuditRowsAsJsonAsync(ct));
        Assert.DoesNotContain("Test Student", json, StringComparison.Ordinal);
        Assert.DoesNotContain("student.one", json, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.CourseName, json, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.GradedTitle, json, StringComparison.Ordinal);
    }

    /// <summary>AC-007, FR-010: viewing the report and switching names write no audit row.</summary>
    [Fact]
    public async Task ViewingTheReport_WritesNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var before = (await host.AuditRowsAsync(ct)).Count;

        await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);
        await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, names: "email"), ct);

        Assert.Equal(before, (await host.AuditRowsAsync(ct)).Count);
    }

    /// <summary>AC-006, FR-009, BR-026: in each read-only cause the export works, is audited and calls no Google API.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheExportWorks_IsAudited_AndCallsNoGoogleApi(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Single(await host.ExportAuditRowsAsync(ct));
        Assert.Empty(host.Classroom.ImpersonatedAs);
    }

    /// <summary>AC-009, FR-005, API-6: a malformed value is a 400 JSON body naming the field; never echoed; no file, no audit row.</summary>
    [Fact]
    public async Task AMalformedValue_Is400_NamingTheField_WithoutEchoOrAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var body = JournalExportHostExtensions.September(seeded.CourseId);
        body["from"] = "zz-bad-date-zz";

        var response = await client.ExportAsync(body, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.StartsWith("application/json", response.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        var json = response.Json();
        var expected = host.Text("Journal.Validation.FromMalformed", "uk");
        Assert.Equal(400, json.GetProperty("status").GetInt32());
        Assert.Equal(expected, json.GetProperty("message").GetString());
        Assert.Equal(JournalExportHostExtensions.ExportPath, json.GetProperty("path").GetString());
        var error = Assert.Single(json.GetProperty("fieldErrors").EnumerateArray());
        Assert.Equal("from", error.GetProperty("field").GetString());
        Assert.Equal(expected, error.GetProperty("message").GetString());
        Assert.DoesNotContain("zz-bad-date-zz", response.Text, StringComparison.Ordinal);
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>OD-004 a, FR-013: an unknown orientation is refused with its own translated message.</summary>
    [Fact]
    public async Task AnUnknownOrientation_Is400_WithItsOwnMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId, orientation: "sideways"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        var error = Assert.Single(response.Json().GetProperty("fieldErrors").EnumerateArray());
        Assert.Equal("orientation", error.GetProperty("field").GetString());
        Assert.Equal(host.Text("Export.Validation.OrientationInvalid", "uk"), error.GetProperty("message").GetString());
    }

    /// <summary>OD-004 a, FR-004.8: the chosen orientation is the file's page setup.</summary>
    [Theory]
    [InlineData(null, XLPageOrientation.Portrait)]
    [InlineData("landscape", XLPageOrientation.Landscape)]
    public async Task TheChosenOrientation_IsTheFilesPageSetup(string? orientation, XLPageOrientation expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId, orientation: orientation), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.All(workbook.Worksheets, w => Assert.Equal(expected, w.PageSetup.PageOrientation));
    }

    /// <summary>AC-009, FR-005, API-5: an unknown course is a 404 JSON body naming the problem; no audit row.</summary>
    [Fact]
    public async Task AnUnknownCourse_Is404_NamingIt_WithoutAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(987654), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        var json = response.Json();
        Assert.Equal(404, json.GetProperty("status").GetInt32());
        Assert.Equal(host.Text("Journal.Validation.CourseUnknown", "uk"), json.GetProperty("message").GetString());
        Assert.Equal("courseId", Assert.Single(json.GetProperty("fieldErrors").EnumerateArray()).GetProperty("field").GetString());
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>AC-009: an unknown created template is a 404 naming the template.</summary>
    [Fact]
    public async Task AnUnknownTemplate_Is404_NamingIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId, "424242"), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Equal(
            host.Text("ReportTemplate.Reference.TemplateNotFound", "uk"),
            response.Json().GetProperty("message").GetString());
    }

    /// <summary>api-design §2.3: a body that is not a JSON object of strings is malformed — no field is named.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"courseId\": 42}")]
    [InlineData("{\"courseId\":\"1\",\"courseId\":\"2\"}")]
    public async Task AMalformedBody_Is400_WithoutFieldErrors(string body)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var response = await client.ExportAsync(body, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        var json = response.Json();
        Assert.Equal(host.Text("Export.Validation.RequestMalformed", "uk"), json.GetProperty("message").GetString());
        Assert.False(json.TryGetProperty("fieldErrors", out var errors) && errors.ValueKind == System.Text.Json.JsonValueKind.Array && errors.GetArrayLength() > 0);
    }

    /// <summary>API-2, API-5: a body that is not JSON is 415 with the API-6 body.</summary>
    [Fact]
    public async Task AFormBody_Is415()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(
            "courseId=" + Id(seeded.CourseId), ct, contentType: "application/x-www-form-urlencoded");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.Status);
        Assert.Equal(415, response.Json().GetProperty("status").GetInt32());
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>FR-002: the report page offers the action with the token meta tag and the orientation choice, portrait checked.</summary>
    [Fact]
    public async Task TheReportPage_OffersTheExport_WithTheTokenAndOrientation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, names: "email"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("name=\"request-verification-token\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("/js/report-export.js", page.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text("Report.Export.Action", "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains("value=\"portrait\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("value=\"landscape\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("data-names=\"email\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("data-course-id=\"" + Id(seeded.CourseId) + "\"", page.Body, StringComparison.Ordinal);
    }

    /// <summary>FR-002: no action on the bare form or after a validation failure.</summary>
    [Fact]
    public async Task TheReportPage_OffersNoExport_WithoutAReport()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var bare = await client.GetAsync(ReportTemplateTestData.ReportPath, ct);
        var invalid = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, names: "zz"), ct);

        Assert.DoesNotContain("/js/report-export.js", bare.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("/js/report-export.js", invalid.Body, StringComparison.Ordinal);
    }
}
