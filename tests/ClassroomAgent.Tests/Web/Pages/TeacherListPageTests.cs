using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using CKeys = ClassroomAgent.Tests.TestInfrastructure.CourseListTestData.Keys;
using Data = ClassroomAgent.Tests.TestInfrastructure.TeacherListTestData;
using Keys = ClassroomAgent.Tests.TestInfrastructure.TeacherListTestData.Keys;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Tl = ClassroomAgent.Tests.TestInfrastructure.TeacherListHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-022 over HTTP: the row, the default and chosen status, the name rule, the link to the course list, paging, the
/// address and the language switcher, the empty state, the home page entry, the 400 page, HTML encoding and no Google
/// call (AC-001 … AC-008, AC-011, AC-013, AC-015, AC-016). Rows are seeded by SQL; the markup hooks are
/// <see cref="TeacherListTestData.Markup"/>.
/// </summary>
public sealed class TeacherListPageTests(PostgreSqlFixture database)
{
    private const string Secret = "zz-secret-zz";

    private async Task<(InstallationTestHost Host, FormClient Client, Tl.SeededWorld World)> StartSeededAsync(
        CancellationToken ct, Actor actor = Actor.Admin)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        var world = await host.SeedTeacherListAsync(ct);
        return (host, client, world);
    }

    private static string RowText(PageResponse page, long participantId)
    {
        var text = Tl.RowText(page.Body, participantId);
        Assert.True(text is not null, $"Participant {participantId} is not listed.");
        return " " + text + " ";
    }

    /// <summary>AC-001, AC-003: the default list is the ACTIVE teachers, each with name, email and the number of active courses.</summary>
    [Fact]
    public async Task TheDefaultList_ShowsActiveTeachers_WithNameEmailAndCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        NoStore.Assert(page, "the teacher list");
        Assert.True(Tl.HasGetForm(page.Body), "The status form is missing or does not submit by GET to the list.");
        Assert.Equal([world.Ukrainka, world.Shevchenko], Tl.RowIds(page.Body));
        Assert.Equal("active", Cl.SelectedValue(page.Body, Data.Parameters.Status));
        var shevchenko = RowText(page, world.Shevchenko);
        Assert.Contains(Tl.ShevchenkoLabel, shevchenko, StringComparison.Ordinal);
        Assert.Contains(Tl.ShevchenkoEmail, shevchenko, StringComparison.Ordinal);
        Assert.Contains(" 2 ", shevchenko, StringComparison.Ordinal);
        Assert.Contains(" 1 ", RowText(page, world.Ukrainka), StringComparison.Ordinal);
        var text = Mc.VisibleText(page.Body);
        Assert.DoesNotContain(Tl.KovalLabel, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Tl.FrankoLabel, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Tl.StudentLabel, text, StringComparison.Ordinal);
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>AC-002: a chosen status, or "all", lists those teachers with the counts of that status.</summary>
    [Fact]
    public async Task AChosenStatus_OrAll_ListsThoseTeachers_WithCountsOfThatStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var archived = await client.GetAsync(Data.Url(status: "archived"), ct);
        var all = await client.GetAsync(Data.Url(status: "ALL"), ct);
        var declined = await client.GetAsync(Data.Url(status: "declined"), ct);

        Assert.Equal([world.Koval, world.Ukrainka], Tl.RowIds(archived.Body));
        Assert.Contains(" 1 ", RowText(archived, world.Ukrainka), StringComparison.Ordinal);
        Assert.Equal("archived", Cl.SelectedValue(archived.Body, Data.Parameters.Status));
        Assert.Equal([world.Koval, world.Ukrainka, world.Shevchenko], Tl.RowIds(all.Body));
        Assert.Contains(" 2 ", RowText(all, world.Ukrainka), StringComparison.Ordinal);
        Assert.Equal("all", Cl.SelectedValue(all.Body, Data.Parameters.Status));
        Assert.Equal(HttpStatusCode.OK, declined.Status);
        Assert.Empty(Tl.RowIds(declined.Body));
    }

    /// <summary>AC-003, AC-011: a teacher without a name is shown by the email, or by the placeholder; emails are shown as stored.</summary>
    [Fact]
    public async Task ATeacherWithoutAName_IsShownByEmail_OrThePlaceholder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await host.InsertCourseAsync(1, "Курс", ct);
        var byEmail = await host.InsertPersonAsync(1, null, null, "zoe@school.example", ct);
        var nothing = await host.InsertPersonAsync(2, null, null, null, ct);
        var mixed = await host.InsertPersonAsync(3, "Ковальчук", "Ірина", "Mixed.Case@School.Example", ct);
        await host.AddTeacherAsync(course, byEmail, ct);
        await host.AddTeacherAsync(course, nothing, ct);
        await host.AddTeacherAsync(course, mixed, ct);

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("zoe@school.example", RowText(page, byEmail), StringComparison.Ordinal);
        Assert.Contains(host.Text(CKeys.TeacherNoName, "uk"), RowText(page, nothing), StringComparison.Ordinal);
        Assert.Contains("Mixed.Case@School.Example", RowText(page, mixed), StringComparison.Ordinal);
    }

    /// <summary>AC-004, AC-016: each row links to the course list of that teacher, and that list shows the counted courses.</summary>
    [Fact]
    public async Task EachRow_LinksToTheCourseListOfThatTeacher_AndTheListShowsTheCountedCourses()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);
        var archived = await client.GetAsync(Data.Url(status: "archived"), ct);

        Assert.Contains(Data.CoursesLink(world.Shevchenko), Tl.RowHrefs(page.Body, world.Shevchenko));
        var courses = await client.GetAsync(Data.CoursesLink(world.Shevchenko), ct);
        Assert.Equal(HttpStatusCode.OK, courses.Status);
        Assert.Equal([world.Physics, world.Chemistry], Cl.RowIds(courses.Body));
        Assert.Contains(Data.CoursesLink(world.Koval, "archived"), Tl.RowHrefs(archived.Body, world.Koval));
        var archivedCourses = await client.GetAsync(Data.CoursesLink(world.Koval, "archived"), ct);
        Assert.Equal(HttpStatusCode.OK, archivedCourses.Status);
        Assert.Equal([world.History], Cl.RowIds(archivedCourses.Body));
    }

    /// <summary>AC-005, AC-015: 120 teachers — 50 per page in name order, the total, page links keeping the status, a page past the end shows the last.</summary>
    [Fact]
    public async Task Paging_Shows50PerPage_SortedByName_AndKeepsTheStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var ids = await host.SeedNumberedTeachersAsync(120, ct);

        var first = await client.GetAsync(Data.Path, ct);
        var second = await client.GetAsync(Data.Url(page: "2"), ct);
        var third = await client.GetAsync(Data.Url(page: "3"), ct);
        var past = await client.GetAsync(Data.Url(page: "9"), ct);
        var allSecond = await client.GetAsync(Data.Url("all", "2"), ct);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(ids.Take(50), Tl.RowIds(first.Body));
        Assert.Contains("120", Html.ElementText(first.Body, Data.Markup.TotalId) ?? string.Empty, StringComparison.Ordinal);
        var links = Cl.HrefsInside(first.Body, Data.Markup.PagingId, "nav");
        Assert.Contains(Data.Url(page: "2"), links);
        Assert.Contains(Data.Url(page: "3"), links);
        Assert.Equal(ids.Skip(50).Take(50), Tl.RowIds(second.Body));
        Assert.Equal(ids.Skip(100), Tl.RowIds(third.Body));
        Assert.Equal(HttpStatusCode.OK, past.Status);
        Assert.Equal(ids.Skip(100), Tl.RowIds(past.Body));
        var allLinks = Cl.HrefsInside(allSecond.Body, Data.Markup.PagingId, "nav");
        Assert.Contains(Data.Url("all"), allLinks);
        Assert.Contains(Data.Url("all", "3"), allLinks);
    }

    /// <summary>AC-005: one page of teachers has no paging links.</summary>
    [Fact]
    public async Task OnePage_HasNoPagingLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(2, Tl.RowIds(page.Body).Count);
        Assert.Contains("2", Html.ElementText(page.Body, Data.Markup.TotalId) ?? string.Empty, StringComparison.Ordinal);
        Assert.Empty(Cl.HrefsInside(page.Body, Data.Markup.PagingId, "nav"));
    }

    /// <summary>
    /// AC-006, AC-013, S-05, S-06: the switcher returns to the canonical URL of the validated values; the page's URLs carry
    /// status and page only, the course-list links status and teacherId only; no name or email is in any URL; unknown
    /// parameters are not echoed.
    /// </summary>
    [Fact]
    public async Task TheAddress_CarriesStatusAndPageOnly_AndTheSwitcherReturnsToTheValidatedUrl()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(
            Data.Path + "?status=ALL&page=1&teacherId=" + world.Koval + "&q=zz-echo-q&evil=zz-echo-zz", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Data.Url("all"), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.DoesNotContain("zz-echo-q", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("zz-echo-zz", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", page.Body, StringComparison.Ordinal);
        var hrefs = Cl.Hrefs(page.Body);
        AssertQueryNames(hrefs.Where(h => h.StartsWith(Data.Path, StringComparison.Ordinal)), [Data.Parameters.Status, Data.Parameters.Page]);
        AssertQueryNames(
            hrefs.Where(h => h.StartsWith(CourseListTestData.Path, StringComparison.Ordinal)),
            [CourseListTestData.Parameters.Status, CourseListTestData.Parameters.TeacherId]);
        foreach (var href in hrefs)
        {
            var decoded = Uri.UnescapeDataString(href);
            Assert.DoesNotContain("@", decoded, StringComparison.Ordinal);
            Assert.DoesNotContain("Шевченко", decoded, StringComparison.Ordinal);
            Assert.DoesNotContain("Українка", decoded, StringComparison.Ordinal);
            Assert.DoesNotContain("Коваль", decoded, StringComparison.Ordinal);
            Assert.DoesNotContain("google", decoded, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertQueryNames(IEnumerable<string> hrefs, string[] allowed)
    {
        foreach (var href in hrefs)
        {
            var query = href.Contains('?', StringComparison.Ordinal) ? href[(href.IndexOf('?', StringComparison.Ordinal) + 1)..] : string.Empty;
            var names = query.Length == 0 ? [] : query.Split('&').Select(p => p.Split('=')[0]).ToArray();
            Assert.All(names, n => Assert.Contains(n, allowed));
        }
    }

    /// <summary>AC-008: with nobody to list the form stays and the empty message is shown, with no table.</summary>
    [Fact]
    public async Task NobodyListed_ShowsTheMessage_WithoutATable()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(Tl.HasGetForm(page.Body));
        Assert.Equal(host.Text(Keys.Empty, "uk"), Html.ElementText(page.Body, Data.Markup.EmptyId));
        Assert.Empty(Tl.RowIds(page.Body));
        Assert.DoesNotContain("<table", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Cl.HrefsInside(page.Body, Data.Markup.PagingId, "nav"));
    }

    /// <summary>AC-008: only an archived course with a teacher — the default ACTIVE lists nobody and shows the same message.</summary>
    [Fact]
    public async Task OnlyArchivedCourses_ShowTheMessage_UnderTheDefaultStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await host.InsertCourseAsync(1, "Архівний курс", ct, CourseTestData.States.Archived);
        await host.AddTeacherAsync(course, await host.InsertPersonAsync(1, "Коваль", "Олена", null, ct), ct);

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(host.Text(Keys.Empty, "uk"), Html.ElementText(page.Body, Data.Markup.EmptyId));
        Assert.Empty(Tl.RowIds(page.Body));
        Assert.DoesNotContain("<table", page.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-007: "Teachers" is the second link of the Google Workspace section, after the courses and before the journal, for both roles and in read-only mode.</summary>
    [Theory]
    [InlineData(Actor.Admin, ReadOnlyModeHost.Cause.NotReadOnly)]
    [InlineData(Actor.Dean, ReadOnlyModeHost.Cause.NotReadOnly)]
    [InlineData(Actor.Dean, ReadOnlyModeHost.Cause.Suspended)]
    public async Task Teachers_IsTheSecondLinkOfTheWorkspaceSection(Actor actor, ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct, cause);
        await using var _host = host;

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, landing.Status);
        var links = Cl.HrefsInside(landing.Body, Data.Markup.WorkspaceSectionId, "nav").ToList();
        Assert.Equal(CourseListTestData.Path, links.ElementAtOrDefault(0));
        Assert.Equal(Data.Path, links.ElementAtOrDefault(1));
        Assert.Contains(CourseListTestData.JournalPath, links);
        Assert.True(links.IndexOf(CourseListTestData.JournalPath) > 1, "The journal link is not after the teachers link.");
        Assert.Contains(host.Text(Keys.NavigationEntry, "uk"), Mc.VisibleText(landing.Body), StringComparison.Ordinal);
    }

    /// <summary>AC-013: each malformed parameter gives 400, this page with its message, no list, no echo.</summary>
    [Theory]
    [InlineData("status", CourseListTestData.Keys.ValidationStatusMalformed)]
    [InlineData("page", CourseListTestData.Keys.ValidationPageMalformed)]
    public async Task AMalformedParameter_Is400_WithItsMessage_AndNoEcho(string parameter, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartSeededAsync(ct);
        await using var _host = host;
        var url = parameter == "status" ? Data.Url(status: Secret) : Data.Url(page: Secret);

        var page = await client.GetAsync(url, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal([host.Text(key, "uk")], Cl.ErrorMessages(page.Body));
        Assert.True(Tl.HasGetForm(page.Body), "A refusal must render this page, not the host's error page.");
        Assert.Empty(Tl.RowIds(page.Body));
        Assert.DoesNotContain(Secret, page.Body, StringComparison.Ordinal);
        Assert.Equal("active", Cl.SelectedValue(page.Body, Data.Parameters.Status));
    }

    /// <summary>AC-013: every failing parameter gets its message, status first.</summary>
    [Fact]
    public async Task BothMalformed_ShowBothMessages_InOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Url("foo", "0"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal(
            [
                host.Text(CKeys.ValidationStatusMalformed, "uk"), host.Text(CKeys.ValidationPageMalformed, "uk"),
            ],
            Cl.ErrorMessages(page.Body));
    }

    /// <summary>AC-013, S-06: after a refusal the switcher's return path and the form drop the rejected value and keep the valid one.</summary>
    [Fact]
    public async Task AfterARefusal_TheRejectedValueIsDropped_FromTheSwitcherAndTheForm()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartSeededAsync(ct);
        await using var _host = host;

        var badPage = await client.GetAsync(Data.Url("archived", Secret), ct);
        var badStatus = await client.GetAsync(Data.Url(Secret, "2"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, badPage.Status);
        Assert.Equal(Data.Url("archived"), UiLanguageTestData.RenderedReturnPath(badPage.Body));
        Assert.Equal("archived", Cl.SelectedValue(badPage.Body, Data.Parameters.Status));
        Assert.DoesNotContain(Secret, badPage.Body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.Status);
        Assert.Equal(Data.Path, UiLanguageTestData.RenderedReturnPath(badStatus.Body));
    }

    /// <summary>S-09: a teacher's name and email are rendered HTML-encoded, never as markup.</summary>
    [Fact]
    public async Task NamesAndEmails_AreHtmlEncoded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await host.InsertCourseAsync(1, "Курс", ct);
        await host.AddTeacherAsync(
            course, await host.InsertPersonAsync(1, "<script>zz</script>", "Ім'я", "<b>zz</b>@school.example", ct), ct);

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain("<script>zz</script>", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>zz</b>", page.Body, StringComparison.Ordinal);
        Assert.Contains("<script>zz</script>", page.Text, StringComparison.Ordinal);
    }
}
