using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-012 AC-015: every string of the two areas comes from the translation files, in Ukrainian and English
/// alike (NFR-073, TC-8). The key list in <see cref="DeanAccountTestData.TextKeys"/> is the contract: a screen
/// that needs a key nobody translated fails here rather than in a browser.
/// </summary>
public sealed class DeanAccountTranslationTests(PostgreSqlFixture database)
{
    public static TheoryData<string> ContractKeys => new(DeanAccountTestData.TextKeys.All);

    /// <summary>AC-015: every contract key resolves in both languages, to two different texts.</summary>
    [Theory]
    [MemberData(nameof(ContractKeys))]
    public async Task EveryKey_IsTranslatedInBothLanguages(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.False(string.IsNullOrWhiteSpace(ukrainian), $"uk: {key}");
        Assert.False(string.IsNullOrWhiteSpace(english), $"en: {key}");
        Assert.NotEqual(key, ukrainian);
        Assert.NotEqual(key, english);
    }

    /// <summary>
    /// AC-010, S-05: the common refusal and the "account disabled" message are two **different** keys with two
    /// different texts — the only distinction SC-2 allows, and one the sequence must not blur.
    /// </summary>
    [Fact]
    public async Task TheCommonRefusalAndTheDisabledMessage_AreDifferent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var refused = host.Text(DeanAccountTestData.TextKeys.SignInRefused, "uk");
        var disabled = host.Text(DeanAccountTestData.TextKeys.SignInAccountDisabled, "uk");

        Assert.NotEqual(refused, disabled);
    }

    /// <summary>AC-015: the two files hold the same key set, so a language cannot fall behind (TC-8).</summary>
    [Fact]
    public async Task BothFiles_HoldTheSameKeys()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.AllTexts("uk");
        var english = host.AllTexts("en");

        Assert.Equal(
            ukrainian.Keys.Order(StringComparer.Ordinal),
            english.Keys.Order(StringComparer.Ordinal));
    }
}
