using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Data = ClassroomAgent.Tests.TestInfrastructure.TeacherListTestData;
using Keys = ClassroomAgent.Tests.TestInfrastructure.TeacherListTestData.Keys;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;
using Tl = ClassroomAgent.Tests.TestInfrastructure.TeacherListHostExtensions;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-022 AC-012, spec FR-014, NFR-073: every key of the page exists in Ukrainian and English with different non-empty
/// texts, the page renders in both languages, Google data is shown as stored and the status choice is offered in the UI
/// language.
/// </summary>
public sealed class TeacherListTranslationTests(PostgreSqlFixture database)
{
    private static readonly string[] StatusValues = ["active", "archived", "provisioned", "declined", "suspended", "all"];

    /// <summary>AC-012, FR-014: every new and reused key exists in uk and en, non-empty, and the two texts differ.</summary>
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

    /// <summary>AC-012: the page renders in the chosen language and shows the teachers' names and emails as stored.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task ThePage_RendersInTheChosenLanguage_AndShowsGoogleDataAsStored(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);
        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Contains(host.Text(Keys.NavigationEntry, language), landing.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.Title, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.ColumnCourses, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.ColumnEmail, language), page.Text, StringComparison.Ordinal);
        var other = language == "uk" ? "en" : "uk";
        Assert.DoesNotContain(host.Text(Keys.ColumnCourses, other), page.Text, StringComparison.Ordinal);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains(Tl.ShevchenkoLabel, text, StringComparison.Ordinal);
        Assert.Contains(Tl.UkrainkaLabel, text, StringComparison.Ordinal);
        Assert.Contains(Tl.ShevchenkoEmail, text, StringComparison.Ordinal);
    }

    /// <summary>AC-012: the five states and "all" are offered in the UI language, in the display order.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheStatusChoice_OffersTheStatesInTheUiLanguage_InOrder(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var options = Cl.StatusOptions(page.Body);
        Assert.Equal(CourseListTestData.Keys.Statuses.Select(k => host.Text(k, language)), options.Select(o => o.Label));
        Assert.Equal(StatusValues, options.Select(o => o.Value));
    }

    /// <summary>AC-012, FR-008: the empty message is shown in the UI language.</summary>
    [Fact]
    public async Task TheEmptyMessage_IsShownInEnglish()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, "en", ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(Keys.Empty, "en"), Mc.VisibleText(page.Body), StringComparison.Ordinal);
    }
}
