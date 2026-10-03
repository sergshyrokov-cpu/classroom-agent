using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-039 AC-001, AC-002, AC-006, AC-008 on the installation: the header switcher on every signed-in page, the
/// choice taking effect from the next page without signing in again, following the user to a new sign-in, and
/// absent from every anonymous page (spec FR-002, FR-003, FR-005, FR-006, FR-008; openapi x-ui-elements).
/// </summary>
public sealed class LanguageSwitcherTests(PostgreSqlFixture database)
{
    private const string DeansPage = DeanAccountTestData.Paths.Deans;

    /// <summary>AC-001: the Admin's pages carry the switcher, offering the other language only.</summary>
    [Theory]
    [InlineData(SignInTestData.LandingPath)]
    [InlineData(DeansPage)]
    [InlineData("/settings/workspace-connection")]
    [InlineData("/settings/access-check")]
    public async Task EverySignedInAdminPage_RendersTheSwitcher(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(UiLanguageTestData.HasSwitcher(page.Body), $"No switcher on {path}.");
        Assert.Equal([UiLanguageTestData.English], UiLanguageTestData.OfferedLanguages(page.Body));
        Assert.Contains("aria-current=\"true\"", page.Body, StringComparison.Ordinal);
        Assert.Equal(path, UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>AC-001: the Dean's pages carry it too.</summary>
    [Theory]
    [InlineData(SignInTestData.LandingPath)]
    [InlineData(DeanAccountTestData.Paths.OwnPassword)]
    public async Task EverySignedInDeanPage_RendersTheSwitcher(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (client, _) = await host.SignInDeanAsync(ct);
        using var _client = client;

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(UiLanguageTestData.HasSwitcher(page.Body), $"No switcher on {path}.");
        Assert.Equal([UiLanguageTestData.English], UiLanguageTestData.OfferedLanguages(page.Body));
    }

    /// <summary>AC-001: the switcher keeps the query string of the page it is on (FR-006).</summary>
    [Fact]
    public async Task TheSwitcher_CarriesThePathAndQuery()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(DeansPage + "?view=all", ct);

        Assert.Equal(DeansPage + "?view=all", UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>
    /// AC-001: choosing redirects back to the same page, which — and every page after it — is now in the chosen
    /// language, with no sign-out and no sign-in in between; the switcher now offers the other language.
    /// </summary>
    [Fact]
    public async Task ChoosingEnglish_ShowsTheSamePageInEnglish_WithoutSigningInAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        var before = await client.GetAsync(DeansPage, ct);

        var choice = await client.ChooseLanguageAsync(DeansPage, UiLanguageTestData.English, ct);
        var same = await client.GetAsync(choice.LocationPath ?? "/", ct);
        var next = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(UiLanguageTestData.Ukrainian, UiLanguageTestData.PageLanguage(before.Body));
        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal(DeansPage, choice.Location);
        Assert.Equal(HttpStatusCode.OK, same.Status);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(same.Body));
        Assert.Contains(host.Text("DeanAccounts.Title", UiLanguageTestData.English), same.Text, StringComparison.Ordinal);
        Assert.Equal([UiLanguageTestData.Ukrainian], UiLanguageTestData.OfferedLanguages(same.Body));
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(next.Body));
    }

    /// <summary>AC-001: and back again.</summary>
    [Fact]
    public async Task ChoosingUkrainianAfterEnglish_SwitchesBack()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (client, deanId) = await host.SignInDeanAsync(ct);
        using var _client = client;

        await client.ChooseLanguageAsync(SignInTestData.LandingPath, UiLanguageTestData.English, ct);
        var back = await client.ChooseLanguageAsync(SignInTestData.LandingPath, UiLanguageTestData.Ukrainian, ct);
        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.Redirect, back.Status);
        Assert.Equal(UiLanguageTestData.Ukrainian, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Equal(UiLanguageTestData.Ukrainian, await host.StoredLanguageAsync(deanId, ct));
    }

    /// <summary>
    /// AC-002: the choice is on the account — a later sign-in, and a sign-in from another browser, both open in
    /// the chosen language although the school default is Ukrainian.
    /// </summary>
    [Fact]
    public async Task TheChoice_FollowsTheDeanToANewSignIn_AndAnotherBrowser()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (first, deanId) = await host.SignInDeanAsync(ct);
        using var _first = first;
        await first.ChooseLanguageAsync(SignInTestData.LandingPath, UiLanguageTestData.English, ct);

        var (otherBrowser, _) = await host.SignInDeanAsync(ct);
        using var _other = otherBrowser;
        var otherPage = await otherBrowser.GetAsync(SignInTestData.LandingPath, ct);

        await first.GetAsync(SignInTestData.LandingPath, ct);
        await first.PostFormAsync(SignInTestData.SignOutPath, [], ct);
        var (again, _) = await host.SignInDeanAsync(ct);
        using var _again = again;
        var againPage = await again.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(deanId, ct));
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(otherPage.Body));
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(againPage.Body));
    }

    /// <summary>AC-002: the Admin's choice follows the next Google sign-in too.</summary>
    [Fact]
    public async Task TheChoice_FollowsTheAdminToANewSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await client.ChooseLanguageAsync(SignInTestData.LandingPath, UiLanguageTestData.English, ct);

        var (again, callback) = await host.SignInWithGoogleAsync(ct);
        using var _again = again;
        var page = await again.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(page.Body));
    }

    /// <summary>AC-002: only the chooser's row changes; the other user's language and pages stay Ukrainian.</summary>
    [Fact]
    public async Task AnotherUsersLanguage_IsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, deanId) = await host.SignInDeanAsync(ct);
        using var _dean = dean;
        var (second, secondId) = await host.SignInDeanAsync(ct, DeanAccountTestData.SecondDeanEmail);
        using var _second = second;

        var choice = await dean.ChooseLanguageAsync(SignInTestData.LandingPath, UiLanguageTestData.English, ct);
        var secondPage = await second.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(deanId, ct));
        Assert.Equal(UiLanguageTestData.Ukrainian, await host.StoredLanguageAsync(secondId, ct));
        Assert.Equal(UiLanguageTestData.Ukrainian, UiLanguageTestData.PageLanguage(secondPage.Body));
    }

    /// <summary>
    /// AC-006: no anonymous page offers the switcher, and each is in the school default whatever the browser asks.
    /// Paired with a signed-in page of the same host carrying it, so the absence is measured against a presence.
    /// </summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task AnonymousPages_HaveNoSwitcher_AndUseTheSchoolDefault(string schoolDefault)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.DefaultLanguage] = schoolDefault;
        host.Start();
        using var anonymous = host.CreateClient();
        var browser = new Dictionary<string, string> { ["Accept-Language"] = schoolDefault == "uk" ? "en" : "uk" };

        var signIn = await anonymous.GetAsync(SignInTestData.SignInPath, ct, browser);
        var error = await anonymous.GetAsync("/error/404", ct, browser);
        var (dean, _) = await host.SignInDeanAsync(ct);
        using var _dean = dean;
        var signedIn = await dean.GetAsync(SignInTestData.LandingPath, ct);

        Assert.False(UiLanguageTestData.HasSwitcher(signIn.Body));
        Assert.False(UiLanguageTestData.HasSwitcher(error.Body));
        Assert.Equal(schoolDefault, UiLanguageTestData.PageLanguage(signIn.Body));
        Assert.Equal(schoolDefault, UiLanguageTestData.PageLanguage(error.Body));
        Assert.True(UiLanguageTestData.HasSwitcher(signedIn.Body));
    }

    /// <summary>
    /// I-5: the error page shows no switcher even to a signed-in user — it is on SC-4's anonymous list — yet it is
    /// in that user's language (§8).
    /// </summary>
    [Fact]
    public async Task TheErrorPage_HasNoSwitcher_EvenWhenSignedIn_ButUsesTheUsersLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, _) = await host.SignInDeanAsync(ct);
        using var _dean = dean;
        var choice = await dean.ChooseLanguageAsync(SignInTestData.LandingPath, UiLanguageTestData.English, ct);

        var error = await dean.GetAsync("/error/404", ct);

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.False(UiLanguageTestData.HasSwitcher(error.Body));
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(error.Body));
    }

    /// <summary>AC-008: the switcher labels are each language's own name, from the translation files, on every page.</summary>
    [Fact]
    public async Task TheSwitcherLabels_AreTheSelfNames_InBothLanguages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, _) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var ukrainianPage = await dean.GetAsync(SignInTestData.LandingPath, ct);
        await dean.ChooseLanguageAsync(SignInTestData.LandingPath, UiLanguageTestData.English, ct);
        var englishPage = await dean.GetAsync(SignInTestData.LandingPath, ct);

        foreach (var page in new[] { ukrainianPage, englishPage })
        {
            Assert.Contains(UiLanguageTestData.UkrainianSelfName, page.Text, StringComparison.Ordinal);
            Assert.Contains(UiLanguageTestData.EnglishSelfName, page.Text, StringComparison.Ordinal);
        }

        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(englishPage.Body));
    }
}
