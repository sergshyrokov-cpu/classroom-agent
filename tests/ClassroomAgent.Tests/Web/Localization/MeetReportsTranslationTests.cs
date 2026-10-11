using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Keys = ClassroomAgent.Tests.TestInfrastructure.MeetReportsTestData.Keys;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-033 AC-016, spec FR-016, NFR-073: every key of the two pages exists in Ukrainian and English with different
/// non-empty texts, report 3 is «Участь студентів», both pages render in both languages, Google data is shown as stored,
/// and the average number of students follows the UI language's number format.
/// </summary>
public sealed class MeetReportsTranslationTests(PostgreSqlFixture database)
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

    /// <summary>FR-009, OD-003: the Ukrainian label of report 3 is «Участь студентів», the English one "Student participation".</summary>
    [Fact]
    public async Task ReportThree_IsCalledParticipationOfStudents()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.Equal("Участь студентів", host.Text(Keys.StudentsTitle, "uk"));
        Assert.Equal("Student participation", host.Text(Keys.StudentsTitle, "en"));
    }

    /// <summary>AC-016: the reports page renders in the chosen language and shows course, people and addresses as stored.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheReportsPage_RendersInTheChosenLanguage_AndShowsGoogleDataAsStored(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);
        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Contains(host.Text(Keys.NavigationEntry, language), landing.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.Title, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.StudentsTitle, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.SummaryTitle, language), page.Text, StringComparison.Ordinal);
        var other = language == "uk" ? "en" : "uk";
        Assert.DoesNotContain(host.Text(Keys.StudentsTitle, other), page.Text, StringComparison.Ordinal);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains(Mr.CourseName, text, StringComparison.Ordinal);
        Assert.Contains(Mr.StudentOneName, text, StringComparison.Ordinal);
        Assert.Contains(Mr.StudentOneEmail, text, StringComparison.Ordinal);
        Assert.Contains(Mr.TeacherName, text, StringComparison.Ordinal);
    }

    /// <summary>AC-016: the meeting page renders in the chosen language and shows the code and names as stored.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheMeetingPage_RendersInTheChosenLanguage_AndShowsGoogleDataAsStored(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(
            MeetReportsTestData.MeetingUrl(
                world.MeetingA.ToString(CultureInfo.InvariantCulture), world.CourseId,
                MeetReportsTestData.Period.FromText, MeetReportsTestData.Period.ToText),
            ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Contains(host.Text(Keys.MeetingTitle, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.MeetingBack, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.MarkCourseStudent, language), page.Text, StringComparison.Ordinal);
        var other = language == "uk" ? "en" : "uk";
        Assert.DoesNotContain(host.Text(Keys.MeetingTitle, other), page.Text, StringComparison.Ordinal);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains(Mr.CourseName, text, StringComparison.Ordinal);
        Assert.Contains(Mr.Codes.Linked, text, StringComparison.Ordinal);
        Assert.Contains(Mr.StudentOneName, text, StringComparison.Ordinal);
        Assert.Contains(Mr.StudentOneEmail, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-016, I-6: three meetings with 2, 0 and 3 course students average 5/3 — "1,7" in Ukrainian, "1.7" in English.
    /// </summary>
    [Theory]
    [InlineData("uk", "1,7", "1.7")]
    [InlineData("en", "1.7", "1,7")]
    public async Task TheAverageNumberOfStudents_FollowsTheUiLanguageNumberFormat(string language, string expected, string notExpected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        var course = await SeedThreeMeetingsAsync(host, ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(course), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        var text = Mc.VisibleText(page.Body);
        Assert.Matches(new Regex(@"(?<![\d.,])" + Regex.Escape(expected) + @"(?!\d)"), text);
        Assert.DoesNotMatch(new Regex(@"(?<![\d.,])" + Regex.Escape(notExpected) + @"(?!\d)"), text);
    }

    /// <summary>Three students; meetings with students One and Two, with nobody, and with all three.</summary>
    private static async Task<long> SeedThreeMeetingsAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await CourseRows.InsertCourseAsync(
            host, ct, googleId: CourseTestData.CourseId(1), name: Mr.CourseName);
        await Mr.AddMemberAsync(host, course, 1, Mr.TeacherEmail, Mr.TeacherName, CourseTestData.Roles.Teacher, ct);
        await Mr.AddMemberAsync(host, course, 2, Mr.StudentOneEmail, Mr.StudentOneName, CourseTestData.Roles.Student, ct);
        await Mr.AddMemberAsync(host, course, 3, Mr.StudentTwoEmail, Mr.StudentTwoName, CourseTestData.Roles.Student, ct);
        await Mr.AddMemberAsync(host, course, 4, Mr.StudentThreeEmail, Mr.StudentThreeName, CourseTestData.Roles.Student, ct);
        await MeetLinkRows.InsertLinkAsync(
            host,
            MeetLinkRows.LinkRow.Automatic(Mr.Codes.Linked, course, new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero)),
            ct);
        await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, MeetReportsTestData.Period.Local(10, 5, 10), TimeSpan.FromMinutes(45), ct,
            new Mr.Connection(Mr.StudentOneEmail, 2400), new Mr.Connection(Mr.StudentTwoEmail, 2400));
        await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, MeetReportsTestData.Period.Local(10, 12, 10), TimeSpan.FromMinutes(30), ct,
            new Mr.Connection(Mr.TeacherEmail, 1800));
        await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, MeetReportsTestData.Period.Local(10, 19, 10), TimeSpan.FromMinutes(60), ct,
            new Mr.Connection(Mr.StudentOneEmail, 3000),
            new Mr.Connection(Mr.StudentTwoEmail, 3000),
            new Mr.Connection(Mr.StudentThreeEmail, 3000));
        return course;
    }
}
