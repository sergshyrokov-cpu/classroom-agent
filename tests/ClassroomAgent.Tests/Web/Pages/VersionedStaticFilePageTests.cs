using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-077 AC-010, AC-011, spec FR-009, over HTTP: a rendered page refers to each of its host's own scripts and
/// stylesheets with <c>?v=</c> followed by the content hash of the file on disk, so changing the file changes the
/// address; the versioned address is still served under the pages' policy, and a form keeps one anti-forgery field.
/// </summary>
public sealed partial class VersionedStaticFilePageTests(PostgreSqlFixture database)
{
    /// <summary>AC-010, FR-009: the anonymous sign-in page and a signed-in page of the Admin and the Dean.</summary>
    [Theory]
    [InlineData(Actor.Anonymous, SignInTestData.SignInPath)]
    [InlineData(Actor.Admin, "/")]
    [InlineData(Actor.Dean, "/")]
    public async Task AnInstallationPage_RefersToItsOwnFilesByContentVersion(Actor actor, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertVersioned(Styling.Installation, page);
    }

    /// <summary>AC-010, FR-009: the installation error page for 400, 403, 404 and 500.</summary>
    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task TheInstallationErrorPage_RefersToItsOwnFilesByContentVersion(int status)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(SignInTestData.ErrorPath(status), ct);

        Assert.Equal(status, (int)page.Status);
        AssertVersioned(Styling.Installation, page);
    }

    /// <summary>AC-010, FR-009: a page that includes a script of its own refers to it by content version too.</summary>
    [Fact]
    public async Task ThePageWithAScript_RefersToItByContentVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.GetAsync(ConnectionInstructionTestData.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertVersioned(Styling.Installation, page);
        Assert.Contains(Own(page.Body), a => a.Contains("copy-instruction.js", StringComparison.Ordinal));
    }

    /// <summary>AC-010, FR-009: the Control Plane sign-in page, the Owner account existing.</summary>
    [Fact]
    public async Task AControlPlaneSignInPage_RefersToItsOwnFilesByContentVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        (await host.CreateOwnerAsync(ct)).Dispose();
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync("/sign-in", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertVersioned(Styling.ControlPlane, page);
    }

    /// <summary>AC-010, FR-009: the Control Plane first-run setup page, before the Owner account exists.</summary>
    [Fact]
    public async Task AControlPlaneSetupPage_RefersToItsOwnFilesByContentVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync("/setup", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertVersioned(Styling.ControlPlane, page);
    }

    /// <summary>AC-010, FR-009: the Control Plane error page for 404.</summary>
    [Fact]
    public async Task AControlPlaneErrorPage_RefersToItsOwnFilesByContentVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync("/error/404", ct);

        Assert.Equal(404, (int)page.Status);
        AssertVersioned(Styling.ControlPlane, page);
    }

    /// <summary>AC-010, FR-009: a versioned address is served with its content type, under the pages' Content-Security-Policy.</summary>
    [Fact]
    public async Task AVersionedAddress_IsServedUnderThePagesPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(SignInTestData.SignInPath, ct);
        var addresses = Own(page.Body);
        Assert.NotEmpty(addresses);

        var problems = new List<string>();
        foreach (var address in addresses)
        {
            // Through the client, so the query string reaches the host as a query string, as from a browser.
            var response = await client.GetAsync(address, ct);
            var type = response.Headers.FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
            var path = address.Split('?')[0];
            var expected = path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? "text/css" : "javascript";
            if (response.Status != HttpStatusCode.OK || !type.Contains(expected, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"GET {address} → {(int)response.Status} {type}");
            }
        }

        Assert.Empty(problems);
        Assert.Empty(SecurityHeaders.Problems(SecurityHeaders.Of(page), SecurityHeaders.InstallationCsp));
    }

    /// <summary>AC-011, I-7: a post form still carries exactly one anti-forgery field; the tag helpers add none of their own.</summary>
    [Fact]
    public async Task AForm_StillCarriesOneAntiforgeryField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        var counts = new List<int>();
        foreach (Match form in FormBlock().Matches(page.Body))
        {
            var open = FormOpenTag().Match(form.Value).Value;
            if (!string.Equals(Styling.Attribute(open, "method"), "post", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            counts.Add(Regex.Matches(form.Value, "__RequestVerificationToken", RegexOptions.None, TimeSpan.FromSeconds(1)).Count);
        }

        Assert.All(counts, c => Assert.True(c <= 1, $"A form carries {c} anti-forgery fields."));
        Assert.Contains(1, counts);
    }

    /// <summary>The root-relative address of every script and stylesheet the page refers to.</summary>
    private static List<string> Own(string html)
    {
        var addresses = new List<string>();
        foreach (Match tag in ScriptOrLinkTag().Matches(html))
        {
            var isScript = tag.Value.StartsWith("<script", StringComparison.OrdinalIgnoreCase);
            string? address;
            if (isScript)
            {
                address = Styling.Attribute(tag.Value, "src");
            }
            else
            {
                var rel = Styling.Attribute(tag.Value, "rel") ?? string.Empty;
                address = rel.Contains("stylesheet", StringComparison.OrdinalIgnoreCase) ? Styling.Attribute(tag.Value, "href") : null;
            }

            if (address is not null && address.StartsWith('/') && !address.StartsWith("//", StringComparison.Ordinal))
            {
                addresses.Add(address);
            }
        }

        return addresses;
    }

    private static void AssertVersioned(string project, PageResponse page)
    {
        var addresses = Own(page.Body);
        Assert.NotEmpty(addresses);

        foreach (var address in addresses)
        {
            var parts = address.Split('?', 2);
            var file = Styling.FileOf(project, parts[0]);
            Assert.True(File.Exists(file), $"No file on disk behind {address}: {file}");

            var expected = "v=" + WebEncoders.Base64UrlEncode(SHA256.HashData(File.ReadAllBytes(file)));
            Assert.True(parts.Length == 2, $"{address} carries no version query.");
            Assert.Equal(expected, parts[1]);
        }

        Assert.DoesNotContain("asp-append-version", page.Body, StringComparison.Ordinal);
        Assert.Empty(InlineCode.Violations(page.Body));
    }

    [GeneratedRegex(@"<(?:script|link)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrLinkTag();

    [GeneratedRegex(@"<form\b[^>]*>.*?</form\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex FormBlock();

    [GeneratedRegex(@"<form\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex FormOpenTag();
}
