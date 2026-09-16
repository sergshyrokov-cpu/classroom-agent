using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-011: sign-out is a POST that ends the session server-side; GET does nothing (FR-011, API-4).</summary>
public sealed class SignOutTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task SignOut_EndsSession_RedirectsToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var home = await owner.GetAsync("/", ct);

        var response = await owner.PostFormAsync("/sign-out", [], ct);
        var afterwards = await owner.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, home.Status);
        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/sign-in", response.LocationPath);
        Assert.NotNull(response.SetCookie(SetCookieHeader.SessionCookieName));
        Assert.DoesNotContain(SetCookieHeader.SessionCookieName, owner.Cookies.Keys);
        Assert.Equal(HttpStatusCode.Redirect, afterwards.Status);
        Assert.Equal("/sign-in", afterwards.LocationPath);
    }

    [Fact]
    public async Task AfterSignOut_ReplayedOldCookie_IsNotAuthenticated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await owner.GetAsync("/", ct);
        var cookiesBeforeSignOut = owner.Cookies;
        Assert.Contains(SetCookieHeader.SessionCookieName, cookiesBeforeSignOut.Keys);

        await owner.PostFormAsync("/sign-out", [], ct);
        using var replay = host.CreateClient();
        replay.ReplaceCookies(cookiesBeforeSignOut);
        var response = await replay.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/sign-in", response.LocationPath);
    }

    [Fact]
    public async Task GetSignOut_Returns404_SessionStillValid()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync("/sign-out", ct);
        var home = await owner.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Equal(HttpStatusCode.OK, home.Status);
    }

    [Fact]
    public async Task SignOutWithoutToken_Returns400_SessionStays()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.PostFormAsync("/sign-out", [], ct, withToken: false);
        var home = await owner.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Error.PageExpired", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, home.Status);
    }
}
