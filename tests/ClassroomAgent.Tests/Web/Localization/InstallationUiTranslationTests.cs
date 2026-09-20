using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-008 AC-015: every user-visible string of the installation comes from <c>Application.Localization</c> in
/// both Ukrainian and English, the school default applies to every user, and data from Google is rendered as
/// it came (spec FR-017, I-14; NFR-073; TC-8).
/// </summary>
public sealed class InstallationUiTranslationTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.Json(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    public static TheoryData<string> ContractKeys => new(SignInTestData.TextKeys.All);

    /// <summary>AC-015, TC-8: the two resource sets have the same keys, with no empty value.</summary>
    [Fact]
    public async Task EveryKey_ExistsInUkrainianAndEnglish()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.AllTexts("uk");
        var english = host.AllTexts("en");

        Assert.NotEmpty(ukrainian);
        Assert.Equal(ukrainian.Keys.Order(StringComparer.Ordinal), english.Keys.Order(StringComparer.Ordinal));
        Assert.All(ukrainian, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value), $"uk: {pair.Key}"));
        Assert.All(english, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value), $"en: {pair.Key}"));
    }

    /// <summary>AC-015: every key the approved contract declares is resolved, in both languages.</summary>
    [Theory]
    [MemberData(nameof(ContractKeys))]
    public async Task EveryContractKey_IsResolvedInBothLanguages(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.NotEqual(ukrainian, english);
    }

    /// <summary>AC-015: with no language setting the school's pages are Ukrainian.</summary>
    [Fact]
    public async Task WithoutTheLanguageSetting_ThePagesAreUkrainian()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(SignInTestData.TextKeys.GoogleButton, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(SignInTestData.TextKeys.GoogleButton, "en"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-015: the configured school default decides the language of an anonymous page.</summary>
    [Fact]
    public async Task TheSchoolDefault_DecidesTheLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.DefaultLanguage] = "en";
        host.Start();
        using var client = host.CreateClient();

        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Contains(host.Text(SignInTestData.TextKeys.GoogleButton, "en"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(SignInTestData.TextKeys.GoogleButton, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-015, I-14: the browser's Accept-Language does not select the culture.</summary>
    [Fact]
    public async Task AcceptLanguage_DoesNotSelectTheCulture()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        var headers = new Dictionary<string, string> { ["Accept-Language"] = "en-GB,en;q=0.9" };

        var page = await client.GetAsync(SignInTestData.SignInPath, ct, headers);

        Assert.Contains(host.Text(SignInTestData.TextKeys.GoogleButton, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-015, I-14: neither a query string nor a cookie selects the culture — US-039 adds the choice.</summary>
    [Fact]
    public async Task NeitherAQueryStringNorACookie_SelectsTheCulture()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var withQuery = await client.GetAsync(SignInTestData.SignInPath + "?culture=en&ui-culture=en", ct);
        client.ReplaceCookies(new Dictionary<string, string>
        {
            [".AspNetCore.Culture"] = "c=en|uic=en",
        });
        var withCookie = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Contains(host.Text(SignInTestData.TextKeys.GoogleButton, "uk"), withQuery.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(SignInTestData.TextKeys.GoogleButton, "uk"), withCookie.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-015: the address Google returned is shown as it is, never translated.</summary>
    [Fact]
    public async Task TheEmailFromGoogle_IsShownAsItIs()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Contains(SignInTestData.AdminEmail, landing.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-015: no user-visible string of a page is hard-coded — the page carries only translated text.</summary>
    [Fact]
    public async Task TheSignInPage_CarriesOnlyTranslatedText()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Contains(host.Text(SignInTestData.TextKeys.SignInTitle, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(SignInTestData.TextKeys.GoogleButton, "uk"), page.Text, StringComparison.Ordinal);
    }
}
