using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-010 AC-010: every sentence of the instruction comes from the translation files, in Ukrainian and English
/// alike — NFR-073 names the super-admin instruction as translated text, and it is the longest such text in the
/// program so far (spec FR-012, I-8). The client ID, the domain and the scope identifiers are data: rendered as
/// they are and never translated.
/// </summary>
public sealed class ConnectionInstructionTranslationTests(PostgreSqlFixture database)
{
    public static TheoryData<string> Keys
    {
        get
        {
            var keys = new TheoryData<string>();
            foreach (var key in ConnectionInstructionTestData.TextKeys.All)
            {
                keys.Add(key);
            }

            return keys;
        }
    }

    /// <summary>AC-010: every key exists in both languages and neither entry is empty.</summary>
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

    /// <summary>
    /// AC-010: the two entries of a key differ — a key whose English value is still the Ukrainian text, or a
    /// missing resource echoing the key name back, fails here.
    /// </summary>
    [Theory]
    [MemberData(nameof(Keys))]
    public async Task EveryKey_IsReallyTranslated(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.NotEqual(ukrainian, english);
        Assert.NotEqual(key, ukrainian);
        Assert.NotEqual(key, english);
    }

    /// <summary>
    /// AC-010, spec I-8: the instruction is broken into one key per paragraph or statement, so a change to one
    /// sentence does not invalidate both translations wholesale. No single entry carries the whole instruction.
    /// </summary>
    [Fact]
    public async Task NoSingleEntry_CarriesTheWholeInstruction()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        string[] cultures = ["uk", "en"];

        foreach (var culture in cultures)
        {
            foreach (var key in ConnectionInstructionTestData.TextKeys.All)
            {
                Assert.True(
                    host.Text(key, culture).Length <= 600,
                    $"'{key}' in '{culture}' is {host.Text(key, culture).Length} characters — one key per paragraph (spec I-8).");
            }
        }
    }

    /// <summary>AC-010: the instruction renders in English for an account whose language is English.</summary>
    [Fact]
    public async Task TheInstruction_RendersInEnglishForAnEnglishAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = WorkspaceConnectionHostExtensions.ApprovingChannel();
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, InstallationTestHost.DefaultStart - TimeSpan.FromHours(1));
        await host.InsertAppUserAsync(ct, uiLanguage: "en");
        host.ControlPlaneHandler = channel;
        host.Start();
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var page = await client.OpenInstructionAsync(ct);

        Assert.Contains(host.Text(ConnectionInstructionTestData.TextKeys.Title, "en"), page.Text, StringComparison.Ordinal);
        Assert.Contains(
            host.Text(ConnectionInstructionTestData.TextKeys.TechnicalAccountReadOnlyRoles, "en"),
            page.Text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-010: the English page carries the same **data** — the client ID, the domain and the scopes are not
    /// translated, re-cased or reformatted for another language.
    /// </summary>
    [Fact]
    public async Task TheDataIsTheSameInBothLanguages()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = WorkspaceConnectionHostExtensions.ApprovingChannel();
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, InstallationTestHost.DefaultStart - TimeSpan.FromHours(1));
        await host.InsertAppUserAsync(ct, uiLanguage: "en");
        host.ControlPlaneHandler = channel;
        host.Start();
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var page = await client.OpenInstructionAsync(ct);

        Assert.Contains(ConnectionInstructionTestData.ClientId, page.Text, StringComparison.Ordinal);
        Assert.Contains(ConnectionInstructionTestData.Domain, page.Text, StringComparison.Ordinal);
        Assert.All(
            ConnectionInstructionTestData.Scopes,
            scope => Assert.Contains(scope, page.Text, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC-010, spec VR-004: the client ID is rendered exactly as stored — no formatting, no grouping, no
    /// truncation. A super-admin must be able to paste it into the Google console as shown (db-design §2.2).
    /// </summary>
    [Fact]
    public async Task TheClientId_IsRenderedExactlyAsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);
        var text = ConnectionInstructionHostExtensions.HandedOverText(page);

        Assert.Contains(ConnectionInstructionTestData.ClientId, text, StringComparison.Ordinal);
        char[] separators = [' ', ',', '.', '-', ' '];
        foreach (var separator in separators)
        {
            Assert.DoesNotContain(Grouped(ConnectionInstructionTestData.ClientId, separator), text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The same digits broken into groups of three — the shape a "readable" formatting change would produce, and
    /// the shape that makes the value useless to paste into the Google console.
    /// </summary>
    private static string Grouped(string clientId, char separator)
    {
        var groups = new List<string>();
        for (var start = clientId.Length; start > 0; start -= 3)
        {
            groups.Insert(0, clientId[Math.Max(0, start - 3)..start]);
        }

        return string.Join(separator, groups);
    }
}
