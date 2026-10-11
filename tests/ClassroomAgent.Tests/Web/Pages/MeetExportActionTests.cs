using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-044 AC-001 (spec FR-002; api-design §2.5, §4): the two Meet report pages offer the export action with the
/// antiforgery token meta tag, the orientation choice (landscape pre-selected) and the validated inputs; the bare form,
/// a refused query and the "meeting not found" page offer none; viewing writes nothing and works in read-only mode.
/// </summary>
public sealed class MeetExportActionTests(PostgreSqlFixture database)
{
    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>True when an <c>&lt;input&gt;</c> tag carries both the value and the <c>checked</c> attribute.</summary>
    private static bool IsChecked(string body, string value) =>
        Regex.Matches(body, "<input\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Any(m => m.Value.Contains($"value=\"{value}\"", StringComparison.Ordinal)
                      && Regex.IsMatch(m.Value, "\\bchecked\\b", RegexOptions.IgnoreCase));

    private static void AssertNoAction(InstallationTestHost host, PageResponse page)
    {
        Assert.DoesNotContain(host.Text("Report.Export.Action", "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("data-course-id", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-001: the reports page of a course offers the action with the token, the orientation and the period.</summary>
    [Fact]
    public async Task TheReportsPage_OffersTheExport_WithTheTokenOrientationAndPeriod()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("name=\"request-verification-token\"", page.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text("Report.Export.Action", "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains("value=\"portrait\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("value=\"landscape\"", page.Body, StringComparison.Ordinal);
        Assert.True(IsChecked(page.Body, "landscape"), "Landscape must be pre-selected.");
        Assert.False(IsChecked(page.Body, "portrait"), "Portrait must not be pre-selected.");
        Assert.Contains("data-course-id=\"" + Id(world.CourseId) + "\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("data-from=\"2026-10-01\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("data-to=\"2026-10-31\"", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-001: without dates the action carries the effective default period (the month of the host clock).</summary>
    [Fact]
    public async Task TheReportsPage_WithoutDates_CarriesTheDefaultPeriod()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.Url(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text("Report.Export.Action", "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains("data-from=\"2026-09-01\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("data-to=\"2026-09-30\"", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-001: the empty state of a course without meetings still offers the action.</summary>
    [Fact]
    public async Task TheReportsPage_OfACourseWithoutMeetings_StillOffersTheExport()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var courseId = await CourseRows.InsertCourseAsync(
            host, ct, googleId: CourseTestData.CourseId(9), name: "Test Course Empty");

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(courseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text("Report.Export.Action", "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains("data-course-id=\"" + Id(courseId) + "\"", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-001: the bare form, a malformed course and an unknown course offer no action.</summary>
    [Theory]
    [InlineData("/workspace/meet-reports")]
    [InlineData("/workspace/meet-reports?courseId=abc")]
    [InlineData("/workspace/meet-reports?courseId=999999")]
    public async Task TheReportsPage_WithoutAReport_OffersNoExport(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(path, ct);

        AssertNoAction(host, page);
    }

    /// <summary>AC-001: the meeting page offers the action with the course and the meeting.</summary>
    [Fact]
    public async Task TheMeetingPage_OffersTheExport_WithTheTokenAndOrientation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.MeetingUrl(Id(world.MeetingA), world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("name=\"request-verification-token\"", page.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text("Report.Export.Action", "uk"), page.Text, StringComparison.Ordinal);
        Assert.True(IsChecked(page.Body, "landscape"), "Landscape must be pre-selected.");
        Assert.Contains("data-course-id=\"" + Id(world.CourseId) + "\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("data-meeting-id=\"" + Id(world.MeetingA) + "\"", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-001: a meeting of another course is "not found" and offers no action.</summary>
    [Fact]
    public async Task TheMeetingNotFoundPage_OffersNoExport()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.MeetingUrl(Id(world.MeetingElsewhere), world.CourseId), ct);

        Assert.DoesNotContain(host.Text("Report.Export.Action", "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("data-meeting-id", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-011: in read-only mode the reports page still offers the action.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheReportsPage_StillOffersTheExport()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, "uk", ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text("Report.Export.Action", "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>Spec FR-002: viewing the pages writes nothing.</summary>
    [Fact]
    public async Task ViewingThePages_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        var before = await host.WriteFingerprintAsync(ct);

        await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);
        await client.GetAsync(MeetReportsTestData.MeetingUrl(Id(world.MeetingA), world.CourseId), ct);

        Assert.Equal(before, await host.WriteFingerprintAsync(ct));
    }
}
