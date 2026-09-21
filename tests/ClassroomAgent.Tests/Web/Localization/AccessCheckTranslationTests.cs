using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-011 AC-010: every sentence of the check comes from the translation files, in Ukrainian and English alike
/// (NFR-073; spec FR-012). Scope URIs and the technical account are data — rendered as they are in both languages.
/// </summary>
public sealed class AccessCheckTranslationTests(PostgreSqlFixture database)
{
    public static TheoryData<string> Keys
    {
        get
        {
            var keys = new TheoryData<string>();
            foreach (var key in AccessCheckTestData.TextKeys.All)
            {
                keys.Add(key);
            }

            return keys;
        }
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task EveryKey_ExistsInBothLanguages(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.NotEqual(string.Empty, host.Text(key, "uk"));
        Assert.NotEqual(string.Empty, host.Text(key, "en"));
    }

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
    /// Spec FR-005, US-010 OD-001: no message of this Story names a Workspace admin role — §7 item 10 is open, and the
    /// check proves reads succeed, not which roles are enough.
    /// </summary>
    [Theory]
    [MemberData(nameof(Keys))]
    public async Task NoMessage_NamesAWorkspaceAdminRole(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        foreach (var culture in new[] { "uk", "en" })
        {
            var text = host.Text(key, culture);
            Assert.All(
                ConnectionInstructionTestData.ForbiddenRoleNames,
                role => Assert.DoesNotContain(role, text, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>AC-010: the result renders in English for an account whose language is English, with the same data.</summary>
    [Fact]
    public async Task TheResult_RendersInEnglishForAnEnglishAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertAppUserAsync(ct, uiLanguage: "en");
        host.ControlPlaneHandler = WorkspaceConnectionHostExtensions.ApprovingChannel();
        host.ConfigureServices = AccessCheckHostExtensions.RegisterFakeProbe;
        host.Start();
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        await AccessCheckHostExtensions.SeedConnectionAsync(host, SeededConnection.Usable, ct);

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.VerdictAccessInPlace, "en"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(AccessCheckTestData.TextKeys.VerdictAccessInPlace, "uk"), page.Text, StringComparison.Ordinal);
        Assert.All(AccessCheckTestData.Scopes, scope => Assert.Contains(scope, page.Text, StringComparison.Ordinal));
    }
}
