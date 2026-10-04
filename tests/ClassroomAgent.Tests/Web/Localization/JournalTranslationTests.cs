using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>US-025 AC-012: every sentence of the journal comes from the translation files, in both languages (NFR-073).</summary>
public sealed class JournalTranslationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task EveryKey_ExistsInBothLanguages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        foreach (var key in JournalTestData.TextKeys.All)
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
}
