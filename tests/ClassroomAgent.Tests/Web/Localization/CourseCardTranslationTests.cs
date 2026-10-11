using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cc = ClassroomAgent.Tests.TestInfrastructure.CourseCardHostExtensions;
using Keys = ClassroomAgent.Tests.TestInfrastructure.CourseCardTestData.Keys;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-021 AC-012, spec FR-016, NFR-073: every key of the card exists in Ukrainian and English with different non-empty
/// texts, the card renders in both languages and Google data is shown as stored.
/// </summary>
public sealed class CourseCardTranslationTests(PostgreSqlFixture database)
{
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

    /// <summary>AC-012: the card renders in the chosen language and shows course and people data as stored.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheCard_RendersInTheChosenLanguage_AndShowsGoogleDataAsStored(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        var world = await host.SeedCourseCardAsync(ct);
        var culture = CultureInfo.GetCultureInfo(language);

        var page = await client.GetAsync(CourseCardTestData.Path(world.Physics), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        foreach (var key in new[]
                 {
                     Keys.SectionTeachers, Keys.SectionStudents, Keys.SectionLeavers, Keys.Owner, Keys.RoleTeacher,
                     Keys.RoleStudent, Keys.NoName, Keys.LinkJournal, Keys.LinkMeetReports, Keys.LinkCourses, Keys.FieldStatus,
                     "Courses.Status.Active",
                 })
        {
            Assert.Contains(host.Text(key, language), text, StringComparison.Ordinal);
        }

        Assert.Contains(Cc.PhysicsName, text, StringComparison.Ordinal);
        Assert.Contains(Cc.PhysicsSection, text, StringComparison.Ordinal);
        Assert.Contains(Cc.ShevchenkoLabel, text, StringComparison.Ordinal);
        Assert.Contains(Cc.ShevchenkoEmail, text, StringComparison.Ordinal);
        var updated = Cc.FieldText(page.Body, CourseCardTestData.Fields.UpdatedAt) ?? string.Empty;
        Assert.Contains(
            new DateTime(2026, 4, 1).ToString(culture.DateTimeFormat.ShortDatePattern, culture),
            updated,
            StringComparison.Ordinal);
    }
}
