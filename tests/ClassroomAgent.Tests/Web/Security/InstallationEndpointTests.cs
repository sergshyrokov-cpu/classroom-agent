using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-005 AC-014, S-02 as US-008 leaves them: the private endpoints are still liveness, readiness and the push
/// receiver, all anonymous and filtered to the private port (DC-6). The rule "the public port answers <c>404</c> to
/// everything" is <b>replaced</b> by US-008, which US-005 wrote it expecting (US-008 api-design §7); the public
/// surface is covered by <c>Web/Security/PublicPortAuthorizationTests</c>.
/// </summary>
public sealed class InstallationEndpointTests(PostgreSqlFixture database)
{
    /// <summary>
    /// US-006 AC-013: the private endpoints are liveness, readiness and the push receiver, all anonymous. US-008 adds
    /// the public surface beside them, so this asserts the private three are present and unchanged rather than that
    /// they are the only endpoints of the host.
    /// </summary>
    [Fact]
    public async Task ThePrivateEndpoints_AreLivenessReadinessAndThePushReceiver_AllAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var privateEndpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new HostEndpoint(e))
            .Where(e => e.Pattern is "health/live" or "health/ready" or "service/v1/status-pushes")
            .ToList();

        Assert.Equal(
            new[] { "health/live", "health/ready", "service/v1/status-pushes" },
            privateEndpoints.Select(e => e.Pattern).Distinct().Order(StringComparer.Ordinal));
        Assert.All(privateEndpoints, e =>
        {
            Assert.True(e.AllowsAnonymous, e.ToString());
            Assert.NotNull(e.Methods);
        });
        Assert.All(
            privateEndpoints.Where(e => e.Pattern != "service/v1/status-pushes"),
            e => Assert.All(e.Methods!, m => Assert.Contains(m, new[] { "GET", "HEAD" })));
    }

    /// <summary>
    /// US-006 AC-013, S-01: the receiver is the only antiforgery-exempt endpoint of the host, POST only and
    /// anonymous. US-008 adds state-changing public endpoints beside it, and every one of those carries the token
    /// (US-008 spec FR-005: the public-port exemption list is empty).
    /// </summary>
    [Fact]
    public async Task ThePushReceiver_IsTheOnlyExemptEndpoint_PostOnly_AndAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var exempt = HostEndpoint.All(host.Services)
            .Where(e => !e.IsFallback && e.UnsafeMethodsAccepted.Count > 0 && e.IsExemptFromAntiforgery)
            .ToList();

        var receiver = Assert.Single(exempt);
        Assert.Equal("service/v1/status-pushes", receiver.Pattern);
        Assert.Equal(["POST"], receiver.Methods);
        Assert.True(receiver.AllowsAnonymous, receiver.ToString());
    }

    /// <summary>
    /// US-008 replaces the US-005 rule that the public port answered <c>404</c> to everything. What survives is the
    /// part DC-6 fixes: a private path is invisible on the public port, and an address nothing serves answers
    /// <c>404</c> — never a sign-in redirect (SC-4 v66).
    /// </summary>
    [Theory]
    [InlineData("GET", "/courses")]
    [InlineData("GET", "/api/v1/sync")]
    [InlineData("POST", "/service/v1/legitimacy-checks")]
    [InlineData("POST", "/service/v1/status-pushes")]
    [InlineData("POST", "/status-changes")]
    [InlineData("PUT", "/anything")]
    [InlineData("DELETE", "/health/ready")]
    public async Task PublicPort_AnAddressNothingServes_Returns404_NoCookieNoRedirect(string method, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicAsync(method, path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.False(response.Headers.ContainsKey("Set-Cookie"));
        Assert.False(response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task PrivatePort_UnknownPath_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPrivateAsync("GET", "/courses", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }
}
