using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-008 AC-002: the installation's public port denies by default, its anonymous endpoints are exactly the
/// SC-4 closed list, and the private port gains nothing from this Story (spec FR-002, FR-004; S-01; API-9;
/// TC-5).
/// </summary>
public sealed class PublicPortAuthorizationTests(PostgreSqlFixture database)
{
    private static readonly string[] FormMethods = ["GET", "HEAD", "POST"];

    [Fact]
    public async Task TheHost_SetsAFallbackPolicyRequiringAnAuthenticatedUser()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var fallback = await host.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(
            fallback.Requirements,
            r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    /// <summary>AC-002, S-01: an anonymous endpoint outside the SC-4 list fails this test.</summary>
    [Fact]
    public async Task OnlySc4EndpointsAllowAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services);
        var anonymous = endpoints.Where(e => e.AllowsAnonymous).ToList();

        Assert.Contains(anonymous, e => e.Pattern == "sign-in");
        Assert.Contains(anonymous, e => e.Pattern == "sign-in/google");
        Assert.Contains(anonymous, e => e.Pattern == "signin-google");
        Assert.Contains(anonymous, e => e.Pattern == "error/{statuscode}");
        Assert.Contains(anonymous, e => e.IsFallback);

        var notOnTheList = anonymous.Where(e => !IsSc4Entry(e)).Select(e => e.ToString()).ToList();
        Assert.Empty(notOnTheList);
    }

    /// <summary>AC-002: the landing page and sign-out are not anonymous.</summary>
    [Fact]
    public async Task TheLandingPageAndSignOut_RequireAnAuthenticatedUser()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services);

        Assert.Contains(endpoints, e => e.Pattern == string.Empty && !e.IsFallback && !e.AllowsAnonymous);
        Assert.Contains(endpoints, e => e.Pattern == "sign-out" && !e.AllowsAnonymous);
    }

    /// <summary>AC-002: an anonymous request to the landing page is challenged, with no return-URL (api-design 2.6).</summary>
    [Fact]
    public async Task AnonymousLandingPage_RedirectsToSignIn_WithNoReturnUrl()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
    }

    /// <summary>AC-002, TC-5: an unmatched address answers 404 to anyone — not a sign-in redirect.</summary>
    [Theory]
    [InlineData("/courses")]
    [InlineData("/admin")]
    [InlineData("/api/v1/sync")]
    public async Task AnonymousUnknownAddress_Returns404_NotASignInRedirect(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Null(response.Location);
    }

    /// <summary>AC-002, DC-6, SC-9: the private port keeps exactly what US-005 and US-006 gave it.</summary>
    [Fact]
    public async Task ThePrivatePort_IsUnchangedByThisStory()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var live = await host.SendPrivateAsync("GET", "/health/live", ct);
        var ready = await host.SendPrivateAsync("GET", "/health/ready", ct);
        var signIn = await host.SendPrivateAsync("GET", SignInTestData.SignInPath, ct);
        var landing = await host.SendPrivateAsync("GET", SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, live.Status);
        Assert.True(ready.Status is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable);
        Assert.Equal(HttpStatusCode.NotFound, signIn.Status);
        Assert.Equal(HttpStatusCode.NotFound, landing.Status);
        Assert.False(live.Headers.ContainsKey("Set-Cookie"));
        Assert.False(ready.Headers.ContainsKey("Set-Cookie"));
    }

    /// <summary>AC-002, DC-6: the private endpoints stay invisible on the public port.</summary>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/service/v1/status-pushes")]
    public async Task ThePrivateEndpoints_AnswerNothingOnThePublicPort(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }

    /// <summary>SC-4: the SC-4 entries of the installation's public port, and nothing else.</summary>
    private static bool IsSc4Entry(HostEndpoint endpoint) =>
        endpoint.IsFallback
        || endpoint.IsStaticFile
        || endpoint.Pattern == "error/{statuscode}"
        || (endpoint.Pattern == "sign-in"
            && (endpoint.Methods is null || endpoint.Methods.All(m => FormMethods.Contains(m, StringComparer.OrdinalIgnoreCase))))
        || (endpoint.Pattern == "sign-in/google"
            && endpoint.Methods is { Count: 1 } startMethods
            && string.Equals(startMethods[0], "POST", StringComparison.OrdinalIgnoreCase))
        || (endpoint.Pattern == "signin-google"
            && endpoint.Methods is not null
            && endpoint.Methods.All(m => m is "GET" or "HEAD"))

        // US-005 and US-006, on the private port and filtered to it.
        || endpoint.Pattern is "health/live" or "health/ready" or "service/v1/status-pushes";
}
