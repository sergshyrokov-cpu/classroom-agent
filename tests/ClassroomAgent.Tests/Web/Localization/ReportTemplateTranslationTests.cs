using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>US-027 AC-010: every new sentence of the templates and the report exists in both languages (NFR-073).</summary>
public sealed class ReportTemplateTranslationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task EveryNewKey_ExistsInBothLanguages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        foreach (var key in ReportTemplateTestData.TextKeys.All)
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
    public async Task TheBuiltInName_IsTranslated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.Equal("Академічний журнал", host.Text(ReportTemplateTestData.TextKeys.BuiltInName, "uk"));
        Assert.Equal("Academic journal", host.Text(ReportTemplateTestData.TextKeys.BuiltInName, "en"));
    }
}
