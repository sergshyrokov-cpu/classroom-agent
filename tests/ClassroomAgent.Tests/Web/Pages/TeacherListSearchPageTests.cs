using System.Net;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Data = ClassroomAgent.Tests.TestInfrastructure.TeacherListTestData;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Tl = ClassroomAgent.Tests.TestInfrastructure.TeacherListHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-070 over HTTP, the teacher list: the field, the whole list searched with the status filter, the total and paging
/// counting the matches, <c>q</c> carried by the paging links and the return path but not by a row's course link (I-12), the
/// no-match message, and the refusals of VR-001 and VR-002 (AC-003, AC-006, AC-007, AC-010; openapi <c>getTeacherList</c>).
/// </summary>
public sealed class TeacherListSearchPageTests(PostgreSqlFixture database)
{
    /// <summary>Sorted after every "Вчитель NNN", so with no search it is on the last page.</summary>
    private const string LastSurname = "Яременко";

    private const string LastGivenName = "Оксана";

    private static readonly string LastEmail = CourseTestData.Email("zz.yaremenko");

    private async Task<(InstallationTestHost Host, FormClient Client, long Last)> StartWithManyTeachersAsync(CancellationToken ct, int count = 55)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await host.SeedNumberedTeachersAsync(count, ct);
        var course = await host.InsertCourseAsync(900, "Останній курс", ct);
        var last = await host.InsertPersonAsync(900, LastSurname, LastGivenName, LastEmail, ct);
        await host.AddTeacherAsync(course, last, ct);
        return (host, client, last);
    }

    /// <summary>AC-003, FR-003: a teacher on a later page is found by surname, given name or email; the total counts the matches only.</summary>
    [Theory]
    [InlineData("яременко")]
    [InlineData("оксана")]
    [InlineData("zz.yaremenko")]
    public async Task ATeacherOnALaterPage_IsFound_AndTheTotalCountsTheMatchesOnly(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, last) = await StartWithManyTeachersAsync(ct);
        await using var _host = host;
        var firstPage = await client.GetAsync(Data.Path, ct);
        Assert.DoesNotContain(last, Tl.RowIds(firstPage.Body));

        var page = await client.GetAsync(Ls.TeachersUrl(q: text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal([last], Tl.RowIds(page.Body));
        Assert.Equal(host.Text(Ls.Keys.TeachersTotal, "uk").Replace("{0}", "1"), Html.ElementText(page.Body, Data.Markup.TotalId));
        Assert.Null(Ls.ElementOf(page.Body, Data.Markup.PagingId));
    }

    /// <summary>AC-003, FR-003: the status filter applies with the search, by AND.</summary>
    [Fact]
    public async Task TheStatusFilter_AppliesWithTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedTeacherListAsync(ct);

        Assert.Empty(Tl.RowIds((await client.GetAsync(Ls.TeachersUrl(q: "коваль"), ct)).Body));
        Assert.Equal([world.Koval], Tl.RowIds((await client.GetAsync(Ls.TeachersUrl("archived", "коваль"), ct)).Body));
        Assert.Equal([world.Koval], Tl.RowIds((await client.GetAsync(Ls.TeachersUrl("all", "коваль"), ct)).Body));
        Assert.Equal([world.Shevchenko], Tl.RowIds((await client.GetAsync(Ls.TeachersUrl(q: "шевченко тарас"), ct)).Body));
    }

    /// <summary>AC-003, FR-003, spec I-1: a student and a former teacher are still not listed, whatever the search.</summary>
    [Fact]
    public async Task WhoIsListed_DoesNotChange_ForASearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedTeacherListAsync(ct);
        var control = await client.GetAsync(Ls.TeachersUrl("all", "шевченко"), ct);
        Assert.Equal([world.Shevchenko], Tl.RowIds(control.Body));

        Assert.Empty(Tl.RowIds((await client.GetAsync(Ls.TeachersUrl("all", "мельник"), ct)).Body));
        Assert.Empty(Tl.RowIds((await client.GetAsync(Ls.TeachersUrl("all", "франко"), ct)).Body));
    }

    /// <summary>AC-006, FR-003: paging over the matches; every paging link carries the search, in the order status, q, page.</summary>
    [Fact]
    public async Task PagingLinks_CarryTheSearch_InTheStatedOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartWithManyTeachersAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Ls.TeachersUrl("all", "вчитель", 2), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(5, Tl.RowIds(page.Body).Count);
        Assert.Equal(host.Text(Ls.Keys.TeachersTotal, "uk").Replace("{0}", "55"), Html.ElementText(page.Body, Data.Markup.TotalId));
        var hrefs = Cl.HrefsInside(page.Body, Data.Markup.PagingId, "nav");
        Assert.NotEmpty(hrefs);
        Assert.All(hrefs, href => Assert.Equal(Ls.TeachersUrl("all", "вчитель"), href.Replace("&page=2", string.Empty).Replace("&page=1", string.Empty)));
        Assert.Equal(Ls.TeachersUrl("all", "вчитель", 2), UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>AC-006, FR-003: a page past the last shows the last, under a search too.</summary>
    [Fact]
    public async Task APagePastTheLast_ShowsTheLast_UnderASearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, last) = await StartWithManyTeachersAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Ls.TeachersUrl(q: "яременко", page: 9), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal([last], Tl.RowIds(page.Body));
        Assert.Equal(Ls.TeachersUrl(q: "яременко"), UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>AC-006, spec section 7: the text is encoded in the address and in the markup, and comes back as it was.</summary>
    [Fact]
    public async Task TheText_IsEncoded_InTheAddressAndInTheField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);
        const string Text = "a&b=<c>\"d'";

        var page = await client.GetAsync(Ls.TeachersUrl(q: Text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Text, Ls.FieldValue(page.Body, Ls.TeacherField));
        Assert.Equal(Ls.TeachersUrl(q: Text), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.DoesNotContain("<c>", page.Body, StringComparison.Ordinal);
    }

    /// <summary>I-12: a row's link to the course list does not carry the search.</summary>
    [Fact]
    public async Task ARowsCourseLink_DoesNotCarryTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedTeacherListAsync(ct);

        var page = await client.GetAsync(Ls.TeachersUrl(q: "шевченко"), ct);

        Assert.Equal([world.Shevchenko], Tl.RowIds(page.Body));
        Assert.Equal([Data.CoursesLink(world.Shevchenko)], Tl.RowHrefs(page.Body, world.Shevchenko));
    }

    /// <summary>AC-010, FR-003, FR-007: the field: label from the translation, type, length limit, no remembering, the validated value.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheField_HasItsLabelAndAttributes_AndShowsTheValidatedText(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);
        if (language == UiLanguageTestData.English)
        {
            await client.ChooseLanguageAsync(Data.Path, UiLanguageTestData.English, ct);
        }

        var page = await client.GetAsync(Ls.TeachersUrl(q: "  шевченко  "), ct);

        var tag = Ls.TagOf(page.Body, Ls.TeacherField);
        Assert.NotNull(tag);
        Assert.Equal("search", Ls.AttributeOf(tag, "type"));
        Assert.Equal("q", Ls.AttributeOf(tag, "name"));
        Assert.Equal("100", Ls.AttributeOf(tag, "maxlength"));
        Assert.Equal("off", Ls.AttributeOf(tag, "autocomplete"));
        Assert.Equal("шевченко", Ls.AttributeOf(tag, "value"));
        Assert.Equal(host.Text(Ls.Keys.TeachersSearch, language), Ls.LabelOf(page.Body, Ls.TeacherField));
        Assert.Contains(tag, Ls.ElementOf(page.Body, Data.Markup.FormId), StringComparison.Ordinal);
    }

    /// <summary>VR-001: white space only is no search; the text is trimmed and exactly 100 characters is allowed.</summary>
    [Fact]
    public async Task WhiteSpaceIsNoSearch_AndAHundredCharactersAreAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);

        var blank = await client.GetAsync(Data.Path + "?q=%20%20", ct);
        Assert.Equal(HttpStatusCode.OK, blank.Status);
        Assert.Equal(2, Tl.RowIds(blank.Body).Count);
        Assert.Equal(Data.Path, UiLanguageTestData.RenderedReturnPath(blank.Body));

        var longest = new string('я', 100);
        var allowed = await client.GetAsync(Ls.TeachersUrl(q: longest), ct);
        Assert.Equal(HttpStatusCode.OK, allowed.Status);
        Assert.Equal(longest, Ls.FieldValue(allowed.Body, Ls.TeacherField));
        Assert.Equal(host.Text(Ls.Keys.TeachersNoMatch, "uk"), Html.ElementText(allowed.Body, Data.Markup.EmptyId));
    }

    /// <summary>AC-003, FR-006: no match under a search shows its own message; with no search the existing one stays.</summary>
    [Fact]
    public async Task EmptyMessages_DifferBetweenASearchAndNone()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.WaitForPurgeRunsAsync(1, ct);

        var none = await client.GetAsync(Data.Path, ct);
        Assert.Equal(host.Text(Ls.Keys.TeachersEmpty, "uk"), Html.ElementText(none.Body, Data.Markup.EmptyId));

        var searched = await client.GetAsync(Ls.TeachersUrl(q: "нікого"), ct);
        Assert.Equal(HttpStatusCode.OK, searched.Status);
        Assert.Equal(host.Text(Ls.Keys.TeachersNoMatch, "uk"), Html.ElementText(searched.Body, Data.Markup.EmptyId));
        Assert.Equal("нікого", Ls.FieldValue(searched.Body, Ls.TeacherField));
    }

    /// <summary>
    /// AC-007, VR-001, VR-002: a refused text answers 400 with the page and the message, the field empty, no list, a return path
    /// without it and the text nowhere in the page; another valid parameter keeps its value.
    /// </summary>
    [Theory]
    [MemberData(nameof(Ls.RefusedTexts), MemberType = typeof(Ls))]
    public async Task ARefusedText_Answers400_WithTheMessage_AndIsNotEchoed(string fragment, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);

        var page = await client.GetAsync(Data.Path + "?status=all&" + fragment, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(host.Text(key, "uk"), Cl.ErrorMessages(page.Body));
        Assert.Empty(Tl.RowIds(page.Body));
        Assert.Null(Ls.ElementOf(page.Body, Data.Markup.EmptyId));
        Assert.NotNull(Ls.TagOf(page.Body, Ls.TeacherField));
        Assert.Null(Ls.FieldValue(page.Body, Ls.TeacherField));
        Assert.Equal(Data.Url("all"), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.DoesNotContain(Ls.Needle, page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-007, FR-003, D-3: a malformed status and a refused text both report; nothing is read for the list.</summary>
    [Fact]
    public async Task EveryFailingParameter_IsReported()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);

        var page = await client.GetAsync(Data.Path + "?status=nonsense&q=" + Ls.Needle + "%07", ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        var messages = Cl.ErrorMessages(page.Body);
        Assert.Contains(host.Text(Ls.Keys.SearchControl, "uk"), messages);
        Assert.Equal(2, messages.Count);
    }

    /// <summary>
    /// AC-007: the rejected text reaches no read at all, and the same page with an accepted text passes it to both reads
    /// (the control: the test would otherwise pass for a source that is never told about any search).
    /// </summary>
    [Theory]
    [MemberData(nameof(Ls.RefusedTexts), MemberType = typeof(Ls))]
    public async Task ARefusedText_ReachesNoRead_AndAnAcceptedTextReachesThem(string fragment, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        var log = new ListSearchReadLog();
        var (host, client) = await JournalHostExtensions.StartAsync(
            database,
            Actor.Dean,
            ct,
            beforeStart: h => h.ConfigureServices = services => services.AddScoped<ITeacherListSource>(
                provider => new RecordingTeacherListSource(new TeacherListSource(provider.GetRequiredService<ClassroomAgentDbContext>()), log)));
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);
        Assert.NotNull(key);

        var refused = await client.GetAsync(Data.Path + "?" + fragment, ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Empty(log.Reads);

        await client.GetAsync(Ls.TeachersUrl(q: "шевченко"), ct);
        Assert.Equal(
            [(nameof(ITeacherListSource.CountTeachersAsync), "шевченко"), (nameof(ITeacherListSource.GetTeacherPageAsync), "шевченко")],
            log.Reads);
    }

    /// <summary>AC-006: the language switch returns to the same validated search.</summary>
    [Fact]
    public async Task SwitchingLanguage_ReturnsToTheSameSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartWithManyTeachersAsync(ct);
        await using var _host = host;
        var url = Ls.TeachersUrl("all", "вчитель", 2);

        var first = await client.GetAsync(url, ct);
        var returnPath = UiLanguageTestData.RenderedReturnPath(first.Body);
        Assert.Equal(url, returnPath);
        var switched = await client.ChooseLanguageAsync(url, UiLanguageTestData.English, ct, returnPath: returnPath);

        Assert.Equal(url, switched.LocationPath);
        var after = await client.GetAsync(switched.LocationPath!, ct);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(after.Body));
        Assert.Equal("вчитель", Ls.FieldValue(after.Body, Ls.TeacherField));
        Assert.Equal(5, Tl.RowIds(after.Body).Count);
    }
}
