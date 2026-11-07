using System.Net;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-070 over HTTP, the Meet codes page: the search form, the counters and rows under a search on all three lists, the
/// Organizers column of OD-001 a, what must not be found, <c>q</c> in every link and the return path, the page clamp of I-6,
/// and the refusals of VR-003 and VR-004 (AC-004, AC-005, AC-006, AC-007, AC-010; openapi <c>getMeetCodes</c>).
/// </summary>
public sealed class MeetCodesSearchPageTests(PostgreSqlFixture database)
{
    private const string UnassignedCode = "abc-defg-hij";

    private const string LinkedCode = "lnk-mmmm-nnn";

    private const string MarkedCode = "mrk-pppp-qqq";

    private static readonly DateTimeOffset At = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly string Shared = Mc.Email("shared.organizer");

    private sealed record World(long Algebra, long Maker);

    /// <summary>One code in each list, all organized by the same address.</summary>
    private static async Task<World> SeedAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        var algebra = await host.SeedRosterCourseAsync("Алгебра", [Mc.Email("algebra.teacher")], [], ct, section: "10-А");
        await host.InsertAppUserAsync(ct, email: Mc.Email("code.maker"));
        var maker = await host.AccountIdAsync(Mc.Email("code.maker"), ct);
        await host.SeedMeetingAsync(UnassignedCode, Shared, At, ct);
        await host.SeedMeetingAsync(LinkedCode, Shared, At, ct);
        await host.SeedPersonLinkAsync(LinkedCode, algebra, maker, At, ct);
        await host.SeedMeetingAsync(MarkedCode, Shared, At, ct);
        await host.SeedMarkAsync(MarkedCode, maker, At, ct);
        return new World(algebra, maker);
    }

    /// <summary><paramref name="count"/> unassigned codes "pag-NNN-xxx" of one organizer.</summary>
    private static async Task SeedManyUnassignedAsync(InstallationTestHost host, int count, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        for (var i = 1; i <= count; i++)
        {
            await host.SeedMeetingAsync($"pag-{i:D3}-xxx", Shared, At.AddMinutes(i), ct);
        }
    }

    /// <summary>AC-004, FR-004: on every list the counters show each list's matches and the table only the shown list's.</summary>
    [Theory]
    [InlineData(Mc.Lists.Unassigned, UnassignedCode)]
    [InlineData(Mc.Lists.Linked, LinkedCode)]
    [InlineData(Mc.Lists.NotACourse, MarkedCode)]
    public async Task UnderASearch_EveryListCounts_AndShowsItsOwnMatchesOnly(string list, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);
        await host.SeedMeetingAsync("oth-unas-001", Mc.Email("other.person"), At, ct);

        var page = await client.GetAsync(Ls.MeetUrl(list, "shared.organizer"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal([expected], Ls.ListedCodes(page.Body));
        var nav = Ls.ListsNavText(page.Body);
        Assert.Contains(Ls.Counter(host, Mc.Lists.Unassigned, 1), nav, StringComparison.Ordinal);
        Assert.Contains(Ls.Counter(host, Mc.Lists.Linked, 1), nav, StringComparison.Ordinal);
        Assert.Contains(Ls.Counter(host, Mc.Lists.NotACourse, 1), nav, StringComparison.Ordinal);
    }

    /// <summary>AC-004: the counters follow the search, and with no search they are as today.</summary>
    [Fact]
    public async Task TheCounters_FollowTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);
        await host.SeedMeetingAsync("oth-unas-001", Mc.Email("other.person"), At, ct);

        var none = await client.GetAsync(Mc.Path, ct);
        Assert.Contains(Ls.Counter(host, Mc.Lists.Unassigned, 2), Ls.ListsNavText(none.Body), StringComparison.Ordinal);

        var searched = Ls.ListsNavText((await client.GetAsync(Ls.MeetUrl(q: "алгебр"), ct)).Body);
        Assert.Contains(Ls.Counter(host, Mc.Lists.Unassigned, 0), searched, StringComparison.Ordinal);
        Assert.Contains(Ls.Counter(host, Mc.Lists.Linked, 1), searched, StringComparison.Ordinal);
        Assert.Contains(Ls.Counter(host, Mc.Lists.NotACourse, 0), searched, StringComparison.Ordinal);
    }

    /// <summary>AC-001, AC-004: by a code without its hyphens, by linked course name and section, by organizer.</summary>
    [Theory]
    [InlineData("abcdefg", Mc.Lists.Unassigned, UnassignedCode)]
    [InlineData("алгебр", Mc.Lists.Linked, LinkedCode)]
    [InlineData("10-а", Mc.Lists.Linked, LinkedCode)]
    [InlineData("алгебра shared.organizer", Mc.Lists.Linked, LinkedCode)]
    public async Task ACode_IsFound_ByEveryFieldOfTheFeature(string text, string list, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);

        // A second row on the Unassigned and Linked lists that no text here matches, so the search must have been applied.
        var other = Mc.Email("other.person");
        var biology = await host.SeedRosterCourseAsync("Біологія", [Mc.Email("biology.teacher")], [], ct, section: "11");
        await host.SeedMeetingAsync("oth-unas-001", other, At, ct);
        await host.SeedMeetingAsync("oth-link-002", other, At, ct);
        await host.SeedPersonLinkAsync("oth-link-002", biology, world.Maker, At, ct);

        var page = await client.GetAsync(Ls.MeetUrl(list, text), ct);

        Assert.Equal([expected], Ls.ListedCodes(page.Body));
    }

    /// <summary>AC-005, FR-004: an erased organizer is not found on any list and the row shows the survivor only.</summary>
    [Theory]
    [InlineData(Mc.Lists.Unassigned, UnassignedCode)]
    [InlineData(Mc.Lists.Linked, LinkedCode)]
    [InlineData(Mc.Lists.NotACourse, MarkedCode)]
    public async Task AnErasedOrganizer_IsNotFound_AndNotShown(string list, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);
        var erased = Mc.Email("erased.person");
        await host.SeedMeetingAsync(code, erased, At.AddDays(1), ct);

        // Control: before the erasure this address finds the code and shows in its row.
        var before = await client.GetAsync(Ls.MeetUrl(list, "erased.person"), ct);
        Assert.Equal([code], Ls.ListedCodes(before.Body));
        Assert.Contains(erased, Ls.CellsOfCode(before.Body, code)[0], StringComparison.Ordinal);

        await host.ExecuteAsync("UPDATE meet_session SET organizer_email = NULL WHERE organizer_email = @e", ct, ("e", erased));

        var after = await client.GetAsync(Ls.MeetUrl(list, "erased.person"), ct);
        Assert.Empty(Ls.ListedCodes(after.Body));
        var row = await client.GetAsync(Ls.MeetUrl(list, code), ct);
        Assert.Equal(Shared, Ls.CellsOfCode(row.Body, code)[0]);
        Assert.DoesNotContain(erased, row.Body, StringComparison.Ordinal);
    }

    /// <summary>FR-004, OD-001 a: the two tables get the Organizers column right after the code, with or without a search.</summary>
    [Theory]
    [InlineData(Mc.Lists.Linked, LinkedCode, null)]
    [InlineData(Mc.Lists.Linked, LinkedCode, "shared.organizer")]
    [InlineData(Mc.Lists.NotACourse, MarkedCode, null)]
    [InlineData(Mc.Lists.NotACourse, MarkedCode, "shared.organizer")]
    public async Task LinkedAndNotACourse_ShowTheOrganizers_RightAfterTheCode(string list, string code, string? text)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);
        var second = Mc.Email("a.first");
        await host.SeedMeetingAsync(code, second, At.AddDays(1), ct);

        var page = await client.GetAsync(Ls.MeetUrl(list, text), ct);

        var headers = Ls.TableHeaders(page.Body);
        Assert.Equal(host.Text(Ls.Keys.ColumnOrganizers, "uk"), headers[1]);
        // US-074 FR-002: each organizer is now a person element; CellsOfCode turns each tag into a space, so the text is compared
        // without the space a tag leaves before the comma, and the markup itself must hold none.
        Assert.Equal(second + ", " + Shared, Ls.CellsOfCode(page.Body, code)[0].Replace(" ,", ",", StringComparison.Ordinal));
        Assert.Contains(second + "</span>, <span", page.Body, StringComparison.Ordinal);
    }

    /// <summary>FR-004: a linked code with no organizer left has an empty cell, not a hole in the row.</summary>
    [Fact]
    public async Task ACodeWithNoOrganizerLeft_HasAnEmptyCell()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.SeedPersonLinkAsync("lnk-noss-ion", world.Algebra, world.Maker, At, ct);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Linked), ct);

        var cells = Ls.CellsOfCode(page.Body, "lnk-noss-ion");
        Assert.Equal(string.Empty, cells[0]);
        Assert.Equal(Ls.TableHeaders(page.Body).Count - 1, cells.Count);
    }

    /// <summary>AC-010, FR-004, FR-007: the form: its place, the label, the attributes, the hidden list and size, no page.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheForm_HasItsFieldsAndKeepsListAndSize(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);
        if (language == UiLanguageTestData.English)
        {
            await client.ChooseLanguageAsync(Mc.Path, UiLanguageTestData.English, ct);
        }

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Linked, "алгебр", 0, 7), ct);

        var formTag = Ls.TagOf(page.Body, Ls.MeetForm);
        Assert.Equal("get", Ls.AttributeOf(formTag, "method"));
        Assert.Equal(Mc.Path, Ls.AttributeOf(formTag, "action"));
        var form = Ls.ElementOf(page.Body, Ls.MeetForm)!;
        var field = Ls.TagOf(form, Ls.MeetField);
        Assert.Equal("search", Ls.AttributeOf(field, "type"));
        Assert.Equal("q", Ls.AttributeOf(field, "name"));
        Assert.Equal("100", Ls.AttributeOf(field, "maxlength"));
        Assert.Equal("off", Ls.AttributeOf(field, "autocomplete"));
        Assert.Equal("алгебр", Ls.AttributeOf(field, "value"));
        Assert.Equal(host.Text(Ls.Keys.MeetCodesSearch, language), Ls.LabelOf(form, Ls.MeetField));
        Assert.Equal([Mc.Lists.Linked], Ls.HiddenValues(form, "list"));
        Assert.Equal(["7"], Ls.HiddenValues(form, "size"));
        Assert.Empty(Ls.HiddenValues(form, "page"));
        Assert.True(
            page.Body.IndexOf(Ls.MeetForm, StringComparison.Ordinal) < page.Body.IndexOf(Ls.MeetLists, StringComparison.Ordinal),
            "The form must come before the list links.");
    }

    /// <summary>AC-006, FR-004: the list links reset to page 0 and carry the search and the size; the switcher path holds both.</summary>
    [Fact]
    public async Task ListLinks_CarryTheSearch_AndResetThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedManyUnassignedAsync(host, 25, ct);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned, "pag", 1, 20), ct);

        Assert.Equal(
            [
                Ls.MeetUrl(Mc.Lists.Unassigned, "pag", 0, 20),
                Ls.MeetUrl(Mc.Lists.Linked, "pag", 0, 20),
                Ls.MeetUrl(Mc.Lists.NotACourse, "pag", 0, 20),
            ],
            Cl.HrefsInside(page.Body, Ls.MeetLists, "nav"));
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, "pag", 1, 20), UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>AC-006, FR-004: paging walks the matches and each link carries the search.</summary>
    [Fact]
    public async Task PagingLinks_CarryTheSearch_AndWalkTheMatches()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedManyUnassignedAsync(host, 25, ct);
        await host.SeedMeetingAsync("oth-unas-001", Mc.Email("other.person"), At, ct);

        var first = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned, "pag", 0, 20), ct);
        Assert.Equal(20, Ls.ListedCodes(first.Body).Count);
        Assert.Equal([Ls.MeetUrl(Mc.Lists.Unassigned, "pag", 1, 20)], Cl.HrefsInside(first.Body, Ls.MeetPaging, "nav"));

        var second = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned, "pag", 1, 20), ct);
        Assert.Equal(5, Ls.ListedCodes(second.Body).Count);
        Assert.Equal([Ls.MeetUrl(Mc.Lists.Unassigned, "pag", 0, 20)], Cl.HrefsInside(second.Body, Ls.MeetPaging, "nav"));
    }

    /// <summary>
    /// I-6, api-design D-4: a page past the last shows the last, with a search and without; the rows' forms and the return
    /// path hold the effective page. The default size is 20: 25 codes make two pages.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("pag")]
    public async Task APagePastTheLast_ShowsTheLast(string? text)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedManyUnassignedAsync(host, 25, ct);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned, text, 9, 20), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(5, Ls.ListedCodes(page.Body).Count);
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, text, 1, 20), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.All(Ls.HiddenValues(page.Body, Mc.Fields.ReturnPage), value => Assert.Equal("1", value));
    }

    /// <summary>I-6, D-4: a total of 0 gives page 0.</summary>
    [Fact]
    public async Task APagePastTheEnd_OfNoMatches_IsPageZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned, "нікого", 9, 20), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, "нікого", 0, 20), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.Equal(host.Text(Ls.Keys.MeetCodesNoMatch, "uk"), Html.ElementText(page.Body, Ls.MeetEmpty));
    }

    /// <summary>AC-004, FR-006: a list with no match under a search shows the one new message; with no search its own stays.</summary>
    [Theory]
    [InlineData(Mc.Lists.Unassigned)]
    [InlineData(Mc.Lists.Linked)]
    [InlineData(Mc.Lists.NotACourse)]
    public async Task EmptyMessages_DifferBetweenASearchAndNone(string list)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.PrepareAsync(ct);

        var none = await client.GetAsync(Ls.MeetUrl(list), ct);
        Assert.Equal(host.Text(Ls.Keys.MeetEmptyOf(list), "uk"), Html.ElementText(none.Body, Ls.MeetEmpty));

        var searched = await client.GetAsync(Ls.MeetUrl(list, "нікого"), ct);
        Assert.Equal(HttpStatusCode.OK, searched.Status);
        Assert.Equal(host.Text(Ls.Keys.MeetCodesNoMatch, "uk"), Html.ElementText(searched.Body, Ls.MeetEmpty));
    }

    /// <summary>
    /// AC-007, VR-003, I-5, I-13: a refused text answers 400 with the message, the field empty, no counters, no table, no
    /// empty message, no paging, a return path without it, and the text nowhere in the page.
    /// </summary>
    [Theory]
    [MemberData(nameof(Ls.RefusedTexts), MemberType = typeof(Ls))]
    public async Task ARefusedText_Answers400_ShowsNoList_AndIsNotEchoed(string fragment, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Mc.Path + "?list=linked&size=7&" + fragment, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(host.Text(key, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Null(Ls.TagOf(page.Body, "meet-codes-table"));
        Assert.Null(Ls.TagOf(page.Body, Ls.MeetEmpty));
        Assert.Null(Ls.TagOf(page.Body, Ls.MeetPaging));
        Assert.DoesNotContain(LinkedCode, page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("(1)", Ls.ListsNavText(page.Body), StringComparison.Ordinal);
        Assert.DoesNotContain("(0)", Ls.ListsNavText(page.Body), StringComparison.Ordinal);
        Assert.NotNull(Ls.TagOf(page.Body, Ls.MeetField));
        Assert.Null(Ls.FieldValue(page.Body, Ls.MeetField));
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Linked, null, 0, 7), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.DoesNotContain(Ls.Needle, page.Body, StringComparison.Ordinal);
    }

    /// <summary>VR-004: invalid list, page or size with a valid search keep the fallback, with the search still applied.</summary>
    [Theory]
    [InlineData("list=nonsense")]
    [InlineData("page=-1")]
    [InlineData("size=500")]
    public async Task AnInvalidOtherParameter_KeepsTheFallback_WithTheSearchApplied(string other)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);
        await host.SeedMeetingAsync("oth-unas-001", Mc.Email("other.person"), At, ct);

        var page = await client.GetAsync(Mc.Path + "?" + other + "&q=abcdefg", ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(host.Text(Ls.Keys.QueryInvalid, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Equal([UnassignedCode], Ls.ListedCodes(page.Body));
        Assert.Equal("abcdefg", Ls.FieldValue(page.Body, Ls.MeetField));
    }

    /// <summary>VR-004, D-3: both invalid: VR-003 applies and only the search message shows.</summary>
    [Fact]
    public async Task BothInvalid_ShowTheSearchRefusalOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Mc.Path + "?list=nonsense&q=" + Ls.Needle + "%07", ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(host.Text(Ls.Keys.SearchControl, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Ls.Keys.QueryInvalid, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Null(Ls.TagOf(page.Body, "meet-codes-table"));
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, null, 0, 20), UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>AC-007: a rejected text reaches no read, and an accepted text reaches the counts and the page (the control).</summary>
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
            beforeStart: h => h.ConfigureServices = services => services.AddScoped<IMeetCodesReadSource>(
                provider => new RecordingMeetCodesReadSource(new MeetCodesReadSource(provider.GetRequiredService<ClassroomAgentDbContext>()), log)));
        await using var _host = host;
        await SeedAsync(host, ct);
        Assert.NotNull(key);

        var refused = await client.GetAsync(Mc.Path + "?" + fragment, ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Empty(log.Reads);

        await client.GetAsync(Ls.MeetUrl(Mc.Lists.Linked, "алгебр"), ct);
        Assert.Equal(
            [(nameof(IMeetCodesReadSource.GetCountsAsync), "алгебр"), (nameof(IMeetCodesReadSource.GetLinkedPageAsync), "алгебр")],
            log.Reads);
    }

    /// <summary>AC-006: the language switch returns to the same validated search and page.</summary>
    [Fact]
    public async Task SwitchingLanguage_ReturnsToTheSameSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedManyUnassignedAsync(host, 25, ct);
        var url = Ls.MeetUrl(Mc.Lists.Unassigned, "pag", 1, 20);

        var first = await client.GetAsync(url, ct);
        var returnPath = UiLanguageTestData.RenderedReturnPath(first.Body);
        Assert.Equal(url, returnPath);
        var switched = await client.ChooseLanguageAsync(url, UiLanguageTestData.English, ct, returnPath: returnPath);

        Assert.Equal(url, switched.LocationPath);
        var after = await client.GetAsync(switched.LocationPath!, ct);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(after.Body));
        Assert.Equal("pag", Ls.FieldValue(after.Body, Ls.MeetField));
        Assert.Equal(5, Ls.ListedCodes(after.Body).Count);
    }

    /// <summary>Spec section 7: the program never builds a code or an email into a query string from a row.</summary>
    [Fact]
    public async Task NoLink_PutsARowsCodeOrEmail_IntoAQueryString()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned, "shared"), ct);

        Assert.Equal([UnassignedCode], Ls.ListedCodes(page.Body));
        Assert.Equal("shared", Ls.FieldValue(page.Body, Ls.MeetField));
        foreach (var href in Cl.Hrefs(page.Body).Where(h => h.Contains('?', StringComparison.Ordinal)))
        {
            var query = href[href.IndexOf('?', StringComparison.Ordinal)..];
            Assert.DoesNotContain(UnassignedCode, query, StringComparison.Ordinal);
            Assert.DoesNotContain(Uri.EscapeDataString(Shared), query, StringComparison.Ordinal);
            Assert.DoesNotContain(Shared, query, StringComparison.Ordinal);
        }
    }
}
