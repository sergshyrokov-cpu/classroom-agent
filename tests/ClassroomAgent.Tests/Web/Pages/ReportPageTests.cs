using System.Globalization;
using System.Net;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-027 over HTTP: the report of the seeded journal by the built-in and by a created template, the 400 and 404
/// pages, the English rendering and the read-only mode (AC-004, 005, 006, 009, 010; api-design §3).
/// </summary>
public sealed class ReportPageTests(PostgreSqlFixture database)
{
    private static readonly string[] ContentMarkers =
    [
        SeededJournal.GradedTitle, SeededJournal.UngradedTitle, SeededJournal.StudentName, SeededJournal.TeacherName,
    ];

    [Fact]
    public async Task TheBuiltInReport_ShowsGradingAndLessonTopics()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        foreach (var marker in ContentMarkers)
        {
            Assert.Contains(marker, page.Text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(SeededJournal.MaterialTitle, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.OctoberTitle, page.Text, StringComparison.Ordinal);
        Assert.Contains(">11<", page.Body, StringComparison.Ordinal);
        Assert.True(
            page.Text.Split(SeededJournal.UngradedTitle).Length - 1 >= 2,
            "The ungraded title must appear in the grading part and in the lesson topics.");
    }

    [Fact]
    public async Task ACreatedTemplate_ShowsTheMaterials_WhenItDoesNotHideThem()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var authorId = await host.AccountIdAsync(SignInTestData.AdminEmail, ct);
        var id = await host.InsertTemplateAsync(ct, "Test Template Full", authorId, view: "full", hideMaterials: false);

        var page = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, id.ToString(CultureInfo.InvariantCulture)), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(SeededJournal.MaterialTitle, page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMalformedQuery_Is400_AndIsNotEchoed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, "zz-bad-zz"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.True(Html.HasInput(page.Body, "from"));
        Assert.DoesNotContain("zz-bad-zz", page.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownTemplate_Is404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, "987654"), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Contains(
            host.Text(ReportTemplateTestData.TextKeys.Reference(ReportTemplateReferenceMessageKey.TemplateNotFound), "uk"),
            page.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownCourse_Is404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(987654), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Contains(host.Text("Journal.Validation.CourseUnknown", "uk"), page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheReport_RendersInEnglish_AndLeavesSchoolAndGoogleTextAsWritten()
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
        var authorId = await host.AccountIdAsync(SignInTestData.AdminEmail, ct);
        var id = await host.InsertTemplateAsync(ct, "Test Template Written", authorId);

        var page = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, id.ToString(CultureInfo.InvariantCulture)), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(SeededJournal.CourseName, page.Text, StringComparison.Ordinal);
        Assert.Contains("Test Template Written", page.Text, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.GradedTitle, page.Text, StringComparison.Ordinal);

        var builtIn = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);
        Assert.Equal(HttpStatusCode.OK, builtIn.Status);
        Assert.Contains(host.Text(ReportTemplateTestData.TextKeys.BuiltInName, "en"), builtIn.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(ReportTemplateTestData.TextKeys.BuiltInName, "uk"), builtIn.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InReadOnlyMode_TheReportIsStillShown_WithTheSameContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(
            database, Actor.Admin, ct, ReadOnlyModeHost.Cause.GracePeriodExpired);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        foreach (var marker in ContentMarkers)
        {
            Assert.Contains(marker, page.Text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(SeededJournal.MaterialTitle, page.Text, StringComparison.Ordinal);
        Assert.Contains(">11<", page.Body, StringComparison.Ordinal);
    }
}
