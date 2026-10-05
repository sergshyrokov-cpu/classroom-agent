using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-040 AC-001, AC-003, AC-004 for the installation: every response of the public and the private port carries
/// <c>Cache-Control: no-store</c>, a served static file does not (spec FR-001, FR-003, FR-004; VR-001 … VR-004;
/// SC-14).
/// </summary>
public sealed class NoStoreResponseTests(PostgreSqlFixture database)
{
    /// <summary>
    /// AC-001, AC-004, VR-004: every routed endpoint of the host, every method, answered to each kind of caller on the
    /// public port. A page added later without the header fails here.
    /// </summary>
    [Theory]
    [InlineData(Actor.Admin)]
    [InlineData(Actor.Dean)]
    [InlineData(Actor.Anonymous)]
    public async Task EveryEndpoint_OnThePublicPort_CarriesNoStore(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        using var _client = client;
        var endpoints = HostEndpoint.All(host.Services);
        Assert.Contains(endpoints, e => e.Pattern == "sign-in");
        Assert.Contains(endpoints, e => e.Pattern == "workspace/journal");

        var missing = await NoStore.MissingAsync(endpoints, async (method, path) =>
        {
            var response = await client.SendAsync(
                new HttpMethod(method),
                path,
                method == "GET" ? null : new FormUrlEncodedContent([]),
                ct);
            return (response.Status, NoStore.Of(response));
        });

        Assert.Empty(missing);
    }

    /// <summary>AC-001, VR-002, VR-004: the same enumeration on the private port — health checks, the push receiver and the 404 of everything else.</summary>
    [Fact]
    public async Task EveryEndpoint_OnThePrivatePort_CarriesNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var endpoints = HostEndpoint.All(host.Services);
        Assert.Contains(endpoints, e => e.Pattern == "health/live");
        Assert.Contains(endpoints, e => e.Pattern == "service/v1/status-pushes");

        var missing = await NoStore.MissingAsync(endpoints, async (method, path) =>
        {
            var response = await host.SendPrivateAsync(method, path, ct);
            return (response.Status, NoStore.Of(response));
        });

        Assert.Empty(missing);
    }

    /// <summary>AC-001, VR-002: a signed-in page of each role and an anonymous page.</summary>
    [Theory]
    [InlineData(Actor.Admin, "/")]
    [InlineData(Actor.Dean, "/")]
    [InlineData(Actor.Anonymous, SignInTestData.SignInPath)]
    public async Task APage_Returns200_WithNoStore(Actor actor, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        using var _client = client;

        var response = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        NoStore.Assert(response, $"{actor} GET {path}");
    }

    /// <summary>AC-001, VR-002: the journal with students' data — the page the rule exists for.</summary>
    [Fact]
    public async Task TheJournal_Returns200_WithNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        using var _client = client;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(SeededJournal.StudentName, response.Text, StringComparison.Ordinal);
        NoStore.Assert(response, "GET journal");
    }

    /// <summary>AC-001, VR-002: the redirect of an anonymous request to sign-in.</summary>
    [Fact]
    public async Task TheRedirectToSignIn_CarriesNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        using var _client = client;

        var response = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        NoStore.Assert(response, "anonymous GET /");
    }

    /// <summary>AC-001, VR-002: the HTTPS redirection of the public port, written before any page runs.</summary>
    [Fact]
    public async Task TheHttpsRedirection_CarriesNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicHttpAsync("GET", "/", ct);

        Assert.True(
            response.Status is HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect or HttpStatusCode.Redirect,
            $"Expected the HTTPS redirection, got {(int)response.Status}.");
        NoStore.Assert(response, "plain-HTTP GET /");
    }

    /// <summary>AC-001, VR-002: a Dean refused an Admin-only screen.</summary>
    [Fact]
    public async Task A403Refusal_CarriesNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        using var _client = client;

        var response = await client.GetAsync(DeanAccountTestData.Paths.Deans, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        NoStore.Assert(response, "Dean GET /settings/deans");
    }

    /// <summary>AC-001, VR-002: an address nothing serves, and the same under <c>/api/v1</c>.</summary>
    [Theory]
    [InlineData("/no-such-page")]
    [InlineData("/api/v1/sync")]
    public async Task A404_CarriesNoStore(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicAsync("GET", path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        NoStore.Assert(response, $"GET {path}");
    }

    /// <summary>AC-001, VR-002: a form posted without its antiforgery token.</summary>
    [Fact]
    public async Task AnAntiforgeryRefusal_CarriesNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _client = client;
        await client.OpenDeanAccountsAsync(ct);

        var response = await client.PostFormAsync(DeanAccountTestData.Paths.Deans, [], ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        NoStore.Assert(response, "POST /settings/deans without a token");
    }

    /// <summary>AC-001, VR-002: a write refused in read-only mode (<c>409</c> through the host's exception handler).</summary>
    [Fact]
    public async Task AReadOnlyRefusal_CarriesNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;
        using var _client = client;

        var response = await client.CreateDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        NoStore.Assert(response, "create a Dean in read-only mode");
    }

    /// <summary>
    /// AC-001, VR-002: an unexpected failure rendered by the exception handler as the <c>500</c> error page. The
    /// failure is real: the table the journal reads is dropped under the running host.
    /// </summary>
    [Fact]
    public async Task AnExceptionHandler500_CarriesNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _client = client;
        var seeded = await host.SeedJournalAsync(ct);
        await host.ExecuteAsync("DROP TABLE course CASCADE", ct);

        var response = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.Status);
        NoStore.Assert(response, "GET journal with its table gone");
    }

    /// <summary>AC-003, VR-003: every file of <c>wwwroot</c> is served without <c>no-store</c>.</summary>
    [Fact]
    public async Task AStaticFile_DoesNotCarryNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        foreach (var path in NoStore.StaticFilesOf("ClassroomAgent.Web"))
        {
            var response = await host.SendPublicAsync("GET", path, ct);

            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.False(NoStore.IsIn(NoStore.Of(response)), $"GET {path}: Cache-Control is '{NoStore.Of(response)}'.");
        }
    }

    /// <summary>
    /// AC-003, VR-003, api-design D-4: the exception is a served file, not a path — a missing file gets an ordinary
    /// response: the catch-all's 404, which matches file-like paths too (US-041 AC-001).
    /// </summary>
    [Fact]
    public async Task AMissingFileUnderAStaticPath_CarriesNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicAsync("GET", "/css/no-such-file.css", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        NoStore.Assert(response, "GET /css/no-such-file.css");
    }
}
