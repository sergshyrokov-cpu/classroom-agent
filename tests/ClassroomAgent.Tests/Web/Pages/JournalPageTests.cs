using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-025 over HTTP: the form, the journal, the draft switch, the 400 and 404 pages, HTML encoding, the empty states,
/// the English rendering and the home page link (AC-001, 004, 005, 006, 009, 011, 012; api-design §2–§5).
/// </summary>
public sealed class JournalPageTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task WithoutACourse_TheFormIsShown_WithTheDefaultPeriodAndTheCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(JournalTestData.Period.FromText, Html.InputValue(page.Body, JournalTestData.Parameters.From));
        Assert.Equal(JournalTestData.Period.ToText, Html.InputValue(page.Body, JournalTestData.Parameters.To));
        Assert.Contains(SeededJournal.CourseName, page.Text, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.Section, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.StudentName, page.Text, StringComparison.Ordinal);
        Assert.True(seeded.CourseId > 0);
    }

    [Fact]
    public async Task TheJournal_ShowsTheColumnsAndTheStudents_ButNotTheOctoberItemOrTheTeacher()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(SeededJournal.GradedTitle, page.Text, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.UngradedTitle, page.Text, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.MaterialTitle, page.Text, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.StudentName, page.Text, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.SilentStudentName, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.OctoberTitle, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.TeacherName, page.Text, StringComparison.Ordinal);
        Assert.Contains("8,5", page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Journal.Cell.NotAssigned", "uk"), page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADraftGrade_IsShownInTheFullView_AndNotInTheShortView()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var draftStudent = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(10), fullName: "Test Student Draft");
        await CourseRows.InsertMembershipAsync(host, seeded.CourseId, draftStudent, ct);
        await CourseWorkRows.InsertSubmissionAsync(
            host, seeded.GradedItemId, draftStudent, ct, googleId: CourseWorkTestData.SubmissionId(10),
            state: CourseWorkTestData.StateCodes.TurnedIn, draftGrade: 6.25m);

        var full = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId, "full"), ct);
        var shortView = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId, "short"), ct);

        Assert.Equal(HttpStatusCode.OK, full.Status);
        Assert.Equal(HttpStatusCode.OK, shortView.Status);
        Assert.Contains("Test Student Draft", full.Text, StringComparison.Ordinal);
        Assert.Contains("6,25", full.Text, StringComparison.Ordinal);
        Assert.Contains("Test Student Draft", shortView.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("6,25", shortView.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EachView_LinksToTheOther()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var full = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);
        var shortView = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId, "short"), ct);

        Assert.Contains("view=short", full.Text, StringComparison.Ordinal);
        Assert.Contains("view=full", shortView.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMalformedCourseId_Is400_WithTheFormAndTheMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.Path + "?courseId=abc", ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.True(Html.HasInput(page.Body, JournalTestData.Parameters.From));
        Assert.Contains(host.Text("Journal.Validation.CourseMalformed", "uk"), page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMalformedDate_Is400_AndTheValueIsNotEchoed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(
            JournalTestData.Url(seeded.CourseId, from: "zz-not-a-date-zz", to: JournalTestData.Period.ToText), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.True(Html.HasInput(page.Body, JournalTestData.Parameters.From));
        Assert.DoesNotContain("zz-not-a-date-zz", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("zz-not-a-date-zz", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownCourse_Is404_WithTheFormAndTheMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(987654), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.True(Html.HasInput(page.Body, JournalTestData.Parameters.From));
        Assert.Contains(host.Text("Journal.Validation.CourseUnknown", "uk"), page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownParameter_IsIgnored_AndNotEchoed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId) + "&marker=qq-unknown-qq", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(SeededJournal.StudentName, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("qq-unknown-qq", page.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TitlesAndRawStates_AreHtmlEncoded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var boldItem = await CourseWorkRows.InsertCourseWorkAsync(
            host, seeded.CourseId, ct, googleId: CourseWorkTestData.ItemId(20), title: "<b>Test Bold</b>",
            itemDate: JournalTestData.Period.Early, maxPoints: 10m);
        await CourseWorkRows.InsertSubmissionAsync(
            host, boldItem, seeded.StudentId, ct, googleId: CourseWorkTestData.SubmissionId(20),
            state: CourseWorkTestData.StateCodes.Unrecognised, rawState: "<i>TEST_RAW</i>");

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("&lt;b&gt;Test Bold&lt;/b&gt;", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Test Bold</b>", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("<i>TEST_RAW</i>", page.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APeriodWithoutItems_ShowsTheNoColumnsMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.Url(seeded.CourseId, "2026-01-01", "2026-01-31"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text("Journal.Empty.NoColumns", "uk"), page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACourseWithItemsButNoStudents_ShowsTheNoRowsMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedJournalAsync(ct);
        var second = await CourseRows.InsertCourseAsync(
            host, ct, googleId: CourseTestData.CourseId(2), name: CourseTestData.CourseName(2));
        await CourseWorkRows.InsertCourseWorkAsync(
            host, second, ct, googleId: CourseWorkTestData.ItemId(30), itemDate: JournalTestData.Period.Early);

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(second), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text("Journal.Empty.NoRows", "uk"), page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEnglishUser_SeesEnglishText_AndTheDecimalPoint_ButTheSameGoogleData()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertAppUserAsync(ct, uiLanguage: "en");
        host.ControlPlaneHandler = WorkspaceConnectionHostExtensions.ApprovingChannel();
        host.Start();
        var (client, callback) = await host.SignInWithGoogleAsync(ct);
        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text("Journal.Cell.NotAssigned", "en"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("Journal.Cell.NotAssigned", "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains("8.5", page.Text, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.CourseName, page.Text, StringComparison.Ordinal);
        Assert.Contains(SeededJournal.GradedTitle, page.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Actor.Admin, ReadOnlyModeHost.Cause.NotReadOnly)]
    [InlineData(Actor.Dean, ReadOnlyModeHost.Cause.NotReadOnly)]
    [InlineData(Actor.Admin, ReadOnlyModeHost.Cause.Suspended)]
    [InlineData(Actor.Dean, ReadOnlyModeHost.Cause.Suspended)]
    public async Task TheHomePage_LinksToTheJournal(Actor actor, ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct, cause);
        await using var _host = host;

        var page = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("href=\"/workspace/journal\"", page.Body, StringComparison.Ordinal);
    }
}
