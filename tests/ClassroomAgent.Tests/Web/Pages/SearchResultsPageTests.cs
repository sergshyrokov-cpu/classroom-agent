using System.Net;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using S = ClassroomAgent.Tests.TestInfrastructure.SearchTestData;
using Sh = ClassroomAgent.Tests.TestInfrastructure.SearchHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-071 AC-002 … AC-006, AC-008 over HTTP on real PostgreSQL: the results page's groups and order, the rows and their links,
/// the student's courses with the BR-068 mark, the limits and the link to the whole list, the messages, the encoding of the text,
/// the warning, and what must not be found (spec FR-002 … FR-009; openapi <c>x-markup-results-page</c>, <c>x-links</c>).
/// </summary>
public sealed class SearchResultsPageTests(PostgreSqlFixture database)
{
    private async Task<(InstallationTestHost Host, FormClient Client)> StartAsync(CancellationToken ct, Actor actor = Actor.Dean) =>
        await JournalHostExtensions.StartAsync(database, actor, ct);

    /// <summary>AC-002: a text matching all four shows them in the fixed order, each with its heading, in both languages.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task AllFourGroups_AppearInTheFixedOrder_WithTheirHeadings(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);
        if (language == "en")
        {
            await client.ChooseLanguageAsync(S.Url(Ls.Needle), UiLanguageTestData.English, ct);
        }

        var page = await client.GetAsync(S.Url(Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal([S.Students, S.Teachers, S.Courses, S.MeetCodes], S.GroupsInOrder(page.Body));
        foreach (var (group, key) in new[]
                 {
                     (S.Students, S.Keys.GroupStudents), (S.Teachers, S.Keys.GroupTeachers), (S.Courses, S.Keys.GroupCourses),
                     (S.MeetCodes, S.Keys.GroupMeetCodes),
                 })
        {
            Assert.Contains("<h2", S.RequireGroup(page.Body, group), StringComparison.Ordinal);
            Assert.StartsWith(host.Text(key, language), S.TextOf(S.RequireGroup(page.Body, group)), StringComparison.Ordinal);
        }

        Assert.Contains(host.Text(S.Keys.Title, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-002: a group with no match is hidden, the rest keep their order.</summary>
    [Theory]
    [InlineData("zz.student", new[] { S.Students })]
    [InlineData("zz.teacher", new[] { S.Teachers })]
    [InlineData("курс", new[] { S.Courses })]
    [InlineData("-aaaa", new[] { S.MeetCodes })]
    public async Task AGroupWithNoMatch_IsHidden(string text, string[] groups)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Url(text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(groups, S.GroupsInOrder(page.Body));
        Assert.Null(S.ParagraphText(page.Body, S.Reminder));
    }

    /// <summary>AC-002: a person in both roles is in both groups; the students' row lists only the courses they study.</summary>
    [Fact]
    public async Task APersonInBothRoles_IsInBothGroups_AndTheStudentsRowOmitsTheCourseTheyTeach()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        var teaches = await host.InsertCourseAsync(1, "Викладає", ct);
        var studies = await host.InsertCourseAsync(2, "Навчається", ct);
        var person = await host.InsertPersonAsync(1, "Двоєдина", "Анна", CourseTestData.Email("both.roles"), ct);
        await host.AddTeacherAsync(teaches, person, ct);
        await host.AddStudentAsync(studies, person, ct);

        var page = await client.GetAsync(S.Url("двоєдина"), ct);

        var students = S.RequireGroup(page.Body, S.Students);
        Assert.Contains("Двоєдина Анна", S.TextOf(students), StringComparison.Ordinal);
        Assert.Contains("Двоєдина Анна", S.TextOf(S.RequireGroup(page.Body, S.Teachers)), StringComparison.Ordinal);
        Assert.Contains(S.CardHref(studies), S.Hrefs(students));
        Assert.DoesNotContain(S.CardHref(teaches), S.Hrefs(students));
    }

    /// <summary>
    /// AC-003, FR-003, BR-068: both courses with links to their cards; only the left one carries the mark, in the journal's words
    /// and with the Kyiv date (22:30 UTC is the next day); the group's text has no other mark. Both languages.
    /// </summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task AStudentOnOneRoster_WhoLeftAnother_ShowsBothCourses_AndOnlyTheLeftOneIsMarked(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        var onRoster = await host.InsertCourseAsync(1, "Алгебра", ct);
        var left = await host.InsertCourseAsync(2, "Фізика", ct, section: "10-А");
        var person = await host.InsertPersonAsync(1, "Мельник", "Петро", CourseTestData.Email("p.melnyk"), ct);
        var lastSeen = new DateTimeOffset(2026, 9, 10, 22, 30, 0, TimeSpan.Zero);
        await host.AddStudentAsync(onRoster, person, ct);
        await CourseRows.InsertMembershipAsync(host, left, person, ct, firstSeenAt: lastSeen.AddDays(-9), lastSeenAt: lastSeen, onRoster: false);
        if (language == "en")
        {
            await client.ChooseLanguageAsync(S.Url("мельник"), UiLanguageTestData.English, ct);
        }

        var page = await client.GetAsync(S.Url("мельник"), ct);

        var texts = DataMarksTexts.Of(language);
        var students = S.RequireGroup(page.Body, S.Students);
        Assert.Contains("Мельник Петро", S.TextOf(students), StringComparison.Ordinal);
        Assert.Contains(CourseTestData.Email("p.melnyk"), S.TextOf(students), StringComparison.Ordinal);
        var algebra = S.TextFromLink(students, S.CardHref(onRoster));
        var physics = S.TextFromLink(students, S.CardHref(left));
        Assert.Contains("Алгебра", algebra, StringComparison.Ordinal);
        Assert.False(S.HasAnyLeftMark(algebra, texts), algebra);
        Assert.Contains("Фізика — 10-А", physics, StringComparison.Ordinal);
        Assert.Contains(texts.With(texts.LeftOn, lastSeen), physics, StringComparison.Ordinal);
    }

    /// <summary>AC-003, I-5, US-060 OD-003 a: off the roster with equal sightings shows the course with the mark and no date.</summary>
    [Fact]
    public async Task AMembershipWithEqualSightings_CarriesTheMarkWithNoDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        var course = await host.InsertCourseAsync(1, "Лише робота", ct);
        var person = await host.InsertPersonAsync(1, "Названий", "Олег", null, ct);
        await CourseRows.InsertMembershipAsync(host, course, person, ct, firstSeenAt: Sh.At, lastSeenAt: Sh.At, onRoster: false);

        var page = await client.GetAsync(S.Url("названий"), ct);

        var texts = DataMarksTexts.Of("uk");
        var row = S.TextFromLink(S.RequireGroup(page.Body, S.Students), S.CardHref(course));
        Assert.True(DataMarksHostExtensions.HasUndatedLeft(row, texts), row);
    }

    /// <summary>FR-003: back on the roster, no mark.</summary>
    [Fact]
    public async Task BackOnTheRoster_ThereIsNoMark()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        var course = await host.InsertCourseAsync(1, "Повернувся", ct);
        var person = await host.InsertPersonAsync(1, "Мельник", "Петро", null, ct);
        await CourseRows.InsertMembershipAsync(host, course, person, ct, firstSeenAt: Sh.At, lastSeenAt: Sh.At.AddDays(3), onRoster: false);
        await host.ExecuteAsync("UPDATE course_membership SET on_roster = true WHERE participant_id = @p", ct, ("p", person));

        var page = await client.GetAsync(S.Url("мельник"), ct);

        var row = S.TextFromLink(S.RequireGroup(page.Body, S.Students), S.CardHref(course));
        Assert.False(S.HasAnyLeftMark(row, DataMarksTexts.Of("uk")), row);
    }

    /// <summary>D-8: a person with no stored name shows the address once.</summary>
    [Fact]
    public async Task AnEmailThatIsTheLabel_IsNotRepeated()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        var course = await host.InsertCourseAsync(1, "Курс без назви людини", ct);
        var email = CourseTestData.Email("email.only.person");
        var person = await host.InsertPersonAsync(1, null, null, email, ct);
        await host.AddStudentAsync(course, person, ct);

        var page = await client.GetAsync(S.Url("email.only.person"), ct);

        var text = S.TextOf(S.RequireGroup(page.Body, S.Students));
        Assert.Equal(text.IndexOf(email, StringComparison.Ordinal), text.LastIndexOf(email, StringComparison.Ordinal));
        Assert.Contains(email, text, StringComparison.Ordinal);
    }

    /// <summary>FR-004: the teacher's row: name, email, a link to their courses at status "all" with no search text.</summary>
    [Fact]
    public async Task ATeacherRow_LinksToTheirCoursesAtStatusAll_WithNoText()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        var world = await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Url("zz.teacher"), ct);

        var group = S.RequireGroup(page.Body, S.Teachers);
        Assert.Contains(Sh.TeacherSurname + " Олена", S.TextOf(group), StringComparison.Ordinal);
        Assert.Contains(CourseTestData.Email("zz.teacher"), S.TextOf(group), StringComparison.Ordinal);
        var link = "/workspace/courses?status=all&teacherId=" + world.TeacherId;
        Assert.Contains(link, S.Hrefs(group));
        Assert.Contains(host.Text(S.Keys.TeacherCourses, "uk"), S.TextFromLink(group, link), StringComparison.Ordinal);
    }

    /// <summary>I-6, I-7: an archived course's teacher is found; a teacher who left every roster is not.</summary>
    [Fact]
    public async Task TheTeachersGroup_IsTheListAtStatusAll()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        var archived = await host.InsertCourseAsync(1, "Старий", ct, CourseTestData.States.Archived);
        var current = await host.InsertPersonAsync(1, "Романенко", "Марія", null, ct);
        var former = await host.InsertPersonAsync(2, "Романюк", "Оксана", null, ct);
        await host.AddTeacherAsync(archived, current, ct);
        await host.AddTeacherAsync(archived, former, ct, onRoster: false);

        var page = await client.GetAsync(S.Url("роман"), ct);

        var text = S.TextOf(S.RequireGroup(page.Body, S.Teachers));
        Assert.Contains("Романенко", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Романюк", text, StringComparison.Ordinal);
    }

    /// <summary>FR-005, I-6: a course of any status: name, section, status in the list's words, a link to the card; both languages.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task ACourseRow_ShowsSectionAndStatus_AndLinksToTheCard(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        var course = await host.InsertCourseAsync(1, "Архівний предмет", ct, CourseTestData.States.Archived, section: "11-Б");
        if (language == "en")
        {
            await client.ChooseLanguageAsync(S.Url("архівний"), UiLanguageTestData.English, ct);
        }

        var page = await client.GetAsync(S.Url("архівний"), ct);

        var group = S.RequireGroup(page.Body, S.Courses);
        Assert.Contains(S.CardHref(course), S.Hrefs(group));
        var text = S.TextOf(group);
        Assert.Contains("Архівний предмет", text, StringComparison.Ordinal);
        Assert.Contains("11-Б", text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Courses.Status.Archived", language), text, StringComparison.Ordinal);
    }

    /// <summary>FR-006, I-9: a Meet row: the code as a link to its list with this search, and the list's name; on every list.</summary>
    [Fact]
    public async Task AMeetRow_LinksToItsListWithTheSearch_AndNamesTheList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        await host.SeedSampleCodesAsync(1, 1, 1, ct);

        var page = await client.GetAsync(S.Url("zzm"), ct);

        var group = S.RequireGroup(page.Body, S.MeetCodes);
        foreach (var (code, list) in new[]
                 {
                     ("zzm-0001-aaa", Mc.Lists.Unassigned), ("zzm-0002-aaa", Mc.Lists.Linked), ("zzm-0003-aaa", Mc.Lists.NotACourse),
                 })
        {
            var row = S.TextFromLink(group, Ls.MeetUrl(list, "zzm"));
            Assert.Contains(code, S.TextOf(group), StringComparison.Ordinal);
            Assert.Contains(host.Text(Ls.Keys.MeetListName(list), "uk"), row, StringComparison.Ordinal);
        }
    }

    /// <summary>FR-006, section 7: no link holds a row's code, name or email in a query string; only ids or the user's own text.</summary>
    [Fact]
    public async Task NoLink_PutsARowsCodeNameOrEmail_IntoAQueryString()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Url(Ls.Needle), ct);

        var main = page.Body[page.Body.IndexOf("<main", StringComparison.Ordinal)..];
        var hrefs = S.Hrefs(main);
        Assert.NotEmpty(hrefs);
        foreach (var value in new[] { Sh.Code, Sh.StudentSurname, Sh.TeacherSurname, "zz.student", "zz.teacher" })
        {
            Assert.DoesNotContain(hrefs, href => Uri.UnescapeDataString(href).Contains(value, StringComparison.Ordinal));
        }
    }

    public static TheoryData<string> SampledGroups => new(S.Students, S.Teachers, S.Courses);

    private static async Task SeedSamplesAsync(InstallationTestHost host, string group, int count, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        switch (group)
        {
            case S.Students:
                await host.SeedNumberedStudentsAsync(count, ct);
                break;
            case S.Teachers:
                await host.SeedSampleTeachersAsync(count, ct);
                break;
            default:
                await host.SeedSampleCoursesAsync(count, ct);
                break;
        }
    }

    /// <summary>AC-004: with 21 matches, 20 rows alphabetically and the count line; the 21st is not shown.</summary>
    [Theory]
    [MemberData(nameof(SampledGroups))]
    public async Task With21Matches_TwentyShowAlphabetically_WithTheCountLine(string group)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await SeedSamplesAsync(host, group, 21, ct);

        var page = await client.GetAsync(S.Url("зразок"), ct);

        var markup = S.RequireGroup(page.Body, group);
        var text = S.TextOf(markup);
        var positions = Enumerable.Range(1, 20).Select(i => text.IndexOf($"Зразок{i:D2}", StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
        Assert.DoesNotContain("Зразок21", text, StringComparison.Ordinal);
        Assert.Equal(
            host.Text(S.Keys.LimitShown, "uk").Replace("{0}", "20").Replace("{1}", "21"),
            S.LimitLine(markup));
    }

    /// <summary>AC-004: with exactly 20 there is neither line nor link.</summary>
    [Theory]
    [MemberData(nameof(SampledGroups))]
    public async Task WithExactly20_ThereIsNeitherLineNorLink(string group)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await SeedSamplesAsync(host, group, 20, ct);

        var page = await client.GetAsync(S.Url("зразок"), ct);

        var markup = S.RequireGroup(page.Body, group);
        Assert.Contains("Зразок20", S.TextOf(markup), StringComparison.Ordinal);
        Assert.Null(S.LimitLine(markup));
        Assert.Null(S.WholeListHref(markup));
    }

    /// <summary>AC-004: students have no link to a whole list, as no such list exists.</summary>
    [Fact]
    public async Task TheStudentsGroup_HasNoWholeListLink()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await SeedSamplesAsync(host, S.Students, 21, ct);

        var page = await client.GetAsync(S.Url("зразок"), ct);

        Assert.Null(S.WholeListHref(S.RequireGroup(page.Body, S.Students)));
    }

    /// <summary>AC-004, x-links: the teachers' link opens the list at status "all" with this search and the same total.</summary>
    [Fact]
    public async Task TheTeachersLink_OpensTheWholeList_WithTheSameTotal()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await SeedSamplesAsync(host, S.Teachers, 21, ct);

        var page = await client.GetAsync(S.Url("зразок"), ct);

        var markup = S.RequireGroup(page.Body, S.Teachers);
        var href = S.WholeListHref(markup);
        Assert.Equal(Ls.TeachersUrl("all", "зразок"), href);
        Assert.Contains(host.Text(S.Keys.WholeList, "uk"), S.TextOf(markup), StringComparison.Ordinal);
        var list = await client.GetAsync(href!, ct);
        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Equal("зразок", Ls.FieldValue(list.Body, Ls.TeacherField));
        Assert.Equal(host.Text(Ls.Keys.TeachersTotal, "uk").Replace("{0}", "21"), Html.ElementText(list.Body, TeacherListTestData.Markup.TotalId));
    }

    /// <summary>AC-004, x-links: the courses' link opens the list at status "all" with this search and the same total.</summary>
    [Fact]
    public async Task TheCoursesLink_OpensTheWholeList_WithTheSameTotal()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await SeedSamplesAsync(host, S.Courses, 21, ct);

        var page = await client.GetAsync(S.Url("зразок"), ct);

        var href = S.WholeListHref(S.RequireGroup(page.Body, S.Courses));
        Assert.Equal(CourseListUrl.Build(CourseStatusFilter.All, null, "зразок", 1), href);
        var list = await client.GetAsync(href!, ct);
        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Equal("зразок", Ls.FieldValue(list.Body, Ls.CourseField));
        Assert.Equal(
            host.Text(CourseListTestData.Keys.PagingTotal, "uk").Replace("{0}", "21"),
            Html.ElementText(list.Body, CourseListTestData.Markup.TotalId));
    }

    /// <summary>AC-004, FR-007: for Meet codes N is the sum of the three counters of the list the link opens.</summary>
    [Fact]
    public async Task TheMeetLink_OpensTheFirstListWithAMatch_AndTheTotalIsTheSumOfTheCounters()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        await host.SeedSampleCodesAsync(9, 8, 5, ct);

        var page = await client.GetAsync(S.Url("zzm"), ct);

        var markup = S.RequireGroup(page.Body, S.MeetCodes);
        Assert.Equal(host.Text(S.Keys.LimitShown, "uk").Replace("{0}", "20").Replace("{1}", "22"), S.LimitLine(markup));
        var href = S.WholeListHref(markup);
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, "zzm"), href);
        var list = await client.GetAsync(href!, ct);
        Assert.Equal(HttpStatusCode.OK, list.Status);
        var nav = Ls.ListsNavText(list.Body);
        Assert.Contains(Ls.Counter(host, Mc.Lists.Unassigned, 9), nav, StringComparison.Ordinal);
        Assert.Contains(Ls.Counter(host, Mc.Lists.Linked, 8), nav, StringComparison.Ordinal);
        Assert.Contains(Ls.Counter(host, Mc.Lists.NotACourse, 5), nav, StringComparison.Ordinal);
    }

    /// <summary>AC-004, FR-007: Meet codes at exactly 20, on one list or summed over the three: neither line nor link.</summary>
    [Theory]
    [InlineData(20, 0, 0)]
    [InlineData(9, 8, 3)]
    public async Task WithExactly20MeetCodes_ThereIsNeitherLineNorLink(int unassigned, int linked, int marked)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        await host.SeedSampleCodesAsync(unassigned, linked, marked, ct);

        var page = await client.GetAsync(S.Url("zzm"), ct);

        var markup = S.RequireGroup(page.Body, S.MeetCodes);
        Assert.Contains("zzm-0020-aaa", S.TextOf(markup), StringComparison.Ordinal);
        Assert.Null(S.LimitLine(markup));
        Assert.Null(S.WholeListHref(markup));
    }

    /// <summary>I-11: with no match on the first list the link goes to the next list that has one, so it never lands on an empty list.</summary>
    [Fact]
    public async Task TheMeetLink_SkipsAListWithNoMatch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        await host.SeedSampleCodesAsync(0, 21, 1, ct);

        var page = await client.GetAsync(S.Url("zzm"), ct);

        Assert.Equal(Ls.MeetUrl(Mc.Lists.Linked, "zzm"), S.WholeListHref(S.RequireGroup(page.Body, S.MeetCodes)));
    }

    public static TheoryData<string?> TooShortTexts => new(null, "", "   ", "я", " я ");

    /// <summary>AC-005, FR-008: no text or one character: the message, 200, no group, no reminder.</summary>
    [Theory]
    [MemberData(nameof(TooShortTexts))]
    public async Task NoTextOrOneCharacter_ShowsTheMessage_AndNoGroup(string? text)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Url(text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(host.Text(S.Keys.TooShort, "uk"), S.ParagraphText(page.Body, S.Message));
        Assert.Empty(S.GroupsInOrder(page.Body));
        Assert.Null(S.ParagraphText(page.Body, S.Reminder));
    }

    /// <summary>VR-003: a 1-character text stays in the field and in the return path.</summary>
    [Fact]
    public async Task OneCharacter_StaysInTheField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(S.Url("я"), ct);

        Assert.Equal("я", S.HeaderValue(page.Body));
        Assert.Equal(S.Url("я"), UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>VR-003: two one-letter words are searched.</summary>
    [Fact]
    public async Task TwoOneLetterWords_AreSearched()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        var course = await host.InsertCourseAsync(1, "Курс", ct);
        var person = await host.InsertPersonAsync(1, "Яр", "Юрій", null, ct);
        await host.AddStudentAsync(course, person, ct);

        var page = await client.GetAsync(S.Url("я ю"), ct);

        Assert.Null(S.ParagraphText(page.Body, S.Message));
        Assert.Contains("Яр Юрій", S.TextOf(S.RequireGroup(page.Body, S.Students)), StringComparison.Ordinal);
    }

    /// <summary>AC-005, VR-002: a refused text: 400, the existing message, no group, nothing echoed in the field, a link or the return path.</summary>
    [Theory]
    [MemberData(nameof(Ls.RefusedTexts), MemberType = typeof(Ls))]
    public async Task ARefusedText_Answers400_WithTheMessage_AndIsNotEchoed(string fragment, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Path + "?" + fragment, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal(host.Text(key, "uk"), S.ParagraphText(page.Body, S.Message));
        Assert.Empty(S.GroupsInOrder(page.Body));
        Assert.Null(S.HeaderValue(page.Body));
        Assert.Equal(S.Path, UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.DoesNotContain(Ls.Needle, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-005: nothing found repeats the text, encoded, then the v108 reminder; no group. Both languages.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task NothingFound_RepeatsTheText_AndShowsTheReminder(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);
        const string Text = "a&b=<c>\"d'";
        if (language == "en")
        {
            await client.ChooseLanguageAsync(S.Url(Text), UiLanguageTestData.English, ct);
        }

        var page = await client.GetAsync(S.Url(Text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Empty(S.GroupsInOrder(page.Body));
        Assert.Equal(host.Text(S.Keys.NothingFound, language).Replace("{0}", Text), S.ParagraphText(page.Body, S.NothingFound));
        Assert.Equal(host.Text(S.Keys.Reminder, language), S.ParagraphText(page.Body, S.Reminder));
        Assert.DoesNotContain("<c>", page.Body, StringComparison.Ordinal);
    }

    /// <summary>FR-001, section 7: the text stays in the field and the switcher's return path, encoded; the raw fallback is not reached.</summary>
    [Fact]
    public async Task TheText_IsEncoded_InTheFieldAndTheReturnPath()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        const string Text = "a&b=<c>\"d'";

        var page = await client.GetAsync(S.Url(Text) + "&zzother=zzmarker", ct);

        Assert.Equal(Text, S.HeaderValue(page.Body));
        Assert.Equal(S.Url(Text), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.DoesNotContain("zzmarker", page.Body, StringComparison.Ordinal);
    }

    /// <summary>VR-004: an unknown parameter is not read and not echoed, on any state of the page.</summary>
    [Theory]
    [InlineData("?zzother=zzmarker")]
    [InlineData("?q=я&zzother=zzmarker")]
    [InlineData("?q=" + Ls.Needle + "&zzother=zzmarker")]
    public async Task AnUnknownParameter_IsNeverEchoed(string query)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(S.Path + query, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain("zzmarker", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-001, spec FR-002: switching language from the results page returns to the same search.</summary>
    [Fact]
    public async Task SwitchingLanguage_ReturnsToTheSameSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var choice = await client.ChooseLanguageAsync(S.Url(Ls.Needle), UiLanguageTestData.English, ct);

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal(S.Url(Ls.Needle), choice.LocationPath);
        var page = await client.GetAsync(choice.LocationPath!, ct);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Equal(Ls.Needle, S.HeaderValue(page.Body));
    }

    public static TheoryData<string, HttpStatusCode> WarningStates => new()
    {
        { S.Url(Ls.Needle), HttpStatusCode.OK },
        { S.Url("нічого такого немає"), HttpStatusCode.OK },
        { S.Url("я"), HttpStatusCode.OK },
        { S.Path, HttpStatusCode.OK },
        { S.Path + "?q=" + Ls.Needle + "%07", HttpStatusCode.BadRequest },
    };

    /// <summary>AC-008, FR-009, I-12: the one-line warning in small text, in every state of the page.</summary>
    [Theory]
    [MemberData(nameof(WarningStates))]
    public async Task TheWarning_ShowsInEveryState_InSmallText(string url, HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(url, ct);

        Assert.Equal(status, page.Status);
        Assert.Equal(host.Text(S.Keys.Warning, "uk"), S.ParagraphText(page.Body, S.Warning));
        var tag = Ls.TagOf(page.Body, S.Warning);
        Assert.Contains("small", Ls.AttributeOf(tag, "class") ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>AC-008: in English too.</summary>
    [Fact]
    public async Task TheWarning_IsInTheUsersLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await client.ChooseLanguageAsync(S.Path, UiLanguageTestData.English, ct);

        var page = await client.GetAsync(S.Path, ct);

        Assert.Equal(host.Text(S.Keys.Warning, "en"), S.ParagraphText(page.Body, S.Warning));
    }

    /// <summary>FR-002, I-3: the results page has no page-search hint.</summary>
    [Fact]
    public async Task TheResultsPage_HasNoHint()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(S.Url(Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.False(PageSearchHintTestData.HasAnyHint(page.Body));
    }

    /// <summary>AC-006, over HTTP: after a real erasure neither the person nor their name shows; a meeting with such an organizer neither.</summary>
    [Fact]
    public async Task AnErasedPerson_AndAMeetingWithThemAsOrganizer_AreNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        var course = await host.InsertCourseAsync(1, "Курс", ct);
        var email = CourseTestData.Email("erased.person");
        var person = await host.InsertPersonAsync(1, "Стертий", "Анна", email, ct);
        await host.AddStudentAsync(course, person, ct);
        await host.SeedMeetingAsync("ers-unas-001", email, Sh.At, ct);
        var before = await client.GetAsync(S.Url("erased.person"), ct);
        Assert.Equal([S.Students, S.MeetCodes], S.GroupsInOrder(before.Body));

        var admin = await host.InsertAdminAsync(ct);
        await host.EraseAsync(admin, person, ct);

        foreach (var text in new[] { "erased.person", "стертий" })
        {
            var page = await client.GetAsync(S.Url(text), ct);
            Assert.Empty(S.GroupsInOrder(page.Body));
            Assert.NotNull(S.ParagraphText(page.Body, S.NothingFound));
        }
    }

    /// <summary>Spec section 9: the reads of a request are real ones: a group's matches come from every field at once, end to end.</summary>
    [Fact]
    public async Task OneText_FindsPeopleCoursesAndCodes_ThroughTheSameRules()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct, Actor.Admin);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Url(Ls.Needle.ToUpperInvariant() + "  "), ct);

        Assert.Equal([S.Students, S.Teachers, S.Courses, S.MeetCodes], S.GroupsInOrder(page.Body));
        Assert.Equal(Ls.Needle.ToUpperInvariant(), S.HeaderValue(page.Body));
        Assert.Contains(Sh.Code, S.TextOf(S.RequireGroup(page.Body, S.MeetCodes)), StringComparison.Ordinal);
    }
}
