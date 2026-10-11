using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Data = ClassroomAgent.Tests.TestInfrastructure.CourseListTestData;
using Keys = ClassroomAgent.Tests.TestInfrastructure.CourseListTestData.Keys;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-020 over HTTP: the row, the default and chosen status, the teacher choice and filter, the name rule, search, the
/// filters together, paging, the links into the journal and the Meet reports, the address and the language switcher,
/// the empty states, the home page entry, the 400 and 404 pages, HTML encoding and no Google call (AC-001 … AC-013,
/// AC-016, AC-017, AC-018; api-design §2 … §5). Rows are seeded by SQL; the markup hooks are
/// <see cref="CourseListTestData.Markup"/>.
/// </summary>
public sealed class CourseListPageTests(PostgreSqlFixture database)
{
    private const string Secret = "zz-secret-zz";

    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk");

    private async Task<(InstallationTestHost Host, FormClient Client, Cl.SeededWorld World)> StartSeededAsync(
        CancellationToken ct, Actor actor = Actor.Admin)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        var world = await host.SeedCourseListAsync(ct);
        return (host, client, world);
    }

    private static string RowText(PageResponse page, long courseId)
    {
        var ids = Cl.RowIds(page.Body);
        var index = ids.ToList().IndexOf(courseId);
        Assert.True(index >= 0, $"Course {courseId} is not listed.");
        return Cl.RowTexts(page.Body)[index];
    }

    /// <summary>AC-001, AC-018, FR-003: name and section, status, current teachers, current students, Kyiv update time.</summary>
    [Fact]
    public async Task TheRow_ShowsEveryColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        NoStore.Assert(page, "the course list");
        Assert.True(Cl.HasGetForm(page.Body), "The filter form is missing or does not submit by GET to the list.");
        var row = RowText(page, world.Physics);
        Assert.Contains(Cl.PhysicsName, row, StringComparison.Ordinal);
        Assert.Contains(Cl.PhysicsSection, row, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.StatusActive, "uk"), row, StringComparison.Ordinal);
        Assert.Contains(Cl.UkrainkaLabel, row, StringComparison.Ordinal);
        Assert.Contains(Cl.ShevchenkoLabel, row, StringComparison.Ordinal);
        Assert.True(
            row.IndexOf(Cl.UkrainkaLabel, StringComparison.Ordinal) < row.IndexOf(Cl.ShevchenkoLabel, StringComparison.Ordinal),
            "Teachers are not in alphabetical order.");
        Assert.DoesNotContain(Cl.FrankoLabel, row, StringComparison.Ordinal);
        Assert.Contains(" 3 ", " " + row + " ", StringComparison.Ordinal);
        Assert.Contains(new DateTime(2026, 4, 1).ToString(Uk.DateTimeFormat.ShortDatePattern, Uk), row, StringComparison.Ordinal);
        Assert.Contains("01:30", row, StringComparison.Ordinal);
        Assert.DoesNotContain("22:30", row, StringComparison.Ordinal);
        Assert.DoesNotContain(Cl.UkrainkaEmail, row, StringComparison.Ordinal);
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>AC-002, FR-004: with no status only ACTIVE courses are listed, by name, and ACTIVE is the selected status.</summary>
    [Fact]
    public async Task ByDefault_OnlyActiveCoursesAreListed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal([world.Physics, world.Chemistry], Cl.RowIds(page.Body));
        Assert.Equal("active", Cl.SelectedValue(page.Body, Data.Parameters.Status));
        Assert.DoesNotContain(Cl.HistoryName, Mc.VisibleText(page.Body), StringComparison.Ordinal);
    }

    /// <summary>AC-003, FR-004: a chosen state lists that state only; "all" lists every course in name order.</summary>
    [Fact]
    public async Task AChosenStatus_OrAll_ListsThoseCourses()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var archived = await client.GetAsync(Data.Url(status: "archived"), ct);
        var provisioned = await client.GetAsync(Data.Url(status: "PROVISIONED"), ct);
        var all = await client.GetAsync(Data.Url(status: "all"), ct);

        Assert.Equal([world.History], Cl.RowIds(archived.Body));
        Assert.Equal("archived", Cl.SelectedValue(archived.Body, Data.Parameters.Status));
        Assert.Equal([world.Algebra], Cl.RowIds(provisioned.Body));
        Assert.Equal([world.Algebra, world.History, world.Physics, world.Chemistry], Cl.RowIds(all.Body));
        Assert.Equal("all", Cl.SelectedValue(all.Body, Data.Parameters.Status));
    }

    /// <summary>
    /// AC-004, FR-005: the choice is "all teachers" then the current teachers of any course by label — the owner and the
    /// co-teacher included, the former teacher not; choosing either teacher of Фізика lists it.
    /// </summary>
    [Fact]
    public async Task TheTeacherChoice_AndFilter_UseTheCurrentTeachers()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);
        var byOwner = await client.GetAsync(Data.Url(world.Shevchenko), ct);
        var byCoTeacher = await client.GetAsync(Data.Url(world.Ukrainka), ct);
        var ownerAll = await client.GetAsync(Data.Url(status: "all", teacherId: Data.Id(world.Shevchenko)), ct);

        var options = Cl.TeacherOptions(page.Body);
        Assert.Equal(
            [
                (string.Empty, host.Text(Keys.FormAllTeachers, "uk")),
                (Data.Id(world.Koval), Cl.KovalLabel),
                (Data.Id(world.Ukrainka), Cl.UkrainkaLabel),
                (Data.Id(world.Shevchenko), Cl.ShevchenkoLabel),
            ],
            options);
        Assert.Equal([world.Physics], Cl.RowIds(byOwner.Body));
        Assert.Equal(Data.Id(world.Shevchenko), Cl.SelectedValue(byOwner.Body, Data.Parameters.TeacherId));
        Assert.Equal([world.Physics], Cl.RowIds(byCoTeacher.Body));
        Assert.Equal([world.History, world.Physics], Cl.RowIds(ownerAll.Body));
    }

    /// <summary>AC-004, VR-002, I-9: a former teacher's id is not in the choice — the page answers 404, listing nothing.</summary>
    [Fact]
    public async Task AFormerTeachersId_Is404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Url(world.Franko), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Equal([host.Text(Keys.ValidationTeacherUnknown, "uk")], Cl.ErrorMessages(page.Body));
        Assert.Empty(Cl.RowIds(page.Body));
        Assert.True(Cl.HasGetForm(page.Body));
        Assert.True(Cl.SelectedValue(page.Body, Data.Parameters.TeacherId) is null or "");
    }

    /// <summary>AC-005, FR-007, I-2: surname first from the profile (FullName is not used); the full email when there is no name.</summary>
    [Fact]
    public async Task ATeacher_IsShownByProfileName_OrByEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;
        var emailOnly = await host.InsertPersonAsync(60, null, null, CourseTestData.Email("no.name"), ct);
        await host.AddTeacherAsync(world.Chemistry, emailOnly, ct);

        var page = await client.GetAsync(Data.Path, ct);

        var text = Mc.VisibleText(page.Body);
        Assert.DoesNotContain("Тарас Шевченко", text, StringComparison.Ordinal);
        Assert.Contains(CourseTestData.Email("no.name"), RowText(page, world.Chemistry), StringComparison.Ordinal);
        Assert.Contains(
            (Data.Id(emailOnly), CourseTestData.Email("no.name")),
            Cl.TeacherOptions(page.Body));
    }

    /// <summary>I-3: two teachers with the same name are told apart in the choice by their emails.</summary>
    [Fact]
    public async Task SameNamedTeachers_AreToldApartByEmail_InTheChoice()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;
        var twin = await host.InsertPersonAsync(61, "Коваль", "Олена", CourseTestData.Email("o.koval.twin"), ct);
        await host.AddTeacherAsync(world.Physics, twin, ct);

        var page = await client.GetAsync(Data.Path, ct);

        var labels = Cl.TeacherOptions(page.Body).ToDictionary(o => o.Value, o => o.Label);
        Assert.Contains(Cl.KovalEmail, labels[Data.Id(world.Koval)], StringComparison.Ordinal);
        Assert.Contains(CourseTestData.Email("o.koval.twin"), labels[Data.Id(twin)], StringComparison.Ordinal);
        Assert.DoesNotContain("@", labels[Data.Id(world.Shevchenko)], StringComparison.Ordinal);
    }

    /// <summary>AC-006, FR-006: a fragment of the name or the section, any letter case; the text stays in the field.</summary>
    [Fact]
    public async Task TheSearch_FindsTheNameOrTheSection_InAnyCase()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var byName = await client.GetAsync(Data.Url(status: "all", q: "ІСТ"), ct);
        var bySection = await client.GetAsync(Data.Url(q: "9-б"), ct);

        Assert.Equal([world.History], Cl.RowIds(byName.Body));
        Assert.Equal("ІСТ", Html.InputValue(byName.Body, Data.Parameters.Q));
        Assert.Equal([world.Physics], Cl.RowIds(bySection.Body));
    }

    /// <summary>AC-007: status, teacher and search together list only the courses meeting all three.</summary>
    [Fact]
    public async Task TheFilters_Combine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Url("all", Data.Id(world.Shevchenko), "фіз"), ct);
        var none = await client.GetAsync(Data.Url("archived", Data.Id(world.Shevchenko), "фіз"), ct);

        Assert.Equal([world.Physics], Cl.RowIds(page.Body));
        Assert.Empty(Cl.RowIds(none.Body));
        Assert.Contains(host.Text(Keys.EmptyNoMatch, "uk"), Mc.VisibleText(none.Body), StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-008, AC-020, FR-008, FR-009: 120 matching courses — 50 per page in name order, the total, page links keeping the
    /// filters, page 3 with the last 20, and a page past the end showing the last page.
    /// </summary>
    [Fact]
    public async Task Paging_Shows50PerPage_AndKeepsTheFilters()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var ids = new List<long>();
        for (var i = 120; i >= 1; i--)
        {
            ids.Add(await host.InsertCourseAsync(i, $"Курс {i:D3}", ct));
        }

        ids.Reverse();

        var first = await client.GetAsync(Data.Url(status: "all", q: "Курс"), ct);
        var third = await client.GetAsync(Data.Url(status: "all", q: "Курс", page: "3"), ct);
        var past = await client.GetAsync(Data.Url(status: "all", q: "Курс", page: "9"), ct);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(ids.Take(50), Cl.RowIds(first.Body));
        Assert.Contains("120", Html.ElementText(first.Body, Data.Markup.TotalId) ?? string.Empty, StringComparison.Ordinal);
        var links = Cl.HrefsInside(first.Body, Data.Markup.PagingId, "nav");
        Assert.Contains(Data.Url(status: "all", q: "Курс", page: "2"), links);
        Assert.Contains(Data.Url(status: "all", q: "Курс", page: "3"), links);
        Assert.Equal(ids.Skip(100), Cl.RowIds(third.Body));
        Assert.Contains(Data.Url(status: "all", q: "Курс", page: "2"), Cl.HrefsInside(third.Body, Data.Markup.PagingId, "nav"));
        Assert.Contains(Data.Url(status: "all", q: "Курс"), Cl.HrefsInside(third.Body, Data.Markup.PagingId, "nav"));
        Assert.Equal(HttpStatusCode.OK, past.Status);
        Assert.Equal(ids.Skip(100), Cl.RowIds(past.Body));
    }

    /// <summary>FR-008: one page of courses has no paging links.</summary>
    [Fact]
    public async Task OnePage_HasNoPagingLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal([world.Physics, world.Chemistry], Cl.RowIds(page.Body));
        Assert.Contains("2", Html.ElementText(page.Body, Data.Markup.TotalId) ?? string.Empty, StringComparison.Ordinal);
        Assert.Empty(Cl.HrefsInside(page.Body, Data.Markup.PagingId, "nav"));
    }

    /// <summary>AC-009, FR-010, OD-008: each row links to the journal and the Meet reports with the course; no Classroom link.</summary>
    [Fact]
    public async Task EachRow_LinksToTheJournalAndTheMeetReports_AndNotToClassroom()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;
        await host.ExecuteAsync(
            "UPDATE course SET alternate_link = 'https://classroom.google.com/c/zz-alternate-link' WHERE id = @id",
            ct,
            ("id", world.Physics));

        var page = await client.GetAsync(Data.Path, ct);

        var hrefs = Cl.Hrefs(page.Body);
        foreach (var course in new[] { world.Physics, world.Chemistry })
        {
            Assert.Contains(Data.JournalLink(course), hrefs);
            Assert.Contains(Data.MeetReportsLink(course), hrefs);
        }

        Assert.DoesNotContain("zz-alternate-link", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(hrefs, h => h.Contains("classroom.google.com", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(host.Text(Keys.LinkJournal, "uk"), Mc.VisibleText(page.Body), StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.LinkMeetReports, "uk"), Mc.VisibleText(page.Body), StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-010, FR-009, S-05, S-06: the switcher returns to the canonical URL of the validated values; every course-list URL
    /// carries the four parameters only, the teacher as an id; no name or email is in any URL; unknown parameters are not
    /// echoed.
    /// </summary>
    [Fact]
    public async Task TheAddress_CarriesTheFiltersOnly_AndTheSwitcherReturnsToTheValidatedUrl()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;
        var teacher = Data.Id(world.Shevchenko);

        var page = await client.GetAsync(
            Data.Url("ALL", teacher, "  Іст  ") + "&evil=zz-echo-zz", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Data.Url("all", teacher, "Іст"), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.DoesNotContain("zz-echo-zz", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", page.Body, StringComparison.Ordinal);
        var allowed = new[] { Data.Parameters.Status, Data.Parameters.TeacherId, Data.Parameters.Q, Data.Parameters.Page };
        foreach (var href in Cl.Hrefs(page.Body).Where(h => h.StartsWith(Data.Path, StringComparison.Ordinal)))
        {
            var query = href.Contains('?', StringComparison.Ordinal) ? href[(href.IndexOf('?', StringComparison.Ordinal) + 1)..] : string.Empty;
            var names = query.Length == 0 ? [] : query.Split('&').Select(p => p.Split('=')[0]).ToArray();
            Assert.All(names, n => Assert.Contains(n, allowed));
        }

        foreach (var href in Cl.Hrefs(page.Body))
        {
            var decoded = Uri.UnescapeDataString(href);
            Assert.DoesNotContain("@", decoded, StringComparison.Ordinal);
            Assert.DoesNotContain("Шевченко", decoded, StringComparison.Ordinal);
            Assert.DoesNotContain("Українка", decoded, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-010, FR-009, FR-012: after a refusal the switcher's return path drops the rejected value and the page.</summary>
    [Fact]
    public async Task AfterARefusal_TheSwitcherDropsTheRejectedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Url(status: Secret, q: "фіз", page: "2"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal(Data.Url(q: "фіз"), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.DoesNotContain(Secret, page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-011, FR-011: with no course synchronized the form stays and its own message is shown, with no table.</summary>
    [Fact]
    public async Task NoCourseSynchronized_ShowsItsOwnMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(Cl.HasGetForm(page.Body));
        Assert.Equal(host.Text(Keys.EmptyNoCoursesStored, "uk"), Html.ElementText(page.Body, Data.Markup.EmptyId));
        Assert.Empty(Cl.RowIds(page.Body));
        Assert.DoesNotContain("<table", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal([(string.Empty, host.Text(Keys.FormAllTeachers, "uk"))], Cl.TeacherOptions(page.Body));
    }

    /// <summary>AC-011, FR-011: courses stored but none matching — the default ACTIVE over archived courses — shows the other message.</summary>
    [Fact]
    public async Task NothingMatching_ShowsTheOtherMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.InsertCourseAsync(1, "Архівний курс", ct, CourseTestData.States.Archived);

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.NotEqual(host.Text(Keys.EmptyNoMatch, "uk"), host.Text(Keys.EmptyNoCoursesStored, "uk"));
        Assert.Equal(host.Text(Keys.EmptyNoMatch, "uk"), Html.ElementText(page.Body, Data.Markup.EmptyId));
        Assert.DoesNotContain("<table", page.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-012, FR-013, I-1: "Courses" is the first link of the Google Workspace section, before the journal, for both roles and in read-only mode.</summary>
    [Theory]
    [InlineData(Actor.Admin, ReadOnlyModeHost.Cause.NotReadOnly)]
    [InlineData(Actor.Dean, ReadOnlyModeHost.Cause.NotReadOnly)]
    [InlineData(Actor.Dean, ReadOnlyModeHost.Cause.Suspended)]
    public async Task Courses_IsTheFirstLinkOfTheWorkspaceSection(Actor actor, ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct, cause);
        await using var _host = host;

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, landing.Status);
        var links = Cl.HrefsInside(landing.Body, "workspace-section", "nav");
        Assert.Equal(Data.Path, links.FirstOrDefault());
        Assert.Contains(Data.JournalPath, links);
        Assert.Contains(host.Text(Keys.NavigationEntry, "uk"), Mc.VisibleText(landing.Body), StringComparison.Ordinal);
    }

    /// <summary>AC-013, FR-012, VR-001 … VR-004: each malformed parameter gives 400, this page with its message, no list, no echo.</summary>
    [Theory]
    [InlineData("status", Keys.ValidationStatusMalformed)]
    [InlineData("teacherId", Keys.ValidationTeacherMalformed)]
    [InlineData("q", Keys.ValidationSearchTooLong)]
    [InlineData("page", Keys.ValidationPageMalformed)]
    public async Task AMalformedParameter_Is400_WithItsMessage_AndNoEcho(string parameter, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartSeededAsync(ct);
        await using var _host = host;
        var url = parameter switch
        {
            "status" => Data.Url(status: Secret),
            "teacherId" => Data.Url(teacherId: Secret),
            "q" => Data.Url(q: Secret + new string('я', 100)),
            _ => Data.Url(page: Secret),
        };

        var page = await client.GetAsync(url, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal([host.Text(key, "uk")], Cl.ErrorMessages(page.Body));
        Assert.True(Cl.HasGetForm(page.Body), "A refusal must render this page, not the host's error page.");
        Assert.Empty(Cl.RowIds(page.Body));
        Assert.DoesNotContain(Secret, page.Body, StringComparison.Ordinal);
        Assert.Equal("active", Cl.SelectedValue(page.Body, Data.Parameters.Status));
        Assert.True(Html.InputValue(page.Body, Data.Parameters.Q) is null or "");
    }

    /// <summary>AC-013, VR-003: a repeated search text and a control character have their own messages.</summary>
    [Fact]
    public async Task ARepeatedOrControlCharacterSearch_HasItsOwnMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartSeededAsync(ct);
        await using var _host = host;

        var repeated = await client.GetAsync(Data.Path + "?q=a&q=b", ct);
        var control = await client.GetAsync(Data.Url(q: "фіз\u0007ика"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, repeated.Status);
        Assert.Equal([host.Text(Keys.ValidationSearchRepeated, "uk")], Cl.ErrorMessages(repeated.Body));
        Assert.Equal(HttpStatusCode.BadRequest, control.Status);
        Assert.Equal([host.Text(Keys.ValidationSearchControlCharacter, "uk")], Cl.ErrorMessages(control.Body));
    }

    /// <summary>FR-012: every failing parameter gets its message, in parameter order.</summary>
    [Fact]
    public async Task SeveralMalformedParameters_ShowEveryMessage_InOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Url("foo", "x", null, "0"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal(
            [
                host.Text(Keys.ValidationStatusMalformed, "uk"), host.Text(Keys.ValidationTeacherMalformed, "uk"),
                host.Text(Keys.ValidationPageMalformed, "uk"),
            ],
            Cl.ErrorMessages(page.Body));
    }

    /// <summary>AC-013, VR-002: a well-formed id of nobody is 404 with its message and the teacher back at "all teachers".</summary>
    [Fact]
    public async Task AnUnknownTeacherId_Is404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Url(teacherId: "987654321"), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Equal([host.Text(Keys.ValidationTeacherUnknown, "uk")], Cl.ErrorMessages(page.Body));
        Assert.True(Cl.HasGetForm(page.Body));
        Assert.Empty(Cl.RowIds(page.Body));
        Assert.DoesNotContain("987654321", page.Body, StringComparison.Ordinal);
    }

    /// <summary>VR-005, S-09: a course name, section and teacher name are rendered HTML-encoded, never as markup.</summary>
    [Fact]
    public async Task GoogleData_IsHtmlEncoded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await host.InsertCourseAsync(1, "<b>zz-markup</b>", ct, section: "<i>zz-section</i>");
        await host.AddTeacherAsync(course, await host.InsertPersonAsync(1, "<script>zz</script>", "Ім'я", null, ct), ct);

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain("<b>zz-markup</b>", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("<i>zz-section</i>", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>zz</script>", page.Body, StringComparison.Ordinal);
        Assert.Contains("<b>zz-markup</b>", page.Text, StringComparison.Ordinal);
    }
}
