using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-010: only the SC-4 entries of this Story are anonymous (FR-014, TC-5).</summary>
public sealed class AnonymousEndpointTests(PostgreSqlFixture database)
{
    private static readonly string[] FormMethods = ["GET", "HEAD", "POST"];

    [Fact]
    public async Task OnlySc4EndpointsAllowAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var fallbackPolicy = await host.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();
        var endpoints = HostEndpoint.All(host.Services);
        var anonymous = endpoints.Where(e => e.AllowsAnonymous).ToList();

        Assert.NotNull(fallbackPolicy);
        Assert.Contains(anonymous, e => e.Pattern == "setup");
        Assert.Contains(anonymous, e => e.Pattern == "sign-in");
        Assert.Contains(anonymous, e => e.Pattern == "error/{statuscode}");
        Assert.Contains(anonymous, e => e.IsFallback);
        Assert.Contains(endpoints, e => e.Pattern == string.Empty && !e.AllowsAnonymous);
        Assert.Contains(endpoints, e => e.Pattern == "sign-out" && !e.AllowsAnonymous);

        var notOnSc4List = anonymous.Where(e => !IsSc4Entry(e)).Select(e => e.ToString()).ToList();
        Assert.Empty(notOnSc4List);
    }

    [Fact]
    public async Task Home_AnonymousWithOwner_RedirectsToSignIn_NoReturnUrl()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/sign-in", response.LocationPath);
    }

    private static bool IsSc4Entry(HostEndpoint endpoint) =>
        endpoint.IsFallback
        || endpoint.IsStaticFile
        || endpoint.Pattern == "error/{statuscode}"
        || (endpoint.Pattern is "setup" or "sign-in"
            && (endpoint.Methods is null || endpoint.Methods.All(m => FormMethods.Contains(m, StringComparer.OrdinalIgnoreCase))));
}
