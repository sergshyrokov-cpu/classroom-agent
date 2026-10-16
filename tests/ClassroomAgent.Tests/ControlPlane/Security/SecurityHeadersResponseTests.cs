using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>
/// US-050 AC-002, AC-003 for the Control Plane: every response — with the Owner account existing and before it
/// exists — carries the four browser security headers, each exactly once, and a CSP whose <c>form-action</c> is
/// <c>'self'</c> only (spec FR-001 … FR-005; VR-001, VR-002, VR-004; SC-15).
/// </summary>
public sealed class SecurityHeadersResponseTests(PostgreSqlFixture database)
{
    private const string Csp = SecurityHeaders.ControlPlaneCsp;

    public enum Caller
    {
        Owner,

        /// <summary>No session, the Owner account exists.</summary>
        Anonymous,

        /// <summary>No Owner account yet: the setup gate answers (US-001 FR-001).</summary>
        BeforeSetup,
    }

    /// <summary>AC-002, VR-004: every routed endpoint, every method, for each kind of caller.</summary>
    [Theory]
    [InlineData(Caller.Owner)]
    [InlineData(Caller.Anonymous)]
    [InlineData(Caller.BeforeSetup)]
    public async Task EveryEndpoint_CarriesTheSecurityHeaders(Caller caller)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = await ClientAsync(host, caller, ct);
        var endpoints = HostEndpoint.All(host.Services);
        Assert.Contains(endpoints, e => e.Pattern == "sign-in");
        Assert.Contains(endpoints, e => e.Pattern == "service/v1/legitimacy-checks");

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

    /// <summary>AC-002: signed-in pages, the sign-in page and the setup page.</summary>
    [Theory]
    [InlineData(Caller.Owner, "/")]
    [InlineData(Caller.Owner, "/installations")]
    [InlineData(Caller.Anonymous, "/sign-in")]
    [InlineData(Caller.BeforeSetup, "/setup")]
    public async Task APage_Returns200_WithTheSecurityHeaders(Caller caller, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = await ClientAsync(host, caller, ct);

        var response = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        SecurityHeaders.Assert(response, Csp, $"{caller} GET {path}");
    }

    /// <summary>AC-002, FR-005, VR-002, spec I-4: a page with an antiforgery token carries <c>DENY</c> only.</summary>
    [Fact]
    public async Task APageWithAnAntiforgeryToken_CarriesOnlyXFrameOptionsDeny()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = await ClientAsync(host, Caller.Anonymous, ct);

        var response = await anonymous.GetAsync("/sign-in", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains("__RequestVerificationToken", response.Body, StringComparison.Ordinal);
        Assert.Equal(["DENY"], SecurityHeaders.ValuesOf(SecurityHeaders.Of(response), SecurityHeaders.XFrameOptions));
    }

    /// <summary>AC-002: the redirect to sign-in and the setup gate's redirect.</summary>
    [Theory]
    [InlineData(Caller.Anonymous, "/sign-in")]
    [InlineData(Caller.BeforeSetup, "/setup")]
    public async Task ARedirect_CarriesTheSecurityHeaders(Caller caller, string location)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = await ClientAsync(host, caller, ct);

        var response = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(location, response.LocationPath);
        SecurityHeaders.Assert(response, Csp, $"{caller} GET /");
    }

    /// <summary>AC-002: an address nothing serves, and the same under <c>/api/v1</c>.</summary>
    [Theory]
    [InlineData("/no-such-page")]
    [InlineData("/api/v1/installations")]
    public async Task A404_CarriesTheSecurityHeaders(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        SecurityHeaders.Assert(response, Csp, $"GET {path}");
    }

    /// <summary>AC-002: sign-out posted without its antiforgery token.</summary>
    [Fact]
    public async Task AnAntiforgeryRefusal_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.PostFormAsync("/sign-out", [], ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        SecurityHeaders.Assert(response, Csp, "POST /sign-out without a token");
    }

    /// <summary>AC-002: the exception handler's <c>500</c> page; the table the list reads is dropped under the running host.</summary>
    [Fact]
    public async Task AnExceptionHandler500_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.ExecuteAsync("DROP TABLE installation CASCADE", ct);

        var response = await owner.GetAsync("/installations", ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.Status);
        SecurityHeaders.Assert(response, Csp, "GET /installations with its table gone");
    }

    /// <summary>AC-002, FR-002, spec I-3: every file of <c>wwwroot</c> carries the headers.</summary>
    [Fact]
    public async Task EveryStaticFile_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var missing = new List<string>();
        foreach (var path in NoStore.StaticFilesOf("ClassroomAgent.ControlPlane"))
        {
            var response = await anonymous.GetAsync(path, ct);
            Assert.Equal(HttpStatusCode.OK, response.Status);
            var problems = SecurityHeaders.Problems(SecurityHeaders.Of(response), Csp);
            if (problems.Count > 0)
            {
                missing.Add($"GET {path}: {string.Join("; ", problems)}");
            }
        }

        Assert.Empty(missing);
    }

    /// <summary>AC-002: a missing file under a static path is the ordinary 404.</summary>
    [Fact]
    public async Task AMissingFileUnderAStaticPath_CarriesTheSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync("/css/no-such-file.css", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        SecurityHeaders.Assert(response, Csp, "GET /css/no-such-file.css");
    }

    /// <summary>
    /// AC-002, AC-003, FR-004, VR-001: the policy the Control Plane sends is strict and names no origin at all —
    /// its <c>form-action</c> is <c>'self'</c> only.
    /// </summary>
    [Fact]
    public async Task TheSentPolicy_IsStrict_AndAllowsNoFormTargetOutsideTheHost()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.GetAsync("/setup", ct);

        var csp = Assert.Single(SecurityHeaders.ValuesOf(SecurityHeaders.Of(response), SecurityHeaders.ContentSecurityPolicy));
        Assert.Empty(SecurityHeaders.DuplicateDirectives(csp));
        var directives = SecurityHeaders.Directives(csp);
        foreach (var name in new[] { "default-src", "script-src", "style-src", "img-src", "font-src", "base-uri", "form-action" })
        {
            Assert.True(directives.TryGetValue(name, out var sources), $"'{name}' is missing from '{csp}'.");
            Assert.Equal(["'self'"], sources);
        }

        Assert.Equal(["'none'"], directives["object-src"]);
        Assert.Equal(["'none'"], directives["frame-ancestors"]);
        Assert.DoesNotContain("'unsafe-inline'", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("'unsafe-eval'", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("://", csp, StringComparison.Ordinal);
    }

    private static async Task<FormClient> ClientAsync(ControlPlaneTestHost host, Caller caller, CancellationToken ct)
    {
        switch (caller)
        {
            case Caller.Owner:
                return await host.CreateOwnerAsync(ct);
            case Caller.Anonymous:
                using (await host.CreateOwnerAsync(ct))
                {
                    return host.CreateClient();
                }

            case Caller.BeforeSetup:
                return host.CreateClient();
            default:
                throw new ArgumentOutOfRangeException(nameof(caller), caller, null);
        }
    }
}
