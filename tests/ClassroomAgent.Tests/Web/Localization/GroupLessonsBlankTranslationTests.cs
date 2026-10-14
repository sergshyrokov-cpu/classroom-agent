using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-043 FR-012, NFR-073 (api-design §5): every new validation message exists in Ukrainian and English.
/// </summary>
public sealed class GroupLessonsBlankTranslationTests(PostgreSqlFixture database)
{
    /// <summary>FR-012: the new messages exist in both languages and differ.</summary>
    [Theory]
    [InlineData("Export.Validation.LayoutInvalid")]
    [InlineData("Export.Validation.StudentsPerCopyInvalid")]
    [InlineData("Export.Validation.LessonsPerCopyInvalid")]
    [InlineData("Export.Validation.TooManySheets")]
    public async Task EveryNewMessage_ExistsInBothLanguages(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.NotEqual(key, ukrainian);
        Assert.NotEqual(key, english);
        Assert.NotEqual(ukrainian, english);
    }

    /// <summary>OD-011 a: the too-many-sheets message names the limit of 100 in both languages.</summary>
    [Fact]
    public async Task TheTooManySheetsMessage_NamesTheLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.Contains("100", host.Text("Export.Validation.TooManySheets", "uk"), StringComparison.Ordinal);
        Assert.Contains("100", host.Text("Export.Validation.TooManySheets", "en"), StringComparison.Ordinal);
    }
}
