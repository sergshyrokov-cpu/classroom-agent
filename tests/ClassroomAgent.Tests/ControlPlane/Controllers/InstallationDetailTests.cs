using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-005: the detail page shows the identifier to copy into configuration (FR-005, spec I-5, I-6).</summary>
public sealed class InstallationDetailTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Detail_ShowsIdentifierNameDomainStatusCreationTimeAndClientId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var createdAt = new DateTimeOffset(2026, 9, 16, 13, 29, 59, TimeSpan.Zero);
        var identifier = await host.InsertInstallationAsync(ct, createdAt: createdAt);

        var response = await owner.GetAsync($"/installations/{identifier:D}", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(identifier.ToString("D"), response.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.Name, response.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.Domain, response.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.ClientId, response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.Status.Active", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains("16.09.2026 13:29 UTC", response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detail_IdentifierIsSelectableText_WithCopyButtonScriptAndConfigurationNote()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await owner.GetAsync($"/installations/{identifier:D}", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Matches(
            $"<[a-z0-9]+[^>]*\\bid=\"installation-identifier\"[^>]*>\\s*{identifier:D}\\s*<",
            response.Body);
        Assert.Matches("<button[^>]*\\bdata-copy-target=\"installation-identifier\"", response.Body);
        Assert.Matches("<script[^>]*\\bsrc=\"(https://localhost)?/js/copy-identifier\\.js\"", response.Body);
        Assert.Contains(host.Text("Installation.Identifier.ConfigurationNote", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.Identifier.Copied", "uk"), response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detail_HasNameAndClientIdActions_NoDomainStatusOrDeleteControl()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await owner.GetAsync($"/installations/{identifier:D}", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Matches($"href=\"(https://localhost)?/installations/{identifier:D}/name\"", response.Body);
        Assert.Matches($"href=\"(https://localhost)?/installations/{identifier:D}/client-id\"", response.Body);
        Assert.Matches("href=\"(https://localhost)?/installations\"", response.Body);
        var installationForms = Regex.Matches(response.Body, "<form[^>]*action=\"[^\"]*/installations[^\"]*\"", RegexOptions.IgnoreCase);
        Assert.Empty(installationForms);
        Assert.DoesNotMatch("/installations/[^\"]*/(domain|status|delete|suspend)", response.Body);
        Assert.False(Html.HasInput(response.Body, "domain"));
    }

    [Fact]
    public async Task Detail_NoInlineScriptOrInlineEventHandler()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await owner.GetAsync($"/installations/{identifier:D}", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.DoesNotMatch("<script(?![^>]*\\bsrc=)[^>]*>", response.Body);
        Assert.DoesNotMatch("\\son[a-z]+\\s*=", response.Body);
    }

    [Fact]
    public async Task EnteredValues_AreHtmlEncoded_OnDetailListAndForms()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        const string markup = "<b>Школа</b>\"'<script>alert(1)</script>";
        var identifier = await host.RegisterInstallationAsync(owner, ct, name: markup);

        var pages = new[]
        {
            await owner.GetAsync($"/installations/{identifier:D}", ct),
            await owner.GetAsync("/installations", ct),
            await owner.GetAsync($"/installations/{identifier:D}/name", ct),
        };

        Assert.All(pages, page =>
        {
            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.DoesNotContain("<b>Школа</b>", page.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("<script>alert(1)</script>", page.Body, StringComparison.Ordinal);
            Assert.Contains(markup, page.Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Detail_UnknownIdentifier_Returns404ErrorPage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.InsertInstallationAsync(ct);

        var response = await owner.GetAsync($"/installations/{InstallationTestData.UnknownIdentifier}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Null(response.Location);
        Assert.Contains(host.Text("Error.NotFound", "uk"), response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallationTestData.Domain, response.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/installations/not-a-uuid")]
    [InlineData("/installations/12345")]
    [InlineData("/installations/not-a-uuid/name")]
    [InlineData("/installations/not-a-uuid/client-id")]
    public async Task NonUuidIdentifier_Returns404ErrorPage(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Contains(host.Text("Error.NotFound", "uk"), response.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("client-id")]
    public async Task EditPages_UnknownIdentifier_GetAndPostReturn404(string page)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var existing = await host.InsertInstallationAsync(ct);
        var path = $"/installations/{InstallationTestData.UnknownIdentifier}/{page}";
        await owner.GetAsync($"/installations/{existing:D}/{page}", ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var get = await owner.GetAsync(path, ct);
        await owner.GetAsync($"/installations/{existing:D}/{page}", ct);
        var post = await owner.PostFormAsync(
            path,
            page == "name" ? InstallationTestData.NameFields(string.Empty) : InstallationTestData.ClientIdFields("x"),
            ct);

        Assert.Equal(HttpStatusCode.NotFound, get.Status);
        Assert.Equal(HttpStatusCode.NotFound, post.Status);
        Assert.Contains(host.Text("Error.NotFound", "uk"), post.Text, StringComparison.Ordinal);
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task CopyScript_IsServedAsStaticFile_WithoutHardCodedText()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.GetAsync("/js/copy-identifier.js", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains("clipboard", response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-copy-target", response.Body, StringComparison.Ordinal);
        Assert.DoesNotMatch("[\\u0400-\\u04FF]", response.Body);
    }
}
