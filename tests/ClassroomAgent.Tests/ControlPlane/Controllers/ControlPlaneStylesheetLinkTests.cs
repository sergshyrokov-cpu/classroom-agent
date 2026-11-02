using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-068 AC-001 (b), AC-002, AC-004 for the Control Plane, over HTTP: the sign-in page, the first-run setup page,
/// the error page for each status and the Owner's signed-in pages link the library and then <c>site.css</c> in their
/// <c>&lt;head&gt;</c>; both stylesheets, the licence, the version note and every override image are served with
/// <c>200</c> and their content type (spec FR-001.5, FR-002, FR-007, FR-010).
/// </summary>
public sealed class ControlPlaneStylesheetLinkTests(PostgreSqlFixture database)
{
    private static readonly string[] Expected = [Styling.BootstrapPath, Styling.SiteCssPath];

    /// <summary>AC-001 (b): the anonymous sign-in page (the Owner account exists).</summary>
    [Fact]
    public async Task TheSignInPage_LinksTheLibraryThenTheSiteStylesheet()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        (await host.CreateOwnerAsync(ct)).Dispose();
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync("/sign-in", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Expected, Styling.StylesheetLinks(page.Body));
    }

    /// <summary>AC-001 (b): the first-run setup page, before the Owner account exists.</summary>
    [Fact]
    public async Task TheSetupPage_LinksTheLibraryThenTheSiteStylesheet()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync("/setup", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Expected, Styling.StylesheetLinks(page.Body));
    }

    /// <summary>AC-001 (b): the Owner's signed-in pages.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/installations")]
    public async Task AnOwnerPage_LinksTheLibraryThenTheSiteStylesheet(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var page = await owner.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Expected, Styling.StylesheetLinks(page.Body));
    }

    /// <summary>AC-001 (b), FR-002.2, spec §8: the anonymous error page for 400, 403, 404 and 500.</summary>
    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task TheErrorPage_LinksTheLibraryThenTheSiteStylesheet(int status)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync($"/error/{status}", ct);

        Assert.Equal(status, (int)page.Status);
        Assert.Equal(Expected, Styling.StylesheetLinks(page.Body));
    }

    /// <summary>AC-001 (b), AC-002, FR-001.5: the two stylesheets as <c>text/css</c>, the licence and the version note as <c>text/plain</c>.</summary>
    [Theory]
    [InlineData(Styling.BootstrapPath, "text/css")]
    [InlineData(Styling.SiteCssPath, "text/css")]
    [InlineData(Styling.LicensePath, "text/plain")]
    [InlineData(Styling.VersionPath, "text/plain")]
    public async Task ALibraryFile_IsServedWithItsContentType(string path, string contentType)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.StartsWith(contentType, Styling.HeaderOf(response, "Content-Type") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-004, FR-007: every image the Control Plane <c>site.css</c> references is served as <c>image/svg+xml</c>.</summary>
    [Fact]
    public async Task EveryOverrideImage_IsServedAsSvg()
    {
        var ct = TestContext.Current.CancellationToken;
        var images = Styling.RootRelativeUrls(Styling.ReadRequired(Styling.FileOf(Styling.ControlPlane, Styling.SiteCssPath)));
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var problems = new List<string>();
        foreach (var path in images)
        {
            var response = await anonymous.GetAsync(path, ct);
            var type = Styling.HeaderOf(response, "Content-Type") ?? string.Empty;
            if (response.Status != HttpStatusCode.OK || !type.StartsWith("image/svg+xml", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"GET {path} → {(int)response.Status} {type}");
            }
        }

        Assert.Empty(problems);
    }
}
