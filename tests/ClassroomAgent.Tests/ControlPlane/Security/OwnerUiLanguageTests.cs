using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>
/// US-039 on the Control Plane — the same operation and switcher for the Owner (OD-004): AC-001, AC-002, AC-004,
/// AC-005, AC-006, AC-009, AC-010 (spec FR-002 … FR-006, VR-001 … VR-003; openapi <c>POST /account/language</c>,
/// host ClassroomAgent.ControlPlane). Refusals are paired with the allowed request in the same test.
/// </summary>
public sealed class OwnerUiLanguageTests(PostgreSqlFixture database)
{
    private const string Home = "/";

    private const string Installations = "/installations";

    /// <summary>AC-001: the Owner's pages carry the switcher, offering the other language.</summary>
    [Theory]
    [InlineData(Home)]
    [InlineData(Installations)]
    [InlineData("/installations/new")]
    public async Task EveryOwnerPage_RendersTheSwitcher(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var page = await owner.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(UiLanguageTestData.HasSwitcher(page.Body), $"No switcher on {path}.");
        Assert.Equal([UiLanguageTestData.English], UiLanguageTestData.OfferedLanguages(page.Body));
        Assert.Contains("aria-current=\"true\"", page.Body, StringComparison.Ordinal);
        Assert.Equal(path, UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>AC-001, AC-002: the same page comes back in English; the Owner row holds the choice.</summary>
    [Fact]
    public async Task ChoosingEnglish_ShowsTheSamePageInEnglish_AndStoresIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var choice = await owner.ChooseLanguageAsync(Installations, UiLanguageTestData.English, ct);
        var same = await owner.GetAsync(choice.LocationPath ?? Home, ct);
        var next = await owner.GetAsync(Home, ct);

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal(Installations, choice.Location);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(same.Body));
        Assert.Equal([UiLanguageTestData.Ukrainian], UiLanguageTestData.OfferedLanguages(same.Body));
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(next.Body));
        Assert.Equal(UiLanguageTestData.English, (await host.OwnerAsync(ct))!.UiLanguage);
    }

    /// <summary>AC-002: a later sign-in opens in the chosen language.</summary>
    [Fact]
    public async Task TheChoice_FollowsTheOwnerToANewSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await owner.ChooseLanguageAsync(Home, UiLanguageTestData.English, ct);

        var (again, signIn) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using var _again = again;
        var page = await again.GetAsync(Home, ct);

        Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(page.Body));
    }

    /// <summary>AC-004: anonymous → challenged, nothing stored; the signed-in Owner then succeeds.</summary>
    [Fact]
    public async Task AnAnonymousRequest_IsChallenged_AndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        using var anonymous = host.CreateClient();
        await anonymous.GetAsync("/sign-in", ct);

        var refused = await anonymous.PostFormAsync(
            UiLanguageTestData.ChoosePath,
            [new(UiLanguageTestData.Fields.Language, UiLanguageTestData.English)],
            ct);
        var storedAfterRefusal = (await host.OwnerAsync(ct))!.UiLanguage;
        var allowed = await owner.ChooseLanguageAsync(Home, UiLanguageTestData.English, ct);

        Assert.Equal(HttpStatusCode.Redirect, refused.Status);
        Assert.Equal("/sign-in", refused.LocationPath?.Split('?')[0]);
        Assert.Equal(UiLanguageTestData.Ukrainian, storedAfterRefusal);
        Assert.Equal(HttpStatusCode.Redirect, allowed.Status);
        Assert.Equal(UiLanguageTestData.English, (await host.OwnerAsync(ct))!.UiLanguage);
    }

    /// <summary>AC-004: authenticated, POST only, not exempt from antiforgery.</summary>
    [Fact]
    public async Task TheAction_IsAuthenticated_PostOnly_AndNotExemptFromAntiforgery()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var endpoint = Assert.Single(HostEndpoint.All(host.Services), e => e.Pattern == "account/language");

        Assert.False(endpoint.AllowsAnonymous, endpoint.ToString());
        Assert.False(endpoint.IsExemptFromAntiforgery, endpoint.ToString());
        Assert.Equal(["POST"], endpoint.Methods);
    }

    /// <summary>AC-004: GET changes nothing; no token → 400; both paired with the allowed POST.</summary>
    [Fact]
    public async Task AGetOrAPostWithoutToken_ChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var get = await owner.GetAsync(UiLanguageTestData.ChoosePath + "?language=en", ct);
        var noToken = await owner.ChooseLanguageAsync(Home, UiLanguageTestData.English, ct, withToken: false);
        var storedAfterRefusals = (await host.OwnerAsync(ct))!.UiLanguage;
        var allowed = await owner.ChooseLanguageAsync(Home, UiLanguageTestData.English, ct);

        Assert.Contains(get.Status, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(HttpStatusCode.BadRequest, noToken.Status);
        Assert.Equal(UiLanguageTestData.Ukrainian, storedAfterRefusals);
        Assert.Equal(HttpStatusCode.Redirect, allowed.Status);
        Assert.Equal(UiLanguageTestData.English, (await host.OwnerAsync(ct))!.UiLanguage);
    }

    /// <summary>AC-005: invalid codes → 400 with the error page, nothing stored, the marker never logged.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("EN")]
    [InlineData("0")]
    [InlineData("de")]
    [InlineData(UiLanguageTestData.RejectedMarker)]
    public async Task AnInvalidCode_IsRefused_NotStored_NotLogged(string code)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var refused = await owner.ChooseLanguageAsync(Home, code, ct);
        var storedAfterRefusal = (await host.OwnerAsync(ct))!.UiLanguage;
        var allowed = await owner.ChooseLanguageAsync(Home, UiLanguageTestData.English, ct);
        var logs = string.Join("\n", await host.ReadLogFilesWhileRunningAsync(ct));

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Contains(host.Text("Error.PageExpired", UiLanguageTestData.Ukrainian), refused.Text, StringComparison.Ordinal);
        Assert.Equal(UiLanguageTestData.Ukrainian, storedAfterRefusal);
        Assert.Equal(HttpStatusCode.Redirect, allowed.Status);
        Assert.DoesNotContain(UiLanguageTestData.RejectedMarker, logs, StringComparison.Ordinal);
    }

    /// <summary>AC-010: a foreign return path leads to <c>/</c>; the choice is still stored.</summary>
    [Theory]
    [InlineData("//evil.example.test/")]
    [InlineData("https://evil.example.test/")]
    [InlineData("/\\evil.example.test")]
    [InlineData("")]
    public async Task AForeignReturnPath_LeadsToHome(string returnPath)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var choice = await owner.ChooseLanguageAsync(Installations, UiLanguageTestData.English, ct, returnPath: returnPath);

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal("/", choice.Location);
        Assert.Equal(UiLanguageTestData.English, (await host.OwnerAsync(ct))!.UiLanguage);
    }

    /// <summary>FR-005, SC-2: the re-issued cookie keeps the Control Plane's attributes — Strict, non-persistent.</summary>
    [Fact]
    public async Task TheReissuedCookie_KeepsTheSc2Attributes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var choice = await owner.ChooseLanguageAsync(Home, UiLanguageTestData.English, ct);

        var header = choice.SetCookie(SetCookieHeader.SessionCookieName);
        Assert.NotNull(header);
        var cookie = SetCookieHeader.Parse(header!);
        Assert.True(cookie.Has("secure"), header);
        Assert.True(cookie.Has("httponly"), header);
        Assert.Equal("strict", cookie.Get("samesite"), ignoreCase: true);
        Assert.False(cookie.Has("expires"), header);
        Assert.False(cookie.Has("max-age"), header);
    }

    /// <summary>AC-009: the re-issued Owner session still ends 8 hours after the original sign-in.</summary>
    [Fact]
    public async Task TheReissuedSession_StillEndsEightHoursAfterTheOriginalSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        for (var step = 1; step <= 23; step++)
        {
            host.Time.Advance(TimeSpan.FromMinutes(20));
            if (step == 21)
            {
                var choice = await owner.ChooseLanguageAsync(Home, UiLanguageTestData.English, ct);
                Assert.Equal(HttpStatusCode.Redirect, choice.Status);
            }
            else
            {
                var active = await owner.GetAsync(Home, ct);
                Assert.True(active.Status == HttpStatusCode.OK, $"Expected 200 at {step * 20} minutes, got {(int)active.Status}.");
            }
        }

        host.Time.Advance(TimeSpan.FromMinutes(19));
        var justBefore = await owner.GetAsync(Home, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));
        var past = await owner.GetAsync(Home, ct);

        Assert.Equal(HttpStatusCode.OK, justBefore.Status);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(justBefore.Body));
        Assert.Equal(HttpStatusCode.Redirect, past.Status);
        Assert.Equal("/sign-in", past.LocationPath);
    }

    /// <summary>AC-009, I-3: the stamp is not rotated, and a kept copy of the re-issued cookie dies at sign-out.</summary>
    [Fact]
    public async Task TheReissuedCookie_StillDiesAtSignOut_AndTheStampIsNotRotated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var stampBefore = (await host.OwnerAsync(ct))!.SecurityStamp;

        var choice = await owner.ChooseLanguageAsync(Home, UiLanguageTestData.English, ct);
        var stampAfterChoice = (await host.OwnerAsync(ct))!.SecurityStamp;
        var kept = owner.Cookies;
        var alive = await owner.GetAsync(Home, ct);
        await owner.PostFormAsync("/sign-out", [], ct);
        using var thief = host.CreateClient();
        thief.ReplaceCookies(kept);
        var replay = await thief.GetAsync(Home, ct);

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal(stampBefore, stampAfterChoice);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(alive.Body));
        Assert.Equal(HttpStatusCode.Redirect, replay.Status);
        Assert.Equal("/sign-in", replay.LocationPath);
    }

    /// <summary>AC-006: sign-in, setup and error pages offer no switcher and stay Ukrainian; a signed-in page does.</summary>
    [Fact]
    public async Task AnonymousPages_HaveNoSwitcher_AndStayUkrainian()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var englishBrowser = new Dictionary<string, string> { ["Accept-Language"] = "en" };
        using var anonymous = host.CreateClient();

        var setup = await anonymous.GetAsync("/setup", ct, englishBrowser);
        using var owner = await host.CreateOwnerAsync(ct);
        var signIn = await anonymous.GetAsync("/sign-in", ct, englishBrowser);
        var error = await anonymous.GetAsync("/error/404", ct, englishBrowser);
        var signedIn = await owner.GetAsync(Home, ct);
        await owner.ChooseLanguageAsync(Home, UiLanguageTestData.English, ct);
        var errorSignedIn = await owner.GetAsync("/error/404", ct);

        foreach (var page in new[] { setup, signIn, error })
        {
            Assert.False(UiLanguageTestData.HasSwitcher(page.Body));
            Assert.Equal(UiLanguageTestData.Ukrainian, UiLanguageTestData.PageLanguage(page.Body));
        }

        Assert.True(UiLanguageTestData.HasSwitcher(signedIn.Body));
        Assert.False(UiLanguageTestData.HasSwitcher(errorSignedIn.Body));
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(errorSignedIn.Body));
    }
}
