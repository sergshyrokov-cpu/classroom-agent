using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>US-019 AC-007: every sentence of the feature comes from the translation files, in both languages (NFR-073).</summary>
public sealed class SynchronizationRequestTranslationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task EveryKey_ExistsInBothLanguages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        foreach (var key in SynchronizationRequestTestData.TextKeys.All)
        {
            var ukrainian = host.Text(key, "uk");
            var english = host.Text(key, "en");

            Assert.NotEqual(string.Empty, ukrainian);
            Assert.NotEqual(string.Empty, english);
            Assert.NotEqual(key, ukrainian);
            Assert.NotEqual(key, english);
            Assert.NotEqual(ukrainian, english);
        }
    }

    [Fact]
    public async Task AnEnglishUser_SeesTheEnglishMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertAppUserAsync(ct, uiLanguage: "en");
        host.ControlPlaneHandler = WorkspaceConnectionHostExtensions.ApprovingChannel();
        host.ConfigureServices = RegisterFakeRequests;
        host.Start();
        var (client, callback) = await host.SignInWithGoogleAsync(ct);
        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        await AccessCheckHostExtensions.SeedConnectionAsync(host, SeededConnection.Usable, ct);

        var press = await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);
        Assert.Equal(HttpStatusCode.Redirect, press.Status);
        var page = await client.GetAsync(press.LocationPath ?? SynchronizationRequestTestData.AdminReturnPage, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(SynchronizationRequestTestData.TextKeys.Requested, "en"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(SynchronizationRequestTestData.TextKeys.Requested, "uk"), page.Text, StringComparison.Ordinal);
    }
}
