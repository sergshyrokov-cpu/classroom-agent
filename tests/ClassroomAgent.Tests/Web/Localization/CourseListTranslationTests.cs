using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Keys = ClassroomAgent.Tests.TestInfrastructure.CourseListTestData.Keys;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-020 AC-016, spec FR-017, NFR-073: every key of the page exists in Ukrainian and English with different non-empty
/// texts, the page renders in both languages, Google data is shown as stored, the status choice is offered in the UI
/// language and a teacher without a name gets the translated placeholder.
/// </summary>
public sealed class CourseListTranslationTests(PostgreSqlFixture database)
{
    private static readonly string[] StatusValues = ["active", "archived", "provisioned", "declined", "suspended", "all"];

    [Fact]
    public async Task EveryKey_ExistsInBothLanguages_NonEmpty_AndTheTextsDiffer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        foreach (var key in Keys.All)
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

    /// <summary>AC-016: the page renders in the chosen language and shows course, section and teachers as stored.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task ThePage_RendersInTheChosenLanguage_AndShowsGoogleDataAsStored(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);
        var page = await client.GetAsync(CourseListTestData.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Contains(host.Text(Keys.NavigationEntry, language), landing.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.Title, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.ColumnTeachers, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.StatusActive, language), page.Text, StringComparison.Ordinal);
        var other = language == "uk" ? "en" : "uk";
        Assert.DoesNotContain(host.Text(Keys.ColumnTeachers, other), page.Text, StringComparison.Ordinal);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains(Cl.PhysicsName, text, StringComparison.Ordinal);
        Assert.Contains(Cl.PhysicsSection, text, StringComparison.Ordinal);
        Assert.Contains(Cl.ShevchenkoLabel, text, StringComparison.Ordinal);
        Assert.Contains(Cl.UkrainkaLabel, text, StringComparison.Ordinal);
    }

    /// <summary>AC-016, FR-004: the five states and "all" are offered in the UI language, in the display order.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheStatusChoice_OffersTheStatesInTheUiLanguage_InOrder(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);

        var page = await client.GetAsync(CourseListTestData.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var options = Cl.StatusOptions(page.Body);
        Assert.Equal(Keys.Statuses.Select(k => host.Text(k, language)), options.Select(o => o.Label));
        Assert.Equal(StatusValues, options.Select(o => o.Value));
    }

    /// <summary>AC-016, FR-007: a teacher with no name and no email gets the translated placeholder.</summary>
    [Fact]
    public async Task ATeacherWithoutAName_IsShownWithTheTranslatedPlaceholder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, "en", ct);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);
        var course = await host.InsertCourseAsync(50, "Безіменний курс", ct);
        var person = await host.InsertPersonAsync(50, null, null, null, ct);
        await host.AddTeacherAsync(course, person, ct);

        var page = await client.GetAsync(CourseListTestData.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(Keys.TeacherNoName, "en"), Mc.VisibleText(page.Body), StringComparison.Ordinal);
    }
}
