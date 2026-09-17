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
    [Fact]
    public async Task RoutedEndpoints_AreOnlyLivenessAndReadiness_GetAndAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new HostEndpoint(e))
            .Where(e => !e.IsFallback)
            .ToList();

        Assert.Equal(new[] { "health/live", "health/ready" }, endpoints.Select(e => e.Pattern).Distinct().Order(StringComparer.Ordinal));
        Assert.All(endpoints, e =>
        {
            Assert.True(e.AllowsAnonymous, e.ToString());
            Assert.NotNull(e.Methods);
            Assert.All(e.Methods!, m => Assert.Contains(m, new[] { "GET", "HEAD" }));
        });
    }

    [Theory]
    [InlineData("GET", "/")]
    [InlineData("GET", "/courses")]
    [InlineData("GET", "/api/v1/sync")]
    [InlineData("POST", "/")]
    [InlineData("POST", "/service/v1/legitimacy-checks")]
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
