using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>US-042 AC-011, FR-010: the two new refusal sentences exist in both languages and differ (NFR-073).</summary>
public sealed class ReportNameSourceTranslationTests(PostgreSqlFixture database)
{
    [Theory]
    [InlineData("ReportTemplate.Validation.NameSourceInvalid")]
    [InlineData("Report.Validation.NameSourceMalformed")]
    public async Task EveryNewKey_ExistsInBothLanguages(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.NotEqual(string.Empty, ukrainian);
        Assert.NotEqual(string.Empty, english);
        Assert.NotEqual(key, ukrainian);
        Assert.NotEqual(key, english);
        Assert.NotEqual(ukrainian, english);
    }
}
