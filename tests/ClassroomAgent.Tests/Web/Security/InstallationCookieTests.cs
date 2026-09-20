using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-008 AC-013: the installation's cookies carry exactly the attributes SC-2 fixes for this host, every
/// state-changing request on the public port is validated for antiforgery by one global rule, and the public
/// port redirects HTTP to HTTPS and sends HSTS (spec FR-005, FR-015; S-11, S-12; NFR-072; TC-5).
/// </summary>
public sealed class InstallationCookieTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    /// <summary>AC-013: the session cookie is httpOnly, Secure and Lax — Lax is required, not preferred.</summary>
    [Fact]
    public async Task TheSessionCookie_IsHostPrefixedSecureHttpOnlyLax()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        var header = callback.SetCookie(SignInTestData.SessionCookieName);
        Assert.NotNull(header);
        var cookie = SetCookieHeader.Parse(header!);
        Assert.True(cookie.Has("secure"), header);
        Assert.True(cookie.Has("httponly"), header);
        Assert.Equal("lax", cookie.Get("samesite"), ignoreCase: true);
        Assert.Equal("/", cookie.Get("path"));
        Assert.False(cookie.Has("domain"), header);
    }

    /// <summary>AC-013, NFR-072: the session is not persistent — there is no "remember me".</summary>
    [Fact]
    public async Task TheSessionCookie_HasNoExpiresAndNoMaxAge()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        var header = callback.SetCookie(SignInTestData.SessionCookieName);
        Assert.NotNull(header);
        var cookie = SetCookieHeader.Parse(header!);
        Assert.False(cookie.Has("expires"), header);
        Assert.False(cookie.Has("max-age"), header);
    }

    /// <summary>AC-013: the antiforgery cookie is httpOnly, Secure and Strict.</summary>
    [Fact]
    public async Task TheAntiforgeryCookie_IsHostPrefixedSecureHttpOnlyStrict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync(SignInTestData.SignInPath, ct);

        var header = response.SetCookie(SignInTestData.AntiforgeryCookieName);
        Assert.NotNull(header);
        var cookie = SetCookieHeader.Parse(header!);
        Assert.True(cookie.Has("secure"), header);
        Assert.True(cookie.Has("httponly"), header);
        Assert.Equal("strict", cookie.Get("samesite"), ignoreCase: true);
    }

    /// <summary>AC-013, SC-2 v64: every cookie of the sign-in flow is Secure, the correlation cookie included.</summary>
    [Fact]
    public async Task EveryCookieOfTheSignInFlow_IsSecure()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        var responses = new List<PageResponse>();

        responses.Add(await client.GetAsync(SignInTestData.SignInPath, ct));
        var start = await client.PostFormAsync(SignInTestData.StartPath, [], ct);
        responses.Add(start);
        responses.Add(await client.CompleteGoogleCallbackAsync(InstallationSignInExtensions.StateOf(start), ct));
        responses.Add(await client.GetAsync(SignInTestData.LandingPath, ct));
        responses.Add(await client.PostFormAsync(SignInTestData.SignOutPath, [], ct));
        responses.Add(await client.GetAsync(SignInTestData.ErrorPath(404), ct));

        var cookies = responses.SelectMany(r => r.SetCookies).Select(SetCookieHeader.Parse).ToList();
        Assert.NotEmpty(cookies);
        Assert.All(cookies, c => Assert.True(c.Has("secure"), $"{c.Name} is not Secure."));
    }

    /// <summary>AC-013, S-12: the installation's public-port antiforgery exemption list is empty.</summary>
    [Fact]
    public async Task NoPublicPortEndpoint_IsExemptFromAntiforgery()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var exempt = HostEndpoint.All(host.Services)
            .Where(e => !e.IsFallback && e.UnsafeMethodsAccepted.Count > 0 && e.IsExemptFromAntiforgery)
            .Select(e => e.Pattern)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        // The one exemption of this host is the US-006 push receiver, and it lives on the private port.
        Assert.Equal(new[] { "service/v1/status-pushes" }, exempt);
    }

    /// <summary>AC-013, TC-5: every state-changing public endpoint refuses a request with no token.</summary>
    [Fact]
    public async Task EveryStateChangingPublicEndpoint_WithoutToken_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (signedIn, _) = await host.SignInWithGoogleAsync(ct);
        var pageExpired = host.Text(SignInTestData.TextKeys.PageExpired, "uk");

        var stateChanging = HostEndpoint.All(host.Services)
            .Where(e => !e.IsFallback && !e.IsStaticFile && !e.IsExemptFromAntiforgery && e.UnsafeMethodsAccepted.Count > 0)
            .ToList();
        Assert.Contains(stateChanging, e => e.Pattern == "sign-in/google");
        Assert.Contains(stateChanging, e => e.Pattern == "sign-out");

        var failures = new List<string>();
        foreach (var endpoint in stateChanging)
        {
            foreach (var method in endpoint.UnsafeMethodsAccepted)
            {
                using var anonymous = host.CreateClient();
                var client = endpoint.AllowsAnonymous ? anonymous : signedIn;
                var response = await client.SendAsync(
                    new HttpMethod(method),
                    endpoint.SamplePath,
                    new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()),
                    ct);
                if (response.Status != HttpStatusCode.BadRequest || !response.Text.Contains(pageExpired, StringComparison.Ordinal))
                {
                    failures.Add($"{method} {endpoint.SamplePath} -> {(int)response.Status}");
                }
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>AC-013, SC-2: the public port redirects HTTP to HTTPS.</summary>
    [Fact]
    public async Task ThePublicPort_RedirectsHttpToHttps()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicHttpAsync("GET", SignInTestData.SignInPath, ct);

        Assert.True(
            response.Status is HttpStatusCode.MovedPermanently or HttpStatusCode.TemporaryRedirect
                or HttpStatusCode.PermanentRedirect or HttpStatusCode.Redirect,
            $"Expected a redirect to HTTPS, got {(int)response.Status}.");
        Assert.StartsWith("https://", response.Headers["Location"], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-013, SC-2: the public port sends HSTS; the private port never does.</summary>
    [Fact]
    public async Task ThePublicPortSendsHsts_AndThePrivatePortDoesNot()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var publicResponse = await host.SendPublicAsync("GET", SignInTestData.SignInPath, ct);
        var privateResponse = await host.SendPrivateAsync("GET", "/health/live", ct);

        Assert.True(
            publicResponse.Headers.ContainsKey("Strict-Transport-Security"),
            "The public port sent no HSTS header.");
        Assert.False(
            privateResponse.Headers.ContainsKey("Strict-Transport-Security"),
            "The private port must not send HSTS — it would break the status push (DC-6).");
    }
}
