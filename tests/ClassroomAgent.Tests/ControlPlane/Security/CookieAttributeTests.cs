using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-005, AC-011: cookie names and attributes fixed by SC-2 and OD-002 (TC-5).</summary>
public sealed class CookieAttributeTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task SessionCookie_IsHostPrefixedSecureHttpOnlyStrict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        await client.GetAsync("/setup", ct);

        var response = await client.PostFormAsync("/setup", TestData.SetupFields(), ct);

        AssertHostOnlyStrictCookie(response, SetCookieHeader.SessionCookieName);
    }

    [Fact]
    public async Task SessionCookie_HasNoExpiresOrMaxAge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        await client.GetAsync("/setup", ct);
        var setup = await client.PostFormAsync("/setup", TestData.SetupFields(), ct);
        var (_, signIn) = await host.SignInAsync(TestData.Login, TestData.Password, ct);

        foreach (var response in new[] { setup, signIn })
        {
            var header = response.SetCookie(SetCookieHeader.SessionCookieName);
            Assert.NotNull(header);
            var cookie = SetCookieHeader.Parse(header);
            Assert.False(cookie.Has("expires"), header);
            Assert.False(cookie.Has("max-age"), header);
        }
    }

    [Fact]
    public async Task AntiforgeryCookie_IsHostPrefixedSecureHttpOnlyStrict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync("/setup", ct);

        AssertHostOnlyStrictCookie(response, SetCookieHeader.AntiforgeryCookieName);
    }

    [Fact]
    public async Task EveryCookieSetByTheStoryFlows_IsSecure()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var responses = new List<PageResponse>();
        using var client = host.CreateClient();

        responses.Add(await client.GetAsync("/setup", ct));
        responses.Add(await client.PostFormAsync("/setup", TestData.SetupFields(setupCode: "wrong"), ct));
        responses.Add(await client.PostFormAsync("/setup", TestData.SetupFields(), ct));
        responses.Add(await client.GetAsync("/", ct));
        responses.Add(await client.PostFormAsync("/sign-out", [], ct));
        responses.Add(await client.GetAsync("/sign-in", ct));
        responses.Add(await client.PostFormAsync("/sign-in", TestData.SignInFields(password: "not the right password"), ct));
        responses.Add(await client.PostFormAsync("/sign-in", TestData.SignInFields(), ct));
        responses.Add(await client.GetAsync("/error/404", ct));

        var cookies = responses.SelectMany(r => r.SetCookies).ToList();
        Assert.Contains(cookies, c => c.StartsWith(SetCookieHeader.SessionCookieName + "=", StringComparison.Ordinal));
        Assert.Contains(cookies, c => c.StartsWith(SetCookieHeader.AntiforgeryCookieName + "=", StringComparison.Ordinal));
        Assert.All(cookies, c => Assert.True(SetCookieHeader.Parse(c).Has("secure"), c));
    }

    private static void AssertHostOnlyStrictCookie(PageResponse response, string name)
    {
        var header = response.SetCookie(name);
        Assert.NotNull(header);
        var cookie = SetCookieHeader.Parse(header);
        Assert.True(cookie.Has("secure"), header);
        Assert.True(cookie.Has("httponly"), header);
        Assert.Equal("strict", cookie.Get("samesite"), StringComparer.OrdinalIgnoreCase);
        Assert.Equal("/", cookie.Get("path"));
        Assert.False(cookie.Has("domain"), header);
    }
}
