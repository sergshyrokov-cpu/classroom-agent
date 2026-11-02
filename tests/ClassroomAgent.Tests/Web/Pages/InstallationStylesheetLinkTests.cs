using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-068 AC-001 (b), AC-002, AC-004 for the installation, over HTTP: the anonymous pages, the error page for each
/// status and a signed-in page of the Admin and the Dean link the library and then <c>site.css</c> in their
/// <c>&lt;head&gt;</c>; both stylesheets, the licence, the version note and every override image are served with
/// <c>200</c> and their content type (spec FR-001.5, FR-002, FR-007).
/// </summary>
public sealed class InstallationStylesheetLinkTests(PostgreSqlFixture database)
{
    private static readonly string[] Expected = [Styling.BootstrapPath, Styling.SiteCssPath];

    /// <summary>AC-001 (b): the anonymous Dean sign-in page and a signed-in page for each role.</summary>
    [Theory]
    [InlineData(Actor.Anonymous, SignInTestData.SignInPath)]
    [InlineData(Actor.Admin, "/")]
    [InlineData(Actor.Dean, "/")]
    public async Task APage_LinksTheLibraryThenTheSiteStylesheet(Actor actor, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(path, ct);

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
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(SignInTestData.ErrorPath(status), ct);

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
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicAsync("GET", path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.StartsWith(contentType, response.ContentType ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-004, FR-007: every image <c>site.css</c> references is served as <c>image/svg+xml</c>; the installation needs at least one.</summary>
    [Fact]
    public async Task EveryOverrideImage_IsServedAsSvg()
    {
        var ct = TestContext.Current.CancellationToken;
        var images = Styling.RootRelativeUrls(Styling.ReadRequired(Styling.FileOf(Styling.Installation, Styling.SiteCssPath)));
        Assert.NotEmpty(images);
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var problems = new List<string>();
        foreach (var path in images)
        {
            var response = await host.SendPublicAsync("GET", path, ct);
            if (response.Status != HttpStatusCode.OK
                || !(response.ContentType ?? string.Empty).StartsWith("image/svg+xml", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"GET {path} → {(int)response.Status} {response.ContentType ?? "(no type)"}");
            }
        }

        Assert.Empty(problems);
    }
}
