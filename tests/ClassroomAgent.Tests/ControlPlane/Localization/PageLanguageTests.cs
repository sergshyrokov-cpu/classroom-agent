using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Localization;

/// <summary>AC-009: anonymous pages are Ukrainian whatever the browser asks; a signed-in Owner sees the account language (FR-019).</summary>
public sealed class PageLanguageTests(PostgreSqlFixture database)
{
    private static readonly IReadOnlyDictionary<string, string> EnglishBrowser =
        new Dictionary<string, string> { ["Accept-Language"] = "en-US,en;q=0.9" };

    [Fact]
    public async Task SetupSignInAndErrorPages_AreUkrainianForAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var errorPage = await client.GetAsync("/error/404", ct, EnglishBrowser);
        await client.GetAsync("/setup", ct, EnglishBrowser);
        var setupPage = await client.PostFormAsync(
            "/setup",
            TestData.SetupFields(setupCode: TestData.OtherSetupCode),
            ct,
            headers: EnglishBrowser);
        using var owner = await host.CreateOwnerAsync(ct);
        using var anonymous = host.CreateClient();
        await anonymous.GetAsync("/sign-in", ct, EnglishBrowser);
        var signInPage = await anonymous.PostFormAsync(
            "/sign-in",
            TestData.SignInFields(password: "this is not the password"),
            ct,
            headers: EnglishBrowser);

        AssertLanguage(host, errorPage, "Error.NotFound", "uk", "en");
        AssertLanguage(host, setupPage, "Setup.SetupCode.Invalid", "uk", "en");
        AssertLanguage(host, signInPage, "SignIn.Refused", "uk", "en");
    }

    [Fact]
    public async Task SignedInOwner_SeesAccountLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var ukrainianOwner = await host.CreateOwnerAsync(ct);

        var ukrainianPage = await ukrainianOwner.GetAsync("/error/404", ct, EnglishBrowser);
        await host.ExecuteAsync("UPDATE owner SET ui_language = 'en'", ct);
        var (englishOwner, signIn) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using var _ = englishOwner;
        var englishPage = await englishOwner.GetAsync("/error/404", ct);
        var home = await englishOwner.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
        Assert.Equal(HttpStatusCode.OK, home.Status);
        AssertLanguage(host, ukrainianPage, "Error.NotFound", "uk", "en");
        AssertLanguage(host, englishPage, "Error.NotFound", "en", "uk");
    }

    private static void AssertLanguage(ControlPlaneTestHost host, PageResponse page, string key, string expected, string other)
    {
        Assert.Contains(host.Text(key, expected), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(key, other), page.Text, StringComparison.Ordinal);
    }
}
