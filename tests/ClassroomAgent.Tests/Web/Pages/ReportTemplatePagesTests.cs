using System.Globalization;
using System.Net;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-027 over HTTP: the template list, create, copy, change and deletion with confirmation, the built-in's 400, the
/// 404 and malformed references, the form re-render, the tampered form, HTML encoding, read-only 409 and the home page
/// link (AC-001, 002, 003, 006, 007, 009, 010; api-design §1–§2).
/// </summary>
public sealed class ReportTemplatePagesTests(PostgreSqlFixture database)
{
    private const string CopySuffixUk = " (копія)";

    private static async Task<long> SeedTemplateAsync(InstallationTestHost host, string name, CancellationToken ct)
    {
        const string author = "author.one@school-one.example.test";
        await host.InsertAppUserAsync(ct, email: author);
        var authorId = await host.AccountIdAsync(author, ct);
        return await host.InsertTemplateAsync(ct, name, authorId);
    }

    [Fact]
    public async Task TheList_ShowsTheBuiltIn_WithItsTranslatedName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(ReportTemplateTestData.ListPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(ReportTemplateTestData.TextKeys.BuiltInName, "uk"), page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATemplate_IsCreatedOverHttp_AndListedWithItsAuthor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var accountId = await host.AccountIdAsync(SignInTestData.AdminEmail, ct);
        var form = await client.GetAsync(ReportTemplateTestData.NewPath, ct);
        Assert.Equal(HttpStatusCode.OK, form.Status);

        var saved = await client.PostFormAsync(
            ReportTemplateTestData.ListPath, ReportTemplateFormBuilder.Valid("Test Template Http").Http(), ct);

        Assert.Equal(HttpStatusCode.Redirect, saved.Status);
        Assert.Equal(ReportTemplateTestData.ListPath, saved.LocationPath);
        var row = Assert.Single(await host.TemplateRowsAsync(ct));
        Assert.Equal("Test Template Http", row.Name);
        Assert.Equal(accountId, row.AuthorId);
        var audit = Assert.Single(await host.AuditRowsAsync(ct), a => a.Action == "report_template_created");
        Assert.Equal("succeeded", audit.Outcome);
        Assert.Equal("report_template", audit.TargetType);
        Assert.Equal(row.Id, audit.TargetId);

        var list = await client.GetAsync(ReportTemplateTestData.ListPath, ct);
        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Contains("Test Template Http", list.Text, StringComparison.Ordinal);
        Assert.Contains(
            host.Text(ReportTemplateTestData.TextKeys.Confirmation(ReportTemplateConfirmationKey.Created), "uk"),
            list.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATemplate_IsCopiedChangedAndDeletedOverHttp()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var builtInName = host.Text(ReportTemplateTestData.TextKeys.BuiltInName, "uk");

        var copyForm = await client.GetAsync(ReportTemplateTestData.CopyPath(ReportTemplateTestData.BuiltInKey), ct);
        Assert.Equal(HttpStatusCode.OK, copyForm.Status);
        Assert.Equal(builtInName + CopySuffixUk, Html.InputValue(copyForm.Body, "name"));
        var copied = await client.PostFormAsync(
            ReportTemplateTestData.ListPath, ReportTemplateFormBuilder.Valid("Test Template Copy").TwelvePoint().Http(), ct);
        Assert.Equal(HttpStatusCode.Redirect, copied.Status);
        var id = Assert.Single(await host.TemplateRowsAsync(ct)).Id.ToString(CultureInfo.InvariantCulture);

        var edit = await client.GetAsync(ReportTemplateTestData.EditPath(id), ct);
        Assert.Equal(HttpStatusCode.OK, edit.Status);
        var changed = await client.PostFormAsync(
            ReportTemplateTestData.ChangePath(id), ReportTemplateFormBuilder.Valid("Test Template Renamed").Http(), ct);
        Assert.Equal(HttpStatusCode.Redirect, changed.Status);
        Assert.Equal(ReportTemplateTestData.ListPath, changed.LocationPath);
        var row = Assert.Single(await host.TemplateRowsAsync(ct));
        Assert.Equal("Test Template Renamed", row.Name);
        Assert.Equal("none", row.ScaleMode);

        var deletion = await client.GetAsync(ReportTemplateTestData.DeletionPath(id), ct);
        Assert.Equal(HttpStatusCode.OK, deletion.Status);
        Assert.Contains("Test Template Renamed", deletion.Text, StringComparison.Ordinal);
        var deleted = await client.PostFormAsync(ReportTemplateTestData.DeletionPath(id), [], ct);
        Assert.Equal(HttpStatusCode.Redirect, deleted.Status);
        Assert.Equal(ReportTemplateTestData.ListPath, deleted.LocationPath);
        Assert.Empty(await host.TemplateRowsAsync(ct));
        Assert.Equal(0, await host.ScalarAsync<long>("SELECT count(*) FROM report_template_mark", ct));
        Assert.Equal(0, await host.ScalarAsync<long>("SELECT count(*) FROM report_template_scale_row", ct));
    }

    [Fact]
    public async Task TheBuiltIn_CannotBeEditedChangedOrDeleted_OverHttp()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var message = host.Text(
            ReportTemplateTestData.TextKeys.Reference(ReportTemplateReferenceMessageKey.BuiltInNotChangeable), "uk");
        var key = ReportTemplateTestData.BuiltInKey;

        var edit = await client.GetAsync(ReportTemplateTestData.EditPath(key), ct);
        var deletionPage = await client.GetAsync(ReportTemplateTestData.DeletionPath(key), ct);
        await client.GetAsync(ReportTemplateTestData.NewPath, ct);
        var change = await client.PostFormAsync(
            ReportTemplateTestData.ChangePath(key), ReportTemplateFormBuilder.Valid("Test Template Http").Http(), ct);
        var deletion = await client.PostFormAsync(ReportTemplateTestData.DeletionPath(key), [], ct);

        foreach (var response in new[] { edit, deletionPage, change, deletion })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.Status);
            Assert.Contains(message, response.Text, StringComparison.Ordinal);
        }

        Assert.Empty(await host.TemplateRowsAsync(ct));
    }

    [Fact]
    public async Task AnUnknownTemplate_Is404_WithTheMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(ReportTemplateTestData.EditPath("987654"), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Contains(
            host.Text(ReportTemplateTestData.TextKeys.Reference(ReportTemplateReferenceMessageKey.TemplateNotFound), "uk"),
            page.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMalformedReference_Is400_WithTheMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(ReportTemplateTestData.EditPath("abc"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(
            host.Text(ReportTemplateTestData.TextKeys.Reference(ReportTemplateReferenceMessageKey.TemplateMalformed), "uk"),
            page.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnInvalidScale_ReRendersTheForm_WithTheValuesAndTheMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await client.GetAsync(ReportTemplateTestData.NewPath, ct);
        var form = ReportTemplateFormBuilder.Valid("Test Template Overlap")
            .Ranges([(0, 60, "low"), (50, 100, "high")]);

        var page = await client.PostFormAsync(ReportTemplateTestData.ListPath, form.Http(), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal("Test Template Overlap", Html.InputValue(page.Body, "name"));
        Assert.Contains(
            host.Text(ReportTemplateTestData.TextKeys.Validation(ReportTemplateFieldErrorKey.ScaleOverlap), "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.Empty(await host.TemplateRowsAsync(ct));
    }

    [Fact]
    public async Task ATamperedForm_IsTheErrorPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await client.GetAsync(ReportTemplateTestData.NewPath, ct);
        var form = ReportTemplateFormBuilder.Valid("Test Template Tampered").Set("view", "wide");

        var page = await client.PostFormAsync(ReportTemplateTestData.ListPath, form.Http(), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.False(Html.HasInput(page.Body, "name"));
        Assert.Empty(await host.TemplateRowsAsync(ct));
    }

    [Fact]
    public async Task ATemplateName_IsHtmlEncoded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        const string raw = "<script>alert(1)</script>";
        await SeedTemplateAsync(host, raw, ct);

        var page = await client.GetAsync(ReportTemplateTestData.ListPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(raw, page.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InReadOnlyMode_ASaveIs409_AndNothingIsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(
            database, Actor.Admin, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;
        var form = await client.GetAsync(ReportTemplateTestData.NewPath, ct);
        Assert.Equal(HttpStatusCode.OK, form.Status);

        var page = await client.PostFormAsync(
            ReportTemplateTestData.ListPath, ReportTemplateFormBuilder.Valid("Test Template Http").Http(), ct);

        Assert.Equal(HttpStatusCode.Conflict, page.Status);
        Assert.Empty(await host.TemplateRowsAsync(ct));
    }

    [Theory]
    [InlineData(Actor.Admin)]
    [InlineData(Actor.Dean)]
    public async Task TheHomePage_LinksToTheTemplates(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;

        var page = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("href=\"/reports/templates\"", page.Body, StringComparison.Ordinal);
    }
}
