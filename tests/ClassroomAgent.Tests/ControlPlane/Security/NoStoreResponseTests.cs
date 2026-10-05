using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>
/// US-040 AC-002, AC-003, AC-004 for the Control Plane: every response carries <c>Cache-Control: no-store</c>, a
/// served static file does not (spec FR-002, FR-003, FR-004; VR-001 … VR-004; SC-14).
/// </summary>
public sealed class NoStoreResponseTests(PostgreSqlFixture database)
{
    public enum Caller
    {
        Owner,

        /// <summary>No session, the Owner account exists.</summary>
        Anonymous,

        /// <summary>No Owner account yet: the setup gate answers (US-001 FR-001).</summary>
        BeforeSetup,
    }

    /// <summary>AC-002, AC-004, VR-004: every routed endpoint, every method, for each kind of caller.</summary>
    [Theory]
    [InlineData(Caller.Owner)]
    [InlineData(Caller.Anonymous)]
    [InlineData(Caller.BeforeSetup)]
    public async Task EveryEndpoint_CarriesNoStore(Caller caller)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = await ClientAsync(host, caller, ct);
        var endpoints = HostEndpoint.All(host.Services);
        Assert.Contains(endpoints, e => e.Pattern == "sign-in");
        Assert.Contains(endpoints, e => e.Pattern == "service/v1/legitimacy-checks");

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

    /// <summary>AC-002, VR-002: a signed-in page and an anonymous page.</summary>
    [Theory]
    [InlineData(Caller.Owner, "/")]
    [InlineData(Caller.Owner, "/installations")]
    [InlineData(Caller.Anonymous, "/sign-in")]
    [InlineData(Caller.BeforeSetup, "/setup")]
    public async Task APage_Returns200_WithNoStore(Caller caller, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = await ClientAsync(host, caller, ct);

        var response = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        NoStore.Assert(response, $"{caller} GET {path}");
    }

    /// <summary>AC-002, VR-002: the redirect to sign-in and the setup gate's redirect.</summary>
    [Theory]
    [InlineData(Caller.Anonymous, "/sign-in")]
    [InlineData(Caller.BeforeSetup, "/setup")]
    public async Task ARedirect_CarriesNoStore(Caller caller, string location)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = await ClientAsync(host, caller, ct);

        var response = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(location, response.LocationPath);
        NoStore.Assert(response, $"{caller} GET /");
    }

    /// <summary>AC-002, VR-002: an address nothing serves, and the same under <c>/api/v1</c>.</summary>
    [Theory]
    [InlineData("/no-such-page")]
    [InlineData("/api/v1/installations")]
    public async Task A404_CarriesNoStore(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        NoStore.Assert(response, $"GET {path}");
    }

    /// <summary>AC-002, VR-002: sign-out posted without its antiforgery token.</summary>
    [Fact]
    public async Task AnAntiforgeryRefusal_CarriesNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.PostFormAsync("/sign-out", [], ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        NoStore.Assert(response, "POST /sign-out without a token");
    }

    /// <summary>
    /// AC-002, VR-002: an unexpected failure rendered by the exception handler as the <c>500</c> error page. The
    /// failure is real: the table the list reads is dropped under the running host.
    /// </summary>
    [Fact]
    public async Task AnExceptionHandler500_CarriesNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.ExecuteAsync("DROP TABLE installation CASCADE", ct);

        var response = await owner.GetAsync("/installations", ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.Status);
        NoStore.Assert(response, "GET /installations with its table gone");
    }

    /// <summary>AC-003, VR-003: every file of <c>wwwroot</c> is served without <c>no-store</c>.</summary>
    [Fact]
    public async Task AStaticFile_DoesNotCarryNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        foreach (var path in NoStore.StaticFilesOf("ClassroomAgent.ControlPlane"))
        {
            var response = await anonymous.GetAsync(path, ct);

            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.False(NoStore.IsIn(NoStore.Of(response)), $"GET {path}: Cache-Control is '{NoStore.Of(response)}'.");
        }
    }

    /// <summary>AC-003, VR-003, api-design D-4: a missing file under a static path is an ordinary 404.</summary>
    [Fact]
    public async Task AMissingFileUnderAStaticPath_CarriesNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync("/css/no-such-file.css", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        NoStore.Assert(response, "GET /css/no-such-file.css");
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
