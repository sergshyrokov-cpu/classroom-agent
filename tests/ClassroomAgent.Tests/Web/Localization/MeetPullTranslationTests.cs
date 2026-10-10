using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-031 AC-014, FR-014, NFR-073, TC-8: every string this Story adds to the "Last synchronization" block — the
/// watermark line, the "not loaded yet" text and the two step names — comes from the translation files, in Ukrainian
/// and English alike.
/// </summary>
public sealed class MeetPullTranslationTests(PostgreSqlFixture database)
{
    public static TheoryData<string> Keys
    {
        get
        {
            var keys = new TheoryData<string>();
            foreach (var key in MeetTestData.Keys.All)
            {
                keys.Add(key);
            }

            return keys;
        }
    }

    /// <summary>AC-014: every key exists in both languages, neither text is empty, and the two differ.</summary>
    [Theory]
    [MemberData(nameof(Keys))]
    public async Task EveryKey_ExistsInBothLanguages_AndTheTextsDiffer(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.False(string.IsNullOrWhiteSpace(ukrainian), $"'{key}' is empty in the uk file.");
        Assert.False(string.IsNullOrWhiteSpace(english), $"'{key}' is empty in the en file.");
        Assert.NotEqual(key, ukrainian);
        Assert.NotEqual(key, english);
        Assert.NotEqual(ukrainian, english);
    }
}
