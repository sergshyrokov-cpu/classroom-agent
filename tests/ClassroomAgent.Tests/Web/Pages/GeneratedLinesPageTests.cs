using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.DataMarksHostExtensions;
using W = ClassroomAgent.Tests.TestInfrastructure.DataMarksWorld;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-063 on the journal and report pages over HTTP and PostgreSQL (TC-2): the "generated" and "data from Google as of"
/// lines (FR-003) — B of the build (FR-001) and the course's last complete read (FR-002) in Kyiv time (FR-006.1), in
/// Ukrainian and English (FR-006.3). The marks course of US-060 is reused; its stored last read is R2 unless a test
/// changes it. The clock is the host's manual clock, so B is known exactly (TC-4).
/// </summary>
public sealed class GeneratedLinesPageTests(PostgreSqlFixture database)
{
    private const string JournalTableId = "journal-table";

    public static TheoryData<string> Languages => new("uk", "en");

    public static TheoryData<string, string> LanguagesAndViews => new()
    {
        { "uk", "full" },
        { "uk", "short" },
        { "en", "full" },
    };

    /// <summary>
    /// AC-001, AC-005, FR-003.1, FR-006: the journal shows «Сформовано: G1» and «Дані з Google на: R2», in Kyiv time, each
    /// in its own element above the table, in both views.
    /// </summary>
    [Theory]
    [MemberData(nameof(LanguagesAndViews))]
    public async Task TheJournal_ShowsGeneratedAndDataAsOf_AboveTheTable(string language, string view)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct);
        var texts = GeneratedLinesTexts.Of(language);
        var g1 = host.Time.GetUtcNow();

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(world.CourseId, view), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertJournalLines(page.Body, texts.GeneratedLine(g1), texts.DataAsOfLine(W.R2));
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>
    /// AC-001, FR-006.1, VR-003, TC-8: the Ukrainian lines pinned as literals — 08:00 UTC is 11:00 in Kyiv, R2
    /// (2026-09-07 22:00 UTC) is already 08.09 in Kyiv; no seconds.
    /// </summary>
    [Fact]
    public async Task TheJournalLines_AreInKyivTime_WithoutSeconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct);
        Assert.Equal(InstallationTestHost.DefaultStart, host.Time.GetUtcNow());

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(world.CourseId), ct);

        AssertJournalLines(page.Body, "Сформовано: 17.09.2026 11:00", "Дані з Google на: 08.09.2026 01:00");
    }

    /// <summary>AC-001, AC-005, FR-003.2: the report header shows both lines after the teachers line.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheReportHeader_ShowsGeneratedAndDataAsOf(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct);
        var texts = GeneratedLinesTexts.Of(language);
        var g1 = host.Time.GetUtcNow();

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(world.CourseId, world.TemplateId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertReportLines(page.Body, texts.GeneratedLine(g1), texts.DataAsOfLine(W.R2));
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>
    /// AC-001, FR-003.1, FR-003.2: next to US-060's header line the two lines are added, the US-060 line stays as it was.
    /// </summary>
    [Fact]
    public async Task NextToTheCourseHeaderLine_BothLinesAreShown_AndTheHeaderLineStays()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct, CourseHeaderVariant.NotInClassroom);
        var texts = GeneratedLinesTexts.Uk;
        var g1 = host.Time.GetUtcNow();

        var journal = await client.GetAsync(JournalTestData.SeptemberUrl(world.CourseId), ct);
        var report = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(world.CourseId, world.TemplateId), ct);

        var headerLine = W.HeaderLine(CourseHeaderVariant.NotInClassroom, DataMarksTexts.Uk)!;
        AssertJournalLines(journal.Body, texts.GeneratedLine(g1), texts.DataAsOfLine(W.R2));
        Assert.Equal(headerLine, Text(Element(journal.Body, "journal-notice") ?? string.Empty));
        AssertReportLines(report.Body, texts.GeneratedLine(g1), texts.DataAsOfLine(W.R2));
        Assert.Equal(headerLine, Text(Element(report.Body, "report-notice") ?? string.Empty));
    }

    /// <summary>
    /// AC-002, FR-001.1, VR-001: an item due five minutes after G1. Before the due moment its cell is "not due yet" and
    /// «Сформовано» shows G1; ten minutes later the same cell is "not turned in" and the line shows the new B — the cell
    /// state and the line agree each time.
    /// </summary>
    [Fact]
    public async Task TheGeneratedLine_IsTheInstantTheCellsWereBuiltFor()
    {
        const string dueTitle = "Test Generated Due";
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct);
        var texts = GeneratedLinesTexts.Uk;
        var before = host.Time.GetUtcNow();
        var due = before.AddMinutes(5);
        var item = await CourseWorkRows.InsertCourseWorkAsync(
            host, world.CourseId, ct, googleId: CourseWorkTestData.ItemId(631), title: dueTitle,
            itemDate: due, dueAt: due, maxPoints: 10m);
        var over = await host.ScalarAsync<long>(
            "SELECT id FROM classroom_participant WHERE surname = @surname", ct, ("surname", W.Over));
        await CourseWorkRows.InsertSubmissionAsync(
            host, item, over, ct, googleId: CourseWorkTestData.SubmissionId(631), state: CourseWorkTestData.StateCodes.Created);

        var first = await client.GetAsync(JournalTestData.SeptemberUrl(world.CourseId, "full"), ct);
        host.Time.Advance(TimeSpan.FromMinutes(10));
        var after = host.Time.GetUtcNow();
        var second = await client.GetAsync(JournalTestData.SeptemberUrl(world.CourseId, "full"), ct);

        Assert.True(before < due && due < after);
        Assert.Equal("термін ще не минув", Text(CellOf(Table(first.Body, JournalTableId), W.Over, dueTitle).Body));
        Assert.Equal(texts.GeneratedLine(before), Text(Element(first.Body, "journal-generated") ?? string.Empty));
        Assert.Equal("не здано", Text(CellOf(Table(second.Body, JournalTableId), W.Over, dueTitle).Body));
        Assert.Equal(texts.GeneratedLine(after), Text(Element(second.Body, "journal-generated") ?? string.Empty));
    }

    /// <summary>AC-006, FR-002.3, OD-001 a: no last read recorded — the line reads "unknown", on the journal and the report.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task WithNoLastReadRecorded_TheDataAsOfLineSaysUnknown(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct);
        await host.ExecuteAsync(
            "UPDATE course SET last_complete_read_at = NULL WHERE id = @id", ct, ("id", world.CourseId));
        var texts = GeneratedLinesTexts.Of(language);
        var g1 = host.Time.GetUtcNow();

        var journal = await client.GetAsync(JournalTestData.SeptemberUrl(world.CourseId), ct);
        var report = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(world.CourseId, world.TemplateId), ct);

        AssertJournalLines(journal.Body, texts.GeneratedLine(g1), texts.DataAsOfLine(null));
        AssertReportLines(report.Body, texts.GeneratedLine(g1), texts.DataAsOfLine(null));
    }

    /// <summary>
    /// AC-009, FR-002.1, VR-002, OD-002 a: an unchanged archived course listed after its last complete read — the line
    /// shows the last complete read, not the later listing.
    /// </summary>
    [Fact]
    public async Task ForAnUnchangedArchivedCourse_TheLineShowsTheLastCompleteRead_NotTheLastListing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct, CourseHeaderVariant.ArchivedUnchanged);
        var listed = W.R2.AddDays(5);
        await host.ExecuteAsync(
            "UPDATE course SET last_listed_at = @listed WHERE id = @id", ct, ("listed", listed), ("id", world.CourseId));
        var texts = GeneratedLinesTexts.Uk;

        var journal = await client.GetAsync(JournalTestData.SeptemberUrl(world.CourseId), ct);
        var report = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(world.CourseId, world.TemplateId), ct);

        Assert.Equal(texts.DataAsOfLine(W.R2), Text(Element(journal.Body, "journal-data-as-of") ?? string.Empty));
        Assert.Equal(texts.DataAsOfLine(W.R2), Text(Element(report.Body, "report-data-as-of") ?? string.Empty));
        Assert.DoesNotContain(texts.Instant(listed), journal.Text, StringComparison.Ordinal);
    }

    /// <summary>FR-003.3: a period with no data still shows both lines on the journal and the report.</summary>
    [Fact]
    public async Task AnEmptyPeriod_StillShowsBothLines()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct);
        var texts = GeneratedLinesTexts.Uk;
        var g1 = host.Time.GetUtcNow();

        var journal = await client.GetAsync(JournalTestData.Url(world.CourseId, "2026-07-01", "2026-07-31"), ct);
        var report = await client.GetAsync(
            ReportTemplateTestData.ReportUrl(world.TemplateId, world.CourseId, "2026-07-01", "2026-07-31"), ct);

        Assert.Equal(HttpStatusCode.OK, journal.Status);
        Assert.Equal(HttpStatusCode.OK, report.Status);
        Assert.Equal(texts.GeneratedLine(g1), Text(Element(journal.Body, "journal-generated") ?? string.Empty));
        Assert.Equal(texts.DataAsOfLine(W.R2), Text(Element(journal.Body, "journal-data-as-of") ?? string.Empty));
        Assert.Equal(texts.GeneratedLine(g1), Text(Element(report.Body, "report-generated") ?? string.Empty));
        Assert.Equal(texts.DataAsOfLine(W.R2), Text(Element(report.Body, "report-data-as-of") ?? string.Empty));
    }

    /// <summary>FR-003.1: with no course selected the journal shows neither line.</summary>
    [Fact]
    public async Task WithNoCourseSelected_TheJournalShowsNeitherLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct);
        await using var _host = host;
        await host.SeedMarksCourseAsync(ct);

        var page = await client.GetAsync(JournalTestData.Url(), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Null(Element(page.Body, "journal-generated"));
        Assert.Null(Element(page.Body, "journal-data-as-of"));
        foreach (var stem in GeneratedLinesTexts.Uk.Stems)
        {
            Assert.DoesNotContain(stem, page.Text, StringComparison.Ordinal);
        }
    }

    /// <summary>Each line is its element's whole text, occurs once on the page, and stands above the journal table.</summary>
    private static void AssertJournalLines(string html, string generated, string dataAsOf)
    {
        Assert.Equal(generated, Text(Element(html, "journal-generated") ?? string.Empty));
        Assert.Equal(dataAsOf, Text(Element(html, "journal-data-as-of") ?? string.Empty));
        var table = html.IndexOf("id=\"" + JournalTableId + "\"", StringComparison.Ordinal);
        Assert.True(table > 0, "The journal has no table.");
        Assert.True(html.IndexOf("id=\"journal-generated\"", StringComparison.Ordinal) < table);
        Assert.True(html.IndexOf("id=\"journal-data-as-of\"", StringComparison.Ordinal) < table);
        AssertOnce(Text(html), generated);
        AssertOnce(Text(html), dataAsOf);
    }

    /// <summary>Each line is its element's whole text, inside <c>#report-header</c>, once on the page.</summary>
    private static void AssertReportLines(string html, string generated, string dataAsOf)
    {
        var header = Element(html, "report-header");
        Assert.NotNull(header);
        Assert.Equal(generated, Text(Element(header, "report-generated") ?? string.Empty));
        Assert.Equal(dataAsOf, Text(Element(header, "report-data-as-of") ?? string.Empty));
        AssertOnce(Text(html), generated);
        AssertOnce(Text(html), dataAsOf);
    }

    private static void AssertOnce(string text, string line)
    {
        var at = text.IndexOf(line, StringComparison.Ordinal);
        Assert.True(at >= 0, $"The page does not show «{line}».");
        Assert.Equal(-1, text.IndexOf(line, at + line.Length, StringComparison.Ordinal));
    }
}
