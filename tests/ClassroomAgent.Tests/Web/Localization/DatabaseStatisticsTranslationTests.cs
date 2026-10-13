using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Data = ClassroomAgent.Tests.TestInfrastructure.DatabaseStatisticsTestData;
using Keys = ClassroomAgent.Tests.TestInfrastructure.DatabaseStatisticsTestData.Keys;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-024 AC-011, spec FR-012, NFR-073: every key of the page exists in Ukrainian and English with different non-empty
/// texts, the page and the home page entry render in both languages, and the failure guidance is offered in the UI
/// language.
/// </summary>
public sealed class DatabaseStatisticsTranslationTests(PostgreSqlFixture database)
{
    /// <summary>AC-011, FR-012: every new and reused key exists in uk and en, non-empty, and the two texts differ.</summary>
    [Fact]
    public async Task EveryKey_ExistsInBothLanguages_NonEmpty_AndTheTextsDiffer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        foreach (var key in Keys.New.Concat(Keys.Reused))
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

    /// <summary>AC-011: the page renders its labels, states and button in the chosen language, and the home page its section and entry.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task ThePage_RendersInTheChosenLanguage(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);
        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        string[] pageKeys =
        [
            Keys.Title, Keys.Total, Keys.Teachers, Keys.Students, Keys.CourseWorkItems, Keys.Submissions,
            Keys.MeetMeetings, Keys.MeetCodesAwaitingDecision, Keys.LastSuccessfulRun, Keys.State, Keys.LastFullRun,
            Keys.MeetLoadedUpTo, CourseListTestData.Keys.StatusActive, CourseListTestData.Keys.StatusArchived,
            CourseListTestData.Keys.StatusProvisioned, CourseListTestData.Keys.StatusDeclined,
            CourseListTestData.Keys.StatusSuspended, SynchronizationRequestTestData.TextKeys.Button,
        ];
        foreach (var key in pageKeys)
        {
            Assert.Contains(host.Text(key, language), page.Text, StringComparison.Ordinal);
        }

        Assert.Contains(host.Text(Keys.Section, language), landing.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.NavigationEntry, language), landing.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-011, AC-007: after a failed run the Dean's hint and the Admin's link text are shown in the UI language.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheFailureGuidance_IsShownInTheUiLanguage(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, dean) = await Mr.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        var admin = await JournalHostExtensions.SignInAsync(host, Actor.Admin, ct);
        if (language != UiLanguageTestData.Ukrainian)
        {
            var choice = await admin.ChooseLanguageAsync(SignInTestData.LandingPath, language, ct);
            Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        }

        await host.SeedSyncStateAsync(
            "failed", ct, lastSuccessfulRunAt: new DateTimeOffset(2026, 3, 1, 22, 30, 0, TimeSpan.Zero), failedStep: "meet");

        var deanPage = await dean.GetAsync(Data.Path, ct);
        var adminPage = await admin.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, deanPage.Status);
        Assert.Equal(HttpStatusCode.OK, adminPage.Status);
        Assert.Contains(host.Text(Keys.AskAdmin, language), deanPage.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.ConnectionPageLink, language), adminPage.Text, StringComparison.Ordinal);
    }
}
