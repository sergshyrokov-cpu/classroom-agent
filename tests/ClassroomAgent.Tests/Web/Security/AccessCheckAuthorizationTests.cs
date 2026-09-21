using System.Net;
using System.Security.Claims;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-011 AC-001: "Проверить доступ" belongs to the Admin alone — the matrix row is ✔ Admin, ✘ Dean
/// (<c>trebovaniya.md</c> §2, v39; spec FR-011, S-01, S-02, S-10; api-design §4; TC-5). Its policy is its own
/// (spec I-9). The Dean principal is synthetic because no Dean can sign in until US-012 (carried US-010 F-1).
/// </summary>
public sealed class AccessCheckAuthorizationTests(PostgreSqlFixture database)
{
    private static ClaimsPrincipal PrincipalOf(string role) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "1")],
            CookieAuthenticationDefaults.AuthenticationScheme));

    [Fact]
    public async Task ThePolicy_AdmitsAnAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(PrincipalOf("Admin"), resource: null, AccessCheckTestData.Policy);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ThePolicy_RefusesADean()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(PrincipalOf("Dean"), resource: null, AccessCheckTestData.Policy);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ThePolicy_RefusesAnAnonymousPrincipal()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource: null,
            AccessCheckTestData.Policy);

        Assert.False(result.Succeeded);
    }

    /// <summary>Spec I-9: one policy per matrix row — separate from the settings and the instruction policies.</summary>
    [Fact]
    public async Task ThePolicy_IsSeparateFromTheOtherSettingsPolicies()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var policies = host.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.NotNull(await policies.GetPolicyAsync(AccessCheckTestData.Policy));
        Assert.NotEqual(AccessCheckTestData.Policy, WorkspaceConnectionTestData.Policy);
        Assert.NotEqual(AccessCheckTestData.Policy, ConnectionInstructionTestData.Policy);
    }

    /// <summary>Openapi: the path serves a GET and a POST, neither anonymous.</summary>
    [Fact]
    public async Task TheEndpoints_AreGetAndPost_AndNeitherIsAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services)
            .Where(e => e.Pattern == AccessCheckTestData.Path.Trim('/'))
            .ToList();
        var methods = endpoints.SelectMany(e => e.Methods ?? []).ToList();

        Assert.NotEmpty(endpoints);
        Assert.Contains("GET", methods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("POST", methods, StringComparer.OrdinalIgnoreCase);
        Assert.All(endpoints, e => Assert.False(e.AllowsAnonymous, e.ToString()));
    }

    /// <summary>S-01: the SC-4 closed list of anonymous endpoints gains nothing.</summary>
    [Fact]
    public async Task TheAnonymousList_GainsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services).ToList();
        var anonymous = endpoints
            .Where(e => e.AllowsAnonymous && !e.IsStaticFile && !e.IsFallback)
            .Select(e => e.Pattern)
            .ToList();

        Assert.Contains(AccessCheckTestData.Path.Trim('/'), endpoints.Select(e => e.Pattern));
        Assert.DoesNotContain(AccessCheckTestData.Path.Trim('/'), anonymous);
    }

    /// <summary>S-10: the POST is not exempt from antiforgery (the exemption list is unchanged).</summary>
    [Fact]
    public async Task ThePost_IsNotExemptFromAntiforgery()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var post = HostEndpoint.All(host.Services)
            .Where(e => e.Pattern == AccessCheckTestData.Path.Trim('/'))
            .Where(e => e.Methods?.Contains("POST", StringComparer.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.NotEmpty(post);
        Assert.All(post, e => Assert.False(e.IsExemptFromAntiforgery, e.ToString()));
    }

    [Fact]
    public async Task AnAnonymousVisitor_OpeningThePage_IsSentToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.OpenAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(SignInTestData.SignInPath, page.LocationPath);
    }

    /// <summary>Openapi POST 302: an anonymous run is sent to sign in and runs nothing.</summary>
    [Fact]
    public async Task AnAnonymousVisitor_RunningTheCheck_IsSentToSignIn_AndNothingRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        host.ConfigureServices = AccessCheckHostExtensions.RegisterFakeProbe;
        host.Start();
        await AccessCheckHostExtensions.SeedConnectionAsync(host, SeededConnection.Usable, ct);
        var probe = AccessCheckHostExtensions.ProbeOf(host);
        using var client = host.CreateClient();
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var response = await client.PostFormAsync(AccessCheckTestData.Path, [], ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        Assert.Empty(probe.RequestCalls);
        Assert.Empty(await host.AccessCheckAuditRowsAsync(ct));
    }

    [Fact]
    public async Task ASignedInAdmin_ReachesThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
    }

    /// <summary>S-10, API-7: without the antiforgery token the run is refused with 400 and nothing reaches Google.</summary>
    [Fact]
    public async Task ARunWithoutTheAntiforgeryToken_IsRefused_AndNothingRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.RunAccessCheckAsync(ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Empty(probe.RequestCalls);
        Assert.Empty(await host.AccessCheckAuditRowsAsync(ct));
    }
}
