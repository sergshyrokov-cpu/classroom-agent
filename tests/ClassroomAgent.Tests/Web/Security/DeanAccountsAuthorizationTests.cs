using System.Net;
using System.Security.Claims;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-012 AC-001: managing Dean accounts belongs to the Admin alone — the matrix row is ✔ Admin, ✘ Dean
/// (<c>trebovaniya.md</c> §2 v64; spec FR-016, S-01, S-02; api-design §4; TC-5). The Dean's own password page is
/// the mirror image: Dean only, because an Admin has no password at all (SC-2).
/// </summary>
public sealed class DeanAccountsAuthorizationTests(PostgreSqlFixture database)
{
    private static ClaimsPrincipal PrincipalOf(string role) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "1")],
            CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>AC-001: the management policy admits an Admin.</summary>
    [Fact]
    public async Task TheManagementPolicy_AdmitsAnAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(PrincipalOf("Admin"), resource: null, DeanAccountTestData.Policies.Manage);

        Assert.True(result.Succeeded);
    }

    /// <summary>AC-001: and refuses a Dean.</summary>
    [Fact]
    public async Task TheManagementPolicy_RefusesADean()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(PrincipalOf("Dean"), resource: null, DeanAccountTestData.Policies.Manage);

        Assert.False(result.Succeeded);
    }

    /// <summary>AC-013: the own-password policy admits a Dean.</summary>
    [Fact]
    public async Task TheOwnPasswordPolicy_AdmitsADean()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(PrincipalOf("Dean"), resource: null, DeanAccountTestData.Policies.ChangeOwn);

        Assert.True(result.Succeeded);
    }

    /// <summary>AC-013, S-07: and refuses an Admin — an Admin has no local password (SC-2).</summary>
    [Fact]
    public async Task TheOwnPasswordPolicy_RefusesAnAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(PrincipalOf("Admin"), resource: null, DeanAccountTestData.Policies.ChangeOwn);

        Assert.False(result.Succeeded);
    }

    /// <summary>AC-001: a signed-in Admin reaches the screen (TC-5, the allowed-role half).</summary>
    [Fact]
    public async Task AnAdmin_ReachesTheScreen()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.OpenDeanAccountsAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
    }

    /// <summary>AC-001: an anonymous visitor is challenged to sign in, not shown the screen (SC-4).</summary>
    [Fact]
    public async Task AnAnonymousVisitor_IsChallenged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.OpenDeanAccountsAsync(ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.SignIn, page.LocationPath);
    }

    /// <summary>
    /// AC-001, AC-009: every management POST without an antiforgery token is refused, and nothing is written
    /// (SC-4, API-7, spec VR-004).
    /// </summary>
    [Fact]
    public async Task AManagementPost_WithoutAnAntiforgeryToken_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        var before = await host.AppUsersAsync(ct);

        var page = await client.CreateDeanAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            ct,
            withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal(before.Count, (await host.AppUsersAsync(ct)).Count);
    }
}
