using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-065 AC-020, spec FR-015, NFR-073, TC-8: every key of the entry exists in Ukrainian and English with different
/// non-empty texts; the entry block and its outcomes render in the chosen language; names from Google are shown as stored.
/// </summary>
public sealed class ErasureExclusionEntryTranslationTests(PostgreSqlFixture database)
{
    private const string FreshEmail = "new.person@school-one.example.test";

    /// <summary>AC-020, FR-015: every entry key exists in uk and en, non-empty, and the two texts differ.</summary>
    [Fact]
    public async Task EveryEntryKey_ExistsInBothLanguages_NonEmpty_AndTheTextsDiffer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        foreach (var key in Keys.Entry)
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

    /// <summary>AC-020: the page renders the entry block's title, explanation, label and button in the chosen language.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task ThePage_RendersTheEntryBlock_InTheChosenLanguage(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, language: language);
        await using var _host = host;

        var page = await client.GetAsync(PagePath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(Keys.EnterTitle, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.EnterExplanation, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.EnterLabel, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.EnterButton, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-020: the four outcomes render in the chosen language, a loaded person's name as stored.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheOutcomes_RenderInTheChosenLanguage(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, language: language);
        await using var _host = host;
        await host.SeedErasureWorldAsync(ct);

        var loaded = await client.PostEntryAsync(TargetEmail, ct);
        Assert.Equal(HttpStatusCode.OK, loaded.Status);
        Assert.Contains(host.Text(Keys.AlreadyLoaded, language), loaded.Text, StringComparison.Ordinal);
        Assert.Contains(TargetFullName, loaded.Text, StringComparison.Ordinal);

        var entered = await client.PostEntryAsync(FreshEmail, ct);
        Assert.Equal(HttpStatusCode.Redirect, entered.Status);
        var afterEntry = await client.GetAsync(PagePath, ct);
        Assert.Contains(host.Text(Keys.ExclusionEntered, language), afterEntry.Text, StringComparison.Ordinal);

        var excluded = await client.PostEntryAsync(FreshEmail, ct);
        Assert.Equal(HttpStatusCode.OK, excluded.Status);
        Assert.Contains(host.Text(Keys.AlreadyExcluded, language), excluded.Text, StringComparison.Ordinal);

        var invalid = await client.PostEntryAsync("not-an-email", ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
        Assert.Contains(host.Text(Keys.EmailInvalid, language), invalid.Text, StringComparison.Ordinal);
    }
}
