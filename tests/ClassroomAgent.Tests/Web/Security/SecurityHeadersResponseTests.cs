using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-050 AC-001, AC-003, AC-004 for the installation: every response of the public and the private port carries
/// <c>X-Frame-Options: DENY</c>, <c>X-Content-Type-Options: nosniff</c>, <c>Referrer-Policy: same-origin</c> and the
/// installation <c>Content-Security-Policy</c>, each exactly once (spec FR-001 … FR-005; VR-001, VR-002, VR-004;
/// SC-15).
/// </summary>
public sealed class SecurityHeadersResponseTests(PostgreSqlFixture database)
{
    private const string Csp = SecurityHeaders.InstallationCsp;

    /// <summary>AC-001, VR-004: every routed endpoint, every method, each kind of caller, on the public port.</summary>
    [Theory]
    [InlineData(Actor.Admin)]
    [InlineData(Actor.Dean)]
    [InlineData(Actor.Anonymous)]
    public async Task EveryEndpoint_OnThePublicPort_CarriesTheSecurityHeaders(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        using var _client = client;
        var endpoints = HostEndpoint.All(host.Services);
        Assert.Contains(endpoints, e => e.Pattern == "sign-in");
        Assert.Contains(endpoints, e => e.Pattern == "workspace/journal");

        var missing = await SecurityHeaders.MissingAsync(endpoints, Csp, async (method, path) =>
        {
            var response = await client.SendAsync(
                new HttpMethod(method),
                path,
                method == "GET" ? null : new FormUrlEncodedContent([]),
                ct);
            return (response.Status, SecurityHeaders.Of(response));
        });

        Assert.Empty(missing);
    }

    /// <summary>AC-001, FR-002, VR-004: the private port — health checks, the push receiver and the 404 of everything else.</summary>
    [Fact]
    public async Task EveryEndpoint_OnThePrivatePort_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var endpoints = HostEndpoint.All(host.Services);
        Assert.Contains(endpoints, e => e.Pattern == "health/live");
        Assert.Contains(endpoints, e => e.Pattern == "service/v1/status-pushes");

        var missing = await SecurityHeaders.MissingAsync(endpoints, Csp, async (method, path) =>
        {
            var response = await host.SendPrivateAsync(method, path, ct);
            return (response.Status, SecurityHeaders.Of(response));
        });

        Assert.Empty(missing);
    }

    /// <summary>AC-001, FR-002: an address nothing serves on the private port.</summary>
    [Fact]
    public async Task AnUnknownAddressOnThePrivatePort_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPrivateAsync("GET", "/no-such-page", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        SecurityHeaders.Assert(response, Csp, "private GET /no-such-page");
    }

    /// <summary>AC-001: a signed-in page of each role and the anonymous sign-in page.</summary>
    [Theory]
    [InlineData(Actor.Admin, "/")]
    [InlineData(Actor.Dean, "/")]
    [InlineData(Actor.Anonymous, SignInTestData.SignInPath)]
    public async Task APage_Returns200_WithTheSecurityHeaders(Actor actor, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        using var _client = client;

        var response = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        SecurityHeaders.Assert(response, Csp, $"{actor} GET {path}");
    }

    /// <summary>
    /// AC-001, FR-005, VR-002, spec I-4: a page that generates an antiforgery token — which today adds
    /// <c>X-Frame-Options: SAMEORIGIN</c> — carries <c>DENY</c> and nothing beside it.
    /// </summary>
    [Fact]
    public async Task APageWithAnAntiforgeryToken_CarriesOnlyXFrameOptionsDeny()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains("__RequestVerificationToken", response.Body, StringComparison.Ordinal);
        Assert.Equal(["DENY"], SecurityHeaders.ValuesOf(SecurityHeaders.Of(response), SecurityHeaders.XFrameOptions));
    }

    /// <summary>AC-001: the journal with students' data.</summary>
    [Fact]
    public async Task TheJournal_Returns200_WithTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        using var _client = client;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        SecurityHeaders.Assert(response, Csp, "GET journal");
    }

    /// <summary>AC-001, FR-002: a downloaded file — the journal export, an <c>/api/v1</c> response.</summary>
    [Fact]
    public async Task TheJournalExport_DownloadedFile_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _client = client;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.NotNull(response.Header("Content-Disposition"));
        SecurityHeaders.Assert(response, Csp, "POST journal export");
    }

    /// <summary>AC-001: an <c>/api/v1</c> refusal with an API-6 body — the export posted without its token.</summary>
    [Fact]
    public async Task AnApiRefusal_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _client = client;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        SecurityHeaders.Assert(response, Csp, "POST journal export without a token");
    }

    /// <summary>AC-001: the redirect of an anonymous request to sign-in.</summary>
    [Fact]
    public async Task TheRedirectToSignIn_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        using var _client = client;

        var response = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        SecurityHeaders.Assert(response, Csp, "anonymous GET /");
    }

    /// <summary>AC-001, FR-002: the HTTPS redirection of the public port, written before any page runs.</summary>
    [Fact]
    public async Task TheHttpsRedirection_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicHttpAsync("GET", "/", ct);

        Assert.True(
            response.Status is HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect or HttpStatusCode.Redirect,
            $"Expected the HTTPS redirection, got {(int)response.Status}.");
        SecurityHeaders.Assert(response, Csp, "plain-HTTP GET /");
    }

    /// <summary>AC-001: a Dean refused an Admin-only screen.</summary>
    [Fact]
    public async Task A403Refusal_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        using var _client = client;

        var response = await client.GetAsync(DeanAccountTestData.Paths.Deans, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        SecurityHeaders.Assert(response, Csp, "Dean GET /settings/deans");
    }

    /// <summary>AC-001: an unknown address — the re-executed error page — and the same under <c>/api/v1</c>.</summary>
    [Theory]
    [InlineData("/no-such-page")]
    [InlineData("/api/v1/sync")]
    public async Task A404_CarriesTheSecurityHeaders(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicAsync("GET", path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        SecurityHeaders.Assert(response, Csp, $"GET {path}");
    }

    /// <summary>AC-001: a form posted without its antiforgery token.</summary>
    [Fact]
    public async Task AnAntiforgeryRefusal_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _client = client;
        await client.OpenDeanAccountsAsync(ct);

        var response = await client.PostFormAsync(DeanAccountTestData.Paths.Deans, [], ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        SecurityHeaders.Assert(response, Csp, "POST /settings/deans without a token");
    }

    /// <summary>AC-001: a write refused in read-only mode (<c>409</c> through the host's exception handler).</summary>
    [Fact]
    public async Task AReadOnlyRefusal_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;
        using var _client = client;

        var response = await client.CreateDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        SecurityHeaders.Assert(response, Csp, "create a Dean in read-only mode");
    }

    /// <summary>
    /// AC-001, FR-002, spec §8: the exception handler's <c>500</c> page, which clears the response before writing it.
    /// The failure is real: the table the journal reads is dropped under the running host.
    /// </summary>
    [Fact]
    public async Task AnExceptionHandler500_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _client = client;
        var seeded = await host.SeedJournalAsync(ct);
        await host.ExecuteAsync("DROP TABLE course CASCADE", ct);

        var response = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.Status);
        SecurityHeaders.Assert(response, Csp, "GET journal with its table gone");
    }

    /// <summary>AC-001, FR-002, spec I-3: every file of <c>wwwroot</c> — unlike <c>no-store</c>, static files are not exempt.</summary>
    [Fact]
    public async Task EveryStaticFile_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var missing = new List<string>();
        foreach (var path in NoStore.StaticFilesOf("ClassroomAgent.Web"))
        {
            var response = await host.SendPublicAsync("GET", path, ct);
            Assert.Equal(HttpStatusCode.OK, response.Status);
            var problems = SecurityHeaders.Problems(SecurityHeaders.Of(response), Csp);
            if (problems.Count > 0)
            {
                missing.Add($"GET {path}: {string.Join("; ", problems)}");
            }
        }

        Assert.Empty(missing);
    }

    /// <summary>AC-001: a missing file under a static path is the ordinary 404.</summary>
    [Fact]
    public async Task AMissingFileUnderAStaticPath_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicAsync("GET", "/css/no-such-file.css", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        SecurityHeaders.Assert(response, Csp, "GET /css/no-such-file.css");
    }

    /// <summary>
    /// AC-003, FR-004, VR-001: the policy the host actually sends is strict — scripts, styles, images and fonts from
    /// <c>'self'</c> only, no <c>'unsafe-inline'</c> or <c>'unsafe-eval'</c>, <c>object-src 'none'</c>,
    /// <c>base-uri 'self'</c>, framing refused, and no origin but Google's sign-in origin in <c>form-action</c>.
    /// </summary>
    [Fact]
    public async Task TheSentPolicy_IsStrict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync(SignInTestData.SignInPath, ct);

        var csp = Assert.Single(SecurityHeaders.ValuesOf(SecurityHeaders.Of(response), SecurityHeaders.ContentSecurityPolicy));
        Assert.Empty(SecurityHeaders.DuplicateDirectives(csp));
        var directives = SecurityHeaders.Directives(csp);
        foreach (var name in new[] { "default-src", "script-src", "style-src", "img-src", "font-src", "base-uri" })
        {
            Assert.True(directives.TryGetValue(name, out var sources), $"'{name}' is missing from '{csp}'.");
            Assert.Equal(["'self'"], sources);
        }

        Assert.Equal(["'none'"], directives["object-src"]);
        Assert.Equal(["'none'"], directives["frame-ancestors"]);
        Assert.Equal(["'self'", SecurityHeaders.GoogleSignInOrigin], directives["form-action"]);
        Assert.DoesNotContain("'unsafe-inline'", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("'unsafe-eval'", csp, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC-004 (automated part), FR-004, spec I-2: the sign-in start — the response the browser follows after posting
    /// the "Sign in with Google" form — carries the policy, and the origin it redirects to is exactly the origin the
    /// policy's <c>form-action</c> lists beside <c>'self'</c>. The Google handler keeps its real configuration here
    /// (no stub): the redirect is built locally, nothing is sent to Google.
    /// </summary>
    [Fact]
    public async Task TheGoogleSignInStart_RedirectsToTheOriginTheFormActionAllows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.StartGoogleSignInAsync(ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.True(Uri.TryCreate(response.Location, UriKind.Absolute, out var location), $"Location '{response.Location}' is not absolute.");
        var redirectOrigin = location.GetLeftPart(UriPartial.Authority);
        SecurityHeaders.Assert(response, Csp, "POST /sign-in/google");
        var csp = SecurityHeaders.ValuesOf(SecurityHeaders.Of(response), SecurityHeaders.ContentSecurityPolicy).Single();
        var formAction = SecurityHeaders.Directives(csp)["form-action"];
        Assert.Equal(["'self'", redirectOrigin], formAction);
    }

    /// <summary>
    /// AC-001, AC-004, FR-002: the return address from Google is answered by the authentication handler, not by a
    /// routed endpoint, so the enumeration does not reach it. Without a valid <c>state</c> the handler refuses before
    /// any exchange with Google.
    /// </summary>
    [Fact]
    public async Task TheGoogleCallbackAddress_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync(SignInTestData.CallbackPath, ct);

        SecurityHeaders.Assert(response, Csp, $"GET {SignInTestData.CallbackPath}");
    }
}
