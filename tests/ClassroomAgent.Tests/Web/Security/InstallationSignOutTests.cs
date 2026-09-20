using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-008 AC-014: sign-out is a <c>POST</c> with the antiforgery token, clears the session so the previous
/// cookie no longer authenticates, and lands the user on the sign-in page (spec FR-016; TC-5;
/// <c>trebovaniya.md</c> section 8).
/// </summary>
public sealed class InstallationSignOutTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    [Fact]
    public async Task SignOut_ClearsTheSession_AndLandsOnTheSignInPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);

        var signOut = await client.PostFormAsync(SignInTestData.SignOutPath, [], ct);
        var afterwards = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.Redirect, signOut.Status);
        Assert.Equal(SignInTestData.SignInPath, signOut.LocationPath);
        Assert.Equal(HttpStatusCode.Redirect, afterwards.Status);
        Assert.Equal(SignInTestData.SignInPath, afterwards.LocationPath);
    }

    /// <summary>AC-014: the previous cookie, replayed, no longer authenticates a request.</summary>
    [Fact]
    public async Task ThePreviousCookieReplayed_NoLongerAuthenticates()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);
        var cookies = client.Cookies;

        await client.PostFormAsync(SignInTestData.SignOutPath, [], ct);

        using var replay = host.CreateClient();
        replay.ReplaceCookies(cookies);
        var response = await replay.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
    }

    /// <summary>AC-014, API-4: a GET does not sign anyone out.</summary>
    [Fact]
    public async Task Get_DoesNotSignAnyoneOut()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var response = await client.GetAsync(SignInTestData.SignOutPath, ct);
        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.True(
            response.Status is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405, got {(int)response.Status}.");
        Assert.Equal(HttpStatusCode.OK, landing.Status);
    }

    /// <summary>AC-014, S-12: without the antiforgery token the session is untouched.</summary>
    [Fact]
    public async Task WithoutTheAntiforgeryToken_TheSessionIsUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);

        var refused = await client.PostFormAsync(SignInTestData.SignOutPath, [], ct, withToken: false);
        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Contains(host.Text(SignInTestData.TextKeys.PageExpired, "uk"), refused.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, landing.Status);
    }

    /// <summary>AC-014: an anonymous sign-out is challenged, not performed — the endpoint requires a user.</summary>
    [Fact]
    public async Task AnonymousSignOut_IsChallenged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var response = await client.PostFormAsync(SignInTestData.SignOutPath, [], ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
    }
}
