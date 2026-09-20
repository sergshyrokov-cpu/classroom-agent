using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-009 AC-011: every string this Story adds comes from the translation files, in Ukrainian and English
/// alike, and the data it renders — the school's domain and the technical account — is never translated
/// (spec FR-011; NFR-073).
/// </summary>
public sealed class WorkspaceConnectionTranslationTests(PostgreSqlFixture database)
{
    public static TheoryData<string> Keys
    {
        get
        {
            var keys = new TheoryData<string>();
            foreach (var key in WorkspaceConnectionTestData.TextKeys.All)
            {
                keys.Add(key);
            }

            return keys;
        }
    }

    /// <summary>AC-011: every key exists in both languages and neither is empty.</summary>
    [Theory]
    [MemberData(nameof(Keys))]
    public async Task EveryKey_ExistsInBothLanguages(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.NotEqual(string.Empty, ukrainian);
        Assert.NotEqual(string.Empty, english);
    }

    /// <summary>AC-011: the two files carry the same set of keys — a key in one only fails here.</summary>
    [Fact]
    public async Task TheTwoFiles_CarryTheSameKeys()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.AllTexts("uk").Keys.Order(StringComparer.Ordinal).ToList();
        var english = host.AllTexts("en").Keys.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(ukrainian, english);
    }

    /// <summary>AC-011: the page renders in English for an account whose language is English.</summary>
    [Fact]
    public async Task ThePage_RendersInEnglishForAnEnglishAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = WorkspaceConnectionHostExtensions.ApprovingChannel();
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, InstallationTestHost.DefaultStart - TimeSpan.FromHours(1));
        await host.InsertAppUserAsync(ct, uiLanguage: "en");
        host.ControlPlaneHandler = channel;
        host.Start();
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var page = await client.OpenSettingsAsync(ct);

        Assert.Contains(host.Text(WorkspaceConnectionTestData.TextKeys.Title, "en"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-011: the stored address is data — it appears exactly as stored, untranslated.</summary>
    [Fact]
    public async Task TheStoredAddress_IsRenderedAsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await host.InsertWorkspaceConnectionAsync(ct, impersonationUserEmail: WorkspaceConnectionTestData.TechnicalAccount);

        var page = await client.OpenSettingsAsync(ct);

        Assert.Contains(WorkspaceConnectionTestData.TechnicalAccount, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-011: no refusal message of this Story is hard-coded — each is a key in both files.</summary>
    [Fact]
    public async Task EveryRefusalMessage_ComesFromTheTranslationFiles()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        string[] refusals =
        [
            WorkspaceConnectionTestData.TextKeys.RefusedDomainMismatch,
            WorkspaceConnectionTestData.TextKeys.RefusedImpersonationDomainMismatch,
            WorkspaceConnectionTestData.TextKeys.RefusedDomainNotConfirmed,
        ];

        foreach (var key in refusals)
        {
            Assert.NotEqual(host.Text(key, "uk"), host.Text(key, "en"));
        }
    }
}
