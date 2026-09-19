using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>
/// US-006 AC-002, AC-013 on the Control Plane side: the push address pages are Owner-only, their POST
/// needs an antiforgery token, and neither becomes anonymous (SC-4, TC-5; api-design §10).
/// </summary>
public sealed class PushAddressSecurityTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task WithoutASession_TheFormRedirectsToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.GetAsync(PushTestData.PushAddressPath(installation), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/sign-in", response.LocationPath);
    }

    [Fact]
    public async Task WithoutASession_ThePostRedirectsToSignIn_AndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);
        using var anonymous = host.CreateClient();

        var response = await anonymous.PostFormAsync(
            PushTestData.PushAddressPath(installation),
            PushTestData.AddressFields(PushTestData.Address),
            ct,
            withToken: false);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/sign-in", response.LocationPath);
        Assert.Null(await host.PushAddressAsync(installation, ct));
    }

    [Fact]
    public async Task WithoutTheOwnerRole_TheFormIsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        using var stripped = host.CreateClient();
        stripped.ReplaceCookies(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SetCookieHeader.SessionCookieName] = await host.SessionWithoutOwnerRoleAsync(owner),
        });

        var response = await stripped.GetAsync(PushTestData.PushAddressPath(installation), ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
    }

    [Fact]
    public async Task WithoutTheOwnerRole_ThePostIsForbidden_AndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);
        await owner.LoadTokenAsync(ct);
        var token = owner.LastToken;
        using var stripped = host.CreateClient();
        stripped.ReplaceCookies(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SetCookieHeader.SessionCookieName] = await host.SessionWithoutOwnerRoleAsync(owner),
            [SetCookieHeader.AntiforgeryCookieName] = Assert.Contains(SetCookieHeader.AntiforgeryCookieName, owner.Cookies),
        });

        var response = await stripped.PostFormAsync(
            PushTestData.PushAddressPath(installation),
            [.. PushTestData.AddressFields(PushTestData.Address), new(Html.AntiforgeryFieldName, token!)],
            ct,
            withToken: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Null(await host.PushAddressAsync(installation, ct));
    }

    [Fact]
    public async Task WithoutAnAntiforgeryToken_ThePostIsRefused_AndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        var response = await owner.PostFormAsync(
            PushTestData.PushAddressPath(installation),
            PushTestData.AddressFields(PushTestData.Address),
            ct,
            withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Error.PageExpired", "uk"), response.Body, StringComparison.Ordinal);
        Assert.Null(await host.PushAddressAsync(installation, ct));
    }

    [Fact]
    public async Task GettingThePushAddressPage_ChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.GetAsync(PushTestData.PushAddressPath(installation) + "?pushAddress=", ct);

        Assert.Equal(PushTestData.Address, await host.PushAddressAsync(installation, ct));
    }

    [Fact]
    public async Task BeforeSetup_ThePushAddressPageRedirectsToSetup()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync(
            PushTestData.PushAddressPath(Guid.Parse(InstallationTestData.UnknownIdentifier)),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/setup", response.LocationPath);
    }
}
