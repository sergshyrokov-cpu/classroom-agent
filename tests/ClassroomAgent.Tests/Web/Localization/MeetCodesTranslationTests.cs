using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-032 AC-017, spec FR-017, NFR-073: every key of the Meet meetings page exists in Ukrainian and English with
/// different non-empty texts, the page renders in both languages, and codes, emails and course names are shown as stored.
/// </summary>
public sealed class MeetCodesTranslationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task EveryKey_ExistsInBothLanguages_NonEmpty_AndTheTextsDiffer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        foreach (var key in Mc.Keys.All)
        {
            var ukrainian = host.Text(key, "uk");
            var english = host.Text(key, "en");

            Assert.False(string.IsNullOrWhiteSpace(ukrainian), $"'{key}' is empty in uk.");
            Assert.False(string.IsNullOrWhiteSpace(english), $"'{key}' is empty in en.");
            Assert.NotEqual(key, ukrainian);
            Assert.NotEqual(key, english);
            Assert.NotEqual(ukrainian, english);
        }
    }

    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task ThePage_RendersInTheChosenLanguage_AndShowsDataAsStored(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        const string organizer = "mc.teacher1@school-one.example.test";
        await host.SeedRosterCourseAsync("Test Course Alpha", [organizer], [], ct);
        await host.SeedMeetingAsync("tr-0001-aaa", organizer, new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero), ct);
        if (language == "en")
        {
            var choice = await client.ChooseLanguageAsync(SignInTestData.LandingPath, UiLanguageTestData.English, ct);
            Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        }

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);
        var page = await client.GetAsync(Mc.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Contains(host.Text(Mc.Keys.Title, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Mc.Keys.NavigationEntry, language), landing.Text, StringComparison.Ordinal);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains("tr-0001-aaa", text, StringComparison.Ordinal);
        Assert.Contains(organizer, text, StringComparison.Ordinal);
        Assert.Contains("Test Course Alpha", text, StringComparison.Ordinal);
    }
}
