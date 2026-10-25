using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-055 AC-006 and AC-006a over HTTP (spec FR-005, I-3, I-4): a Dean's course journal, report and report export
/// show the data of AC-001 and AC-002 — maximum points 12 345 678.5, an assigned grade of 2 500 000 and a draft of
/// 1 000 000.25 — as stored, in the Ukrainian number format, and none of them fails. The values are seeded straight
/// into the database (synthetic, TC-4); before the change the seed itself is refused by the old column type.
/// </summary>
public sealed class LargeGradesPageTests(PostgreSqlFixture database)
{
    private const string LargeTitle = "Test Large Points Item";

    private const decimal MaxPoints = 12_345_678.5m;

    private const decimal Assigned = 2_500_000m;

    private const decimal Draft = 1_000_000.25m;

    /// <summary>The seeded journal (US-025) plus one September task carrying AC-001's maximum and AC-002's grades.</summary>
    private static async Task<SeededJournal> SeedLargeValuesAsync(InstallationTestHost host, CancellationToken ct)
    {
        var seeded = await host.SeedJournalAsync(ct);
        var item = await CourseWorkRows.InsertCourseWorkAsync(
            host, seeded.CourseId, ct, googleId: CourseWorkTestData.ItemId(30), title: LargeTitle,
            itemDate: JournalTestData.Period.Early.AddDays(3), maxPoints: MaxPoints);
        await CourseWorkRows.InsertSubmissionAsync(
            host, item, seeded.StudentId, ct, googleId: CourseWorkTestData.SubmissionId(30),
            state: CourseWorkTestData.StateCodes.TurnedIn, assignedGrade: Assigned, draftGrade: Draft);
        return seeded;
    }

    /// <summary>The value as the pages format it: trailing zeros dropped, the UI language's separator (uk by default).</summary>
    private static string Uk(decimal value) => value.ToString("0.############################", CultureInfo.GetCultureInfo("uk"));

    /// <summary>
    /// AC-006, FR-005: the journal's full view shows the cell's grade over the maximum and, for a student with no grade yet,
    /// the large draft, each as stored.
    /// </summary>
    [Fact]
    public async Task TheJournal_ShowsTheLargeGradeAndMaximum_AsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var seeded = await SeedLargeValuesAsync(host, ct);

        // US-025: a draft is shown only while no grade is assigned, so the large draft is carried by a second student.
        var draftStudent = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(31), fullName: "Test Student Large Draft");
        await CourseRows.InsertMembershipAsync(
            host, seeded.CourseId, draftStudent, ct, firstSeenAt: JournalTestData.Period.StartUtc.AddDays(-30));
        var item = await host.ScalarAsync<long>(
            "SELECT id FROM course_work WHERE google_id = @id", ct, ("id", CourseWorkTestData.ItemId(30)));
        await CourseWorkRows.InsertSubmissionAsync(
            host, item, draftStudent, ct, googleId: CourseWorkTestData.SubmissionId(31),
            state: CourseWorkTestData.StateCodes.TurnedIn, draftGrade: Draft);

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId, "full"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(LargeTitle, page.Text, StringComparison.Ordinal);
        Assert.Contains(Uk(Assigned) + " / " + Uk(MaxPoints), page.Text, StringComparison.Ordinal);
        Assert.Contains("Test Student Large Draft", page.Text, StringComparison.Ordinal);
        Assert.Contains(Uk(Draft), page.Text, StringComparison.Ordinal);
        Assert.Equal("12345678,5", Uk(MaxPoints));
    }

    /// <summary>
    /// AC-006, FR-005, spec I-4: a report by a template without conversion shows the raw grade over the maximum as stored.
    /// </summary>
    [Fact]
    public async Task ARawPointsReport_ShowsTheLargeGradeAndMaximum_AsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var seeded = await SeedLargeValuesAsync(host, ct);
        var author = await host.AccountIdAsync(DeanAccountTestData.DeanEmail, ct);
        var template = await host.InsertTemplateAsync(ct, "Test Template Raw Points", author);

        var page = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, template.ToString(CultureInfo.InvariantCulture)), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(LargeTitle, page.Text, StringComparison.Ordinal);
        Assert.Contains(Uk(Assigned) + " / " + Uk(MaxPoints), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-005, SPEC_REVIEW m-1: the built-in report converts by its scale — 2 500 000 of 12 345 678.5 is about 20 % — and
    /// the page does not fail on the large pair.
    /// </summary>
    [Fact]
    public async Task TheBuiltInScaleReport_DoesNotFail_OnTheLargeValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var seeded = await SeedLargeValuesAsync(host, ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(LargeTitle, page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-006a, FR-005, SPEC_REVIEW m-2: the report export in its table layout (the one the export endpoint builds; the
    /// shipped blank form renders grades through the same <c>GradingCell</c>) is produced and its cell carries the grade
    /// over the maximum as stored.
    /// </summary>
    [Fact]
    public async Task TheReportExport_CarriesTheLargeGradeAndMaximum_AsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var seeded = await SeedLargeValuesAsync(host, ct);
        var author = await host.AccountIdAsync(DeanAccountTestData.DeanEmail, ct);
        var template = await host.InsertTemplateAsync(ct, "Test Template Raw Export", author);

        var response = await client.ExportAsync(
            JournalExportHostExtensions.September(seeded.CourseId, template.ToString(CultureInfo.InvariantCulture)), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var texts = workbook.Worksheet(1).CellsUsed().Select(c => c.GetFormattedString()).ToList();
        Assert.Contains(texts, t => t.Contains(LargeTitle, StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains(Uk(Assigned) + " / " + Uk(MaxPoints), StringComparison.Ordinal));
    }
}
