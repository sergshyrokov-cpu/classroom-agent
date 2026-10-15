using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-038 AC-021, spec FR-018, NFR-073, TC-8: every key of the feature exists in Ukrainian and English with different
/// non-empty texts; the page, the confirmation and the home page entry render in the chosen language; names from Google
/// are shown as stored.
/// </summary>
public sealed class ErasureTranslationTests(PostgreSqlFixture database)
{
    /// <summary>AC-021, FR-018: every key exists in uk and en, non-empty, and the two texts differ.</summary>
    [Fact]
    public async Task EveryKey_ExistsInBothLanguages_NonEmpty_AndTheTextsDiffer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        foreach (var key in Keys.All)
        {
            var ukrainian = host.Text(key, "uk");
            var english = host.Text(key, "en");

            Assert.False(string.IsNullOrWhiteSpace(ukrainian), $"'{key}' is empty in uk.");
            Assert.False(string.IsNullOrWhiteSpace(english), $"'{key}' is empty in en.");
            Assert.NotEqual(key, ukrainian);
            Assert.NotEqual(key, english);
            Assert.NotEqual(ukrainian, english);
        }
    }

    /// <summary>AC-021: the page renders its title, search button and lifting title in the chosen language.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task ThePage_RendersInTheChosenLanguage(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, language: language);
        await using var _host = host;

        var page = await client.GetAsync(PagePath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(Keys.Title, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.SearchButton, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.LiftTitle, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-021: the confirmation renders in the chosen language, with the names exactly as stored.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheConfirmation_RendersInTheChosenLanguage_AndTheNamesAsStored(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, language: language);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);

        var page = await client.GetAsync(ConfirmationPath(world.Target), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(Keys.ExportWarning, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.Mark, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.EraseButton, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(PhysicsName, page.Text, StringComparison.Ordinal);
        Assert.Contains(TargetFullName, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-021: the home page entry renders in the chosen language.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheHomePageEntry_RendersInTheChosenLanguage(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, language: language);
        await using var _host = host;

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var block = System.Net.WebUtility.HtmlDecode(Inner(page.Body, Markup.AdminSettings) ?? string.Empty);
        Assert.Contains(host.Text(Keys.NavigationEntry, language), block, StringComparison.Ordinal);
    }
}
