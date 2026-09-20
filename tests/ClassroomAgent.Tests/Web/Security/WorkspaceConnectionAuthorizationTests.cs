using System.Net;
using System.Security.Claims;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-009 AC-001: the connection settings belong to the Admin alone — the permission-matrix row "Настройка
/// WorkspaceConnection (домен, impersonation)" is ✔ Admin, ✘ Dean (<c>trebovaniya.md</c> §2; spec FR-010,
/// S-01; api-design §4; TC-5). Both the allowed and the forbidden role are proven, and the SC-4 anonymous
/// closed list gains nothing.
/// </summary>
public sealed class WorkspaceConnectionAuthorizationTests(PostgreSqlFixture database)
{
    private static ClaimsPrincipal PrincipalOf(string role) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "1")],
            CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>AC-001: the policy exists and admits an Admin.</summary>
    [Fact]
    public async Task ThePolicy_AdmitsAnAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            PrincipalOf("Admin"),
            resource: null,
            WorkspaceConnectionTestData.Policy);

        Assert.True(result.Succeeded);
    }

    /// <summary>AC-001: a Dean is refused by the policy itself, not by a hidden link.</summary>
    [Fact]
    public async Task ThePolicy_RefusesADean()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            PrincipalOf("Dean"),
            resource: null,
            WorkspaceConnectionTestData.Policy);

        Assert.False(result.Succeeded);
    }

    /// <summary>AC-001: an anonymous principal is refused as well.</summary>
    [Fact]
    public async Task ThePolicy_RefusesAnAnonymousPrincipal()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource: null,
            WorkspaceConnectionTestData.Policy);

        Assert.False(result.Succeeded);
    }

    /// <summary>AC-001: both endpoints declare the policy rather than relying on the fallback (API-9).</summary>
    [Fact]
    public async Task BothEndpoints_DeclareTheAdminPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services)
            .Where(e => e.Pattern == WorkspaceConnectionTestData.Path.Trim('/'))
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, e => Assert.False(e.AllowsAnonymous, e.ToString()));
        Assert.Contains(endpoints, e => e.Methods is null || e.Methods.Contains("GET", StringComparer.OrdinalIgnoreCase));
        Assert.Contains(endpoints, e => e.Methods is null || e.Methods.Contains("POST", StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>AC-001, S-01: the SC-4 closed list of anonymous endpoints is unchanged by this Story.</summary>
    [Fact]
    public async Task TheAnonymousList_GainsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var anonymous = HostEndpoint.All(host.Services)
            .Where(e => e.AllowsAnonymous && !e.IsStaticFile && !e.IsFallback)
            .Select(e => e.Pattern)
            .ToList();

        Assert.DoesNotContain(WorkspaceConnectionTestData.Path.Trim('/'), anonymous);
    }

    /// <summary>AC-001: an anonymous visitor is sent to sign in and never sees the page.</summary>
    [Fact]
    public async Task AnAnonymousVisitor_IsSentToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.OpenSettingsAsync(ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(SignInTestData.SignInPath, page.LocationPath);
    }

    /// <summary>AC-001: a signed-in Admin reaches the page.</summary>
    [Fact]
    public async Task ASignedInAdmin_ReachesThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenSettingsAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
    }

    /// <summary>AC-001: the save without an antiforgery token is refused with 400 and writes nothing (S-08).</summary>
    [Fact]
    public async Task TheSaveWithoutAnAntiforgeryToken_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(
            WorkspaceConnectionTestData.TechnicalAccount,
            ct,
            withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Empty(await host.WorkspaceConnectionsAsync(ct));
    }

    /// <summary>AC-001: the save endpoint is not exempt from the global antiforgery rule.</summary>
    [Fact]
    public async Task TheSaveEndpoint_IsNotExemptFromAntiforgery()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services)
            .Where(e => e.Pattern == WorkspaceConnectionTestData.Path.Trim('/'))
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, e => Assert.False(e.IsExemptFromAntiforgery, e.ToString()));
    }

    /// <summary>AC-001: a GET saves nothing — only POST writes (<c>trebovaniya.md</c> §8).</summary>
    [Fact]
    public async Task AGetWithTheFieldInTheQuery_SavesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.GetAsync(
            WorkspaceConnectionTestData.Path
            + "?" + WorkspaceConnectionTestData.EmailField
            + "=" + Uri.EscapeDataString(WorkspaceConnectionTestData.TechnicalAccount),
            ct);

        Assert.Empty(await host.WorkspaceConnectionsAsync(ct));
        Assert.Empty(await host.ConnectionAuditRowsAsync(ct));
    }
}
