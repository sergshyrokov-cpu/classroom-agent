using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-005 AC-014, S-02: the installation host maps only liveness and readiness in this Story, both GET
/// and anonymous as the SC-4 entry; the public port answers <c>404</c> with an empty body to everything,
/// sets no cookie and redirects nowhere (spec FR-013, FR-014, I-13; TC-5).
/// </summary>
public sealed class InstallationEndpointTests(PostgreSqlFixture database)
{
    /// <summary>US-006 AC-013 adds the push receiver: the third routed endpoint, POST, anonymous, on the private port.</summary>
    [Fact]
    public async Task RoutedEndpoints_AreLivenessReadinessAndThePushReceiver_AllAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new HostEndpoint(e))
            .Where(e => !e.IsFallback)
            .ToList();

        Assert.Equal(
            new[] { "health/live", "health/ready", "service/v1/status-pushes" },
            endpoints.Select(e => e.Pattern).Distinct().Order(StringComparer.Ordinal));
        Assert.All(endpoints, e =>
        {
            Assert.True(e.AllowsAnonymous, e.ToString());
            Assert.NotNull(e.Methods);
        });
        Assert.All(
            endpoints.Where(e => e.Pattern != "service/v1/status-pushes"),
            e => Assert.All(e.Methods!, m => Assert.Contains(m, new[] { "GET", "HEAD" })));
    }

    /// <summary>US-006 AC-013, S-01: the receiver is the only state-changing endpoint, and it is exempt from antiforgery.</summary>
    [Fact]
    public async Task ThePushReceiver_IsTheOnlyUnsafeEndpoint_PostOnly_AndExemptFromAntiforgery()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var unsafeEndpoints = HostEndpoint.All(host.Services)
            .Where(e => !e.IsFallback && e.UnsafeMethodsAccepted.Count > 0)
            .ToList();

        var receiver = Assert.Single(unsafeEndpoints);
        Assert.Equal("service/v1/status-pushes", receiver.Pattern);
        Assert.Equal(["POST"], receiver.Methods);
        Assert.True(receiver.IsExemptFromAntiforgery, receiver.ToString());
        Assert.True(receiver.AllowsAnonymous, receiver.ToString());
    }

    [Theory]
    [InlineData("GET", "/")]
    [InlineData("GET", "/courses")]
    [InlineData("GET", "/api/v1/sync")]
    [InlineData("POST", "/")]
    [InlineData("POST", "/service/v1/legitimacy-checks")]
    [InlineData("POST", "/service/v1/status-pushes")]
    [InlineData("POST", "/status-changes")]
    [InlineData("PUT", "/anything")]
    [InlineData("DELETE", "/health/ready")]
    public async Task PublicPort_AnyRequest_Returns404EmptyBody_NoCookieNoRedirect(string method, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicAsync(method, path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Equal(string.Empty, response.Body);
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
