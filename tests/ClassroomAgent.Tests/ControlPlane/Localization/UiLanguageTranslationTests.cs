using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Localization;

/// <summary>
/// US-039 AC-008 in the Control Plane: every string the switcher adds is in both translation files, and the two
/// language labels are each language's own name in both files (spec FR-010, NFR-073).
/// </summary>
public sealed class UiLanguageTranslationTests(PostgreSqlFixture database)
{
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task EverySwitcherKey_IsTranslated(string culture)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var texts = host.AllTexts(culture);

        Assert.All(UiLanguageTestData.Keys.All, key =>
        {
            Assert.True(texts.ContainsKey(key), $"'{key}' is missing from the {culture} file.");
            Assert.False(string.IsNullOrWhiteSpace(texts[key]), $"'{key}' is empty in the {culture} file.");
        });
    }

    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheLanguageLabels_AreTheSelfNames_InBothFiles(string culture)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        Assert.Equal(UiLanguageTestData.UkrainianSelfName, host.Text(UiLanguageTestData.Keys.UkrainianLabel, culture));
        Assert.Equal(UiLanguageTestData.EnglishSelfName, host.Text(UiLanguageTestData.Keys.EnglishLabel, culture));
    }

    [Fact]
    public async Task TheSwitchersAccessibleName_DiffersBetweenTheLanguages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        Assert.NotEqual(
            host.Text(UiLanguageTestData.Keys.SwitcherName, "uk"),
            host.Text(UiLanguageTestData.Keys.SwitcherName, "en"));
    }
}
