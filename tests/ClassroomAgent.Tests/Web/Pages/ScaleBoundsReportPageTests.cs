using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-057 AC-006 and AC-007 a) over HTTP (spec FR-005): a report by a school-scale template shows the label of 100 % for
/// points at or above the maximum and the label of 0 % for points at or below zero, on the page and in the export, and
/// never fails. The course holds exactly one graded item so no other grade can produce the same labels. The values are
/// seeded straight into PostgreSQL and are synthetic (TC-4).
/// </summary>
public sealed class ScaleBoundsReportPageTests(PostgreSqlFixture database)
{
    private const string ItemTitle = "Test Scale Bounds Item";

    private const decimal MaxPoints = 0.0001m;

    private const decimal ExtremeGrade = 70000000000000000000000000000m;

    private static readonly IReadOnlyList<(int From, int To, string Label)> Scale =
        [(0, 0, "TSZERO"), (1, 99, "TSMID"), (100, 100, "TSFULL")];

    /// <summary>A course with one student and one item whose single submission carries the given grade in the given column.</summary>
    private static async Task<(long CourseId, long TemplateId)> SeedAsync(
        InstallationTestHost host, string which, decimal points, CancellationToken ct)
    {
        var courseId = await CourseRows.InsertCourseAsync(
            host, ct, googleId: CourseTestData.CourseId(57), name: "Test Course Scale Bounds",
            createdAt: JournalTestData.Period.StartUtc.AddDays(-30));
        var studentId = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(57), fullName: "Test Student Scale", surname: "Scale",
            givenName: "Test Student");
        await CourseRows.InsertMembershipAsync(
            host, courseId, studentId, ct, firstSeenAt: JournalTestData.Period.StartUtc.AddDays(-30));
        var itemId = await CourseWorkRows.InsertCourseWorkAsync(
            host, courseId, ct, googleId: CourseWorkTestData.ItemId(57), title: ItemTitle,
            itemDate: JournalTestData.Period.Early.AddDays(3), maxPoints: MaxPoints);
        await CourseWorkRows.InsertSubmissionAsync(
            host, itemId, studentId, ct, googleId: CourseWorkTestData.SubmissionId(57),
            state: CourseWorkTestData.StateCodes.TurnedIn,
            assignedGrade: which == "assigned" ? points : null,
            draftGrade: which == "draft" ? points : null);

        var author = await host.AccountIdAsync(DeanAccountTestData.DeanEmail, ct);
        var templateId = await host.InsertTemplateAsync(ct, "Test Template Scale Bounds", author, scale: Scale);
        return (courseId, templateId);
    }

    /// <summary>
    /// The page and the export of the report for the seeded course; both must succeed and carry the item, and the cell
    /// texts of both are returned for the label assertions.
    /// </summary>
    private async Task<(string PageText, List<string> ExportTexts)> ReportAsync(string which, decimal points)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var (courseId, templateId) = await SeedAsync(host, which, points, ct);
        var templateIdText = templateId.ToString(CultureInfo.InvariantCulture);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(courseId, templateIdText), ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(ItemTitle, page.Text, StringComparison.Ordinal);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(courseId, templateIdText), ct);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        var texts = workbook.Worksheet(1).CellsUsed().Select(c => c.GetFormattedString()).ToList();
        return (page.Text, texts);
    }

    /// <summary>
    /// AC-006, FR-005.1: points far above the maximum (7e28 of 0.0001) show the 100 % label on the page and in the
    /// export. Before the change the division overflows and the page or the export fails. A draft-only submission is not
    /// graded, so its cell shows a state mark plus the draft's label.
    /// </summary>
    [Theory]
    [InlineData("assigned")]
    [InlineData("draft")]
    public async Task AnExtremeGrade_ShowsTheLabelFor100Percent_OnThePageAndInTheExport(string which)
    {
        var (pageText, exportTexts) = await ReportAsync(which, ExtremeGrade);

        Assert.Contains("TSFULL", pageText, StringComparison.Ordinal);
        Assert.DoesNotContain("TSMID", pageText, StringComparison.Ordinal);
        Assert.DoesNotContain("TSZERO", pageText, StringComparison.Ordinal);
        Assert.Contains(exportTexts, t => t.Contains("TSFULL", StringComparison.Ordinal));
        Assert.DoesNotContain(exportTexts, t => t.Contains("TSMID", StringComparison.Ordinal));
        Assert.DoesNotContain(exportTexts, t => t.Contains("TSZERO", StringComparison.Ordinal));
    }

    /// <summary>
    /// AC-007 a), FR-005.2: zero points show the 0 % label on the page and in the export. Green before the change by
    /// design (0 / 0.0001 is already 0).
    /// </summary>
    [Theory]
    [InlineData("assigned")]
    [InlineData("draft")]
    public async Task AZeroGrade_ShowsTheLabelFor0Percent_OnThePageAndInTheExport(string which)
    {
        var (pageText, exportTexts) = await ReportAsync(which, 0m);

        Assert.Contains("TSZERO", pageText, StringComparison.Ordinal);
        Assert.DoesNotContain("TSMID", pageText, StringComparison.Ordinal);
        Assert.DoesNotContain("TSFULL", pageText, StringComparison.Ordinal);
        Assert.Contains(exportTexts, t => t.Contains("TSZERO", StringComparison.Ordinal));
        Assert.DoesNotContain(exportTexts, t => t.Contains("TSMID", StringComparison.Ordinal));
        Assert.DoesNotContain(exportTexts, t => t.Contains("TSFULL", StringComparison.Ordinal));
    }
}
