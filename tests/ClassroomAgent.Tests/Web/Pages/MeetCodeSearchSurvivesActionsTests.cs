using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-070 FR-005 over HTTP: the search travels through a row action as <c>returnQ</c>, validated, and only the validated text
/// is ever re-emitted: forms and links of the rows, a success's redirect, the refusals that re-render, the course-choice page
/// and its Cancel (I-7), and a <c>returnQ</c> that fails VR-001 being simply no search (AC-006; openapi
/// <c>x-outcomes-with-search</c>, <c>x-components.returnQ-field</c>).
/// </summary>
public sealed class MeetCodeSearchSurvivesActionsTests(PostgreSqlFixture database)
{
    private const string Code = "act-aaaa-bbb";

    private const string Text = "петро іван";

    private static readonly DateTimeOffset At = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private static KeyValuePair<string, string> ReturnQ(string value) => new(Ls.ReturnQ, value);

    private sealed record World(long Alpha, long Beta, long Maker);

    private static async Task<World> SeedAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        var alpha = await host.SeedRosterCourseAsync("Test Course Alpha", [Mc.Email("mc.teacher1")], [], ct);
        var beta = await host.SeedRosterCourseAsync("Test Course Beta", [Mc.Email("mc.teacher2")], [], ct);
        await host.InsertAppUserAsync(ct, email: Mc.Email("code.maker"));
        var maker = await host.AccountIdAsync(Mc.Email("code.maker"), ct);
        await host.SeedMeetingAsync(Code, Mc.Email("mc.teacher1"), At, ct);
        return new World(alpha, beta, maker);
    }

    /// <summary>AC-006, FR-005.1: on the Unassigned list the course-choice link and the mark form carry the validated text.</summary>
    [Fact]
    public async Task AnUnassignedRow_CarriesTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned, "act-aaaa"), ct);

        Assert.Equal([Code], Ls.ListedCodes(page.Body));
        Assert.Contains(Ls.ChoiceUrl(Code, 0, "act-aaaa"), Cl.Hrefs(page.Body));
        Assert.Equal(1, Ls.FormCount(page.Body, "/not-a-course-mark"));
        Assert.Equal(["act-aaaa"], Ls.HiddenValues(page.Body, Ls.ReturnQ));
    }

    /// <summary>AC-006, FR-005.1: on the Linked list the confirm form, the mark form and the re-link link carry it.</summary>
    [Fact]
    public async Task ALinkedRow_CarriesTheSearch_InEveryAction()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.SeedAutoLinkAsync(Code, world.Alpha, At, ct);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Linked, "act-aaaa"), ct);

        Assert.Contains(Ls.ChoiceUrl(Code, 0, "act-aaaa"), Cl.Hrefs(page.Body));
        Assert.Equal(1, Ls.FormCount(page.Body, "/confirmation"));
        Assert.Equal(1, Ls.FormCount(page.Body, "/not-a-course-mark"));
        Assert.Equal(["act-aaaa", "act-aaaa"], Ls.HiddenValues(page.Body, Ls.ReturnQ));
    }

    /// <summary>AC-006, FR-005.1: on the Not-a-course list the pick link carries it.</summary>
    [Fact]
    public async Task AMarkedRow_CarriesTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.SeedMarkAsync(Code, world.Maker, At, ct);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.NotACourse, "act-aaaa"), ct);

        Assert.Contains(Ls.ChoiceUrl(Code, 0, "act-aaaa"), Cl.Hrefs(page.Body));
    }

    /// <summary>FR-004: with no search no field and no parameter is emitted, never an empty one.</summary>
    [Fact]
    public async Task WithNoSearch_NoReturnQIsEmitted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned), ct);

        Assert.Equal([Code], Ls.ListedCodes(page.Body));
        Assert.Contains(Ls.ChoiceUrl(Code, 0, null), Cl.Hrefs(page.Body));
        Assert.Empty(Ls.HiddenValues(page.Body, Ls.ReturnQ));
    }

    /// <summary>AC-006, FR-005.2: a success redirects to the list the code came from, its return page and the search.</summary>
    [Fact]
    public async Task PickingACourse_RedirectsToTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);

        var response = await client.PostWithTokenAsync(
            Mc.LinkPath(Code),
            [
                Mc.Field(Mc.Fields.CourseId, world.Alpha),
                Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned),
                Mc.Field(Mc.Fields.ReturnPage, 2),
                ReturnQ(Text),
            ],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, Text, 2), response.LocationPath);
    }

    /// <summary>AC-006, FR-005.2: confirming redirects to Linked with the search.</summary>
    [Fact]
    public async Task Confirming_RedirectsToTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.SeedAutoLinkAsync(Code, world.Alpha, At, ct);

        var response = await client.PostWithTokenAsync(
            Mc.ConfirmationPath(Code),
            [Mc.Field(Mc.Fields.ExpectedCourseId, world.Alpha), Mc.Field(Mc.Fields.ReturnPage, 1), ReturnQ(Text)],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Linked, Text, 1), response.LocationPath);
    }

    /// <summary>AC-006, FR-005.2: marking redirects to the list the code came from with the search.</summary>
    [Fact]
    public async Task Marking_RedirectsToTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var response = await client.PostWithTokenAsync(
            Mc.MarkPath(Code),
            [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned), Mc.Field(Mc.Fields.ReturnPage, 3), ReturnQ(Text)],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, Text, 3), response.LocationPath);
        Assert.Equal(Code, Assert.Single(await host.McLinkRowsAsync(ct)).MeetingCode);
    }

    /// <summary>AC-006, I-6: the last row on page 1 leaves its list under a search: following the redirect shows the page before.</summary>
    [Fact]
    public async Task APageLeftEmptyByTheAction_ShowsTheLastPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        for (var i = 1; i <= 21; i++)
        {
            await host.SeedMeetingAsync($"pag-{i:D3}-xxx", Mc.Email("shared.organizer"), At.AddMinutes(i), ct);
        }

        var second = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned, "pag", 1, 20), ct);
        var last = Assert.Single(Ls.ListedCodes(second.Body));

        var response = await client.PostWithTokenAsync(
            Mc.MarkPath(last),
            [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned), Mc.Field(Mc.Fields.ReturnPage, 1), ReturnQ("pag")],
            ct);

        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, "pag", 1), response.LocationPath);
        var after = await client.GetAsync(response.LocationPath!, ct);
        Assert.Equal(20, Ls.ListedCodes(after.Body).Count);
        Assert.Equal("pag", Ls.FieldValue(after.Body, Ls.MeetField));
    }

    /// <summary>FR-005.3, D-6: a stale state answers 409 on the code's current list, page 0, with the search.</summary>
    [Fact]
    public async Task AStaleState_KeepsTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.SeedAutoLinkAsync(Code, world.Alpha, At, ct);

        var page = await client.PostWithTokenAsync(
            Mc.MarkPath(Code),
            [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned), Mc.Field(Mc.Fields.ReturnPage, 4), ReturnQ("act-aaaa")],
            ct);

        Assert.Equal(HttpStatusCode.Conflict, page.Status);
        Assert.Contains(host.Text(Mc.Keys.MessageStateChanged, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Equal("act-aaaa", Ls.FieldValue(page.Body, Ls.MeetField));
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Linked, "act-aaaa", 0, 20), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.Equal([Code], Ls.ListedCodes(page.Body));
    }

    /// <summary>FR-005.3, D-6: an unknown code answers 404 on Unassigned, page 0, with the search.</summary>
    [Fact]
    public async Task AnUnknownCode_KeepsTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.PostWithTokenAsync(
            Mc.MarkPath("unk-nown-cod"),
            [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned), Mc.Field(Mc.Fields.ReturnPage, 4), ReturnQ("act-aaaa")],
            ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Contains(host.Text(Mc.Keys.MessageCodeNotFound, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Equal("act-aaaa", Ls.FieldValue(page.Body, Ls.MeetField));
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, "act-aaaa", 0, 20), UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>FR-005.3: the same course answers 400 on the course-choice page, carrying the search into its form.</summary>
    [Fact]
    public async Task TheSameCourse_KeepsTheSearch_OnTheChoicePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.SeedAutoLinkAsync(Code, world.Alpha, At, ct);

        var page = await client.PostWithTokenAsync(
            Mc.LinkPath(Code),
            [
                Mc.Field(Mc.Fields.CourseId, world.Alpha),
                Mc.Field(Mc.Fields.ExpectedState, Mc.States.Linked),
                Mc.Field(Mc.Fields.ExpectedCourseId, world.Alpha),
                Mc.Field(Mc.Fields.ReturnPage, 2),
                ReturnQ(Text),
            ],
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(host.Text(Mc.Keys.FieldSameCourse, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Equal([Text], Ls.HiddenValues(Ls.ElementOf(page.Body, Ls.ChoiceForm)!, Ls.ReturnQ));
        Assert.Equal(Ls.ChoiceUrl(Code, 2, Text), UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>FR-005.3: an unknown course answers 404 on the course-choice page, carrying the search into its form.</summary>
    [Fact]
    public async Task AnUnknownCourse_KeepsTheSearch_OnTheChoicePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.PostWithTokenAsync(
            Mc.LinkPath(Code),
            [
                Mc.Field(Mc.Fields.CourseId, 987654321),
                Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned),
                Mc.Field(Mc.Fields.ReturnPage, 2),
                ReturnQ(Text),
            ],
            ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Contains(host.Text(Mc.Keys.FieldCourseNotFound, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Equal([Text], Ls.HiddenValues(Ls.ElementOf(page.Body, Ls.ChoiceForm)!, Ls.ReturnQ));
    }

    /// <summary>FR-005: a malformed form answers the error page as it does today, not the list and not the search.</summary>
    [Fact]
    public async Task AMalformedForm_AnswersTheErrorPage_NotTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.PostWithTokenAsync(
            Mc.MarkPath(Code), [Mc.Field(Mc.Fields.ExpectedState, "nonsense"), ReturnQ(Ls.Needle)], ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Null(Ls.TagOf(page.Body, Ls.MeetField));
        Assert.DoesNotContain(Ls.Needle, page.Body, StringComparison.Ordinal);
    }

    /// <summary>FR-005.5: a <c>returnQ</c> failing VR-001 is no search: the action is unaffected and it is not echoed.</summary>
    [Theory]
    [MemberData(nameof(Ls.RefusedReturnTexts), MemberType = typeof(Ls))]
    public async Task ARefusedReturnQ_IsNoSearch_AndTheActionGoesOn(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var response = await client.PostWithTokenAsync(
            Mc.MarkPath(Code),
            [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned), Mc.Field(Mc.Fields.ReturnPage, 0), ReturnQ(text)],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, null, 0), response.LocationPath);
        Assert.Single(await host.McLinkRowsAsync(ct));
        Assert.DoesNotContain(Ls.Needle, await host.McLinksFingerprintAsync(ct), StringComparison.Ordinal);
        var audits = await host.AuditRowsAsJsonAsync(ct);
        Assert.NotEmpty(audits);
        Assert.DoesNotContain(audits, row => row.Contains(Ls.Needle, StringComparison.Ordinal));
    }

    /// <summary>
    /// FR-005.5, api-design D-2: a repeated <c>returnQ</c> is no search, though any other repeated known field refuses the form.
    /// The control shows the same form with a repeated known field being malformed.
    /// </summary>
    [Fact]
    public async Task ARepeatedReturnQ_IsNoSearch_WhereARepeatedKnownFieldIsMalformed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var repeatedKnown = await client.PostWithTokenAsync(
            Mc.MarkPath(Code),
            [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned), Mc.Field(Mc.Fields.ReturnPage, 0), Mc.Field(Mc.Fields.ReturnPage, 1)],
            ct);
        Assert.Equal(HttpStatusCode.BadRequest, repeatedKnown.Status);

        var repeatedSearch = await client.PostWithTokenAsync(
            Mc.MarkPath(Code),
            [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned), Mc.Field(Mc.Fields.ReturnPage, 0), ReturnQ(Ls.Needle), ReturnQ(Ls.Needle + "2")],
            ct);
        Assert.Equal(HttpStatusCode.Redirect, repeatedSearch.Status);
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, null, 0), repeatedSearch.LocationPath);
    }

    /// <summary>FR-005.1, FR-005.4, I-7: the choice page carries the text in its form, its return path and Cancel, which goes to the code's list and page.</summary>
    [Theory]
    [InlineData("unassigned")]
    [InlineData("linked")]
    [InlineData("not-a-course")]
    public async Task TheChoicePage_CarriesTheSearch_AndCancelReturnsToTheCodesList(string list)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        if (list == Mc.Lists.Linked)
        {
            await host.SeedAutoLinkAsync(Code, world.Alpha, At, ct);
        }
        else if (list == Mc.Lists.NotACourse)
        {
            await host.SeedMarkAsync(Code, world.Maker, At, ct);
        }

        var page = await client.GetAsync(Ls.ChoiceUrl(Code, 3, Text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var form = Ls.ElementOf(page.Body, Ls.ChoiceForm)!;
        Assert.Equal([Text], Ls.HiddenValues(form, Ls.ReturnQ));
        Assert.Equal(Ls.ChoiceUrl(Code, 3, Text), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.Contains(Ls.MeetUrl(list, Text, 3), Cl.Hrefs(form));
    }

    /// <summary>I-7: with no search Cancel also returns to the code's list and page.</summary>
    [Fact]
    public async Task WithNoSearch_CancelReturnsToTheCodesList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.SeedMarkAsync(Code, world.Maker, At, ct);

        var page = await client.GetAsync(Ls.ChoiceUrl(Code, 3, null), ct);

        var form = Ls.ElementOf(page.Body, Ls.ChoiceForm)!;
        Assert.Empty(Ls.HiddenValues(form, Ls.ReturnQ));
        Assert.Contains(Ls.MeetUrl(Mc.Lists.NotACourse, null, 3), Cl.Hrefs(form));
    }

    /// <summary>FR-005.5, spec section 7: on the choice page a <c>returnQ</c> failing VR-001 is no search: answered 200, echoed nowhere.</summary>
    [Theory]
    [InlineData(Ls.Needle + "%07")]
    public async Task OnTheChoicePage_ARefusedReturnQ_IsNoSearch(string raw)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Ls.ChoiceUrl(Code, 3, null) + "&returnQ=" + raw, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain(Ls.Needle, page.Body, StringComparison.Ordinal);
        Assert.Equal(Ls.ChoiceUrl(Code, 3, null), UiLanguageTestData.RenderedReturnPath(page.Body));

        // Control: an accepted text passes, so the page is reading the parameter at all.
        var accepted = await client.GetAsync(Ls.ChoiceUrl(Code, 3, "act-aaaa"), ct);
        Assert.Equal(Ls.ChoiceUrl(Code, 3, "act-aaaa"), UiLanguageTestData.RenderedReturnPath(accepted.Body));
    }

    /// <summary>AC-006: the choice page's language switch returns to the same page with the same search.</summary>
    [Fact]
    public async Task SwitchingLanguage_OnTheChoicePage_KeepsTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);
        var url = Ls.ChoiceUrl(Code, 3, Text);

        var first = await client.GetAsync(url, ct);
        var returnPath = UiLanguageTestData.RenderedReturnPath(first.Body);
        Assert.Equal(url, returnPath);
        var switched = await client.ChooseLanguageAsync(url, UiLanguageTestData.English, ct, returnPath: returnPath);

        Assert.Equal(url, switched.LocationPath);
        var after = await client.GetAsync(switched.LocationPath!, ct);
        Assert.Equal([Text], Ls.HiddenValues(Ls.ElementOf(after.Body, Ls.ChoiceForm)!, Ls.ReturnQ));
    }

    /// <summary>
    /// AC-008, FR-005, spec section 7: a read-only refusal stays the host's 409 and writes no text into its audit row; the
    /// control is the same post outside read-only mode carrying the search (see <see cref="Marking_RedirectsToTheSearch"/>).
    /// </summary>
    [Fact]
    public async Task InReadOnlyMode_ARowActionIsRefused_AndNoTextIsAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.PostWithTokenAsync(
            Mc.MarkPath(Code), [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned), ReturnQ(Ls.Needle)], ct);

        Assert.Equal(HttpStatusCode.Conflict, page.Status);
        Assert.Empty(await host.McLinkRowsAsync(ct));
        var audits = await host.AuditRowsAsJsonAsync(ct);
        Assert.NotEmpty(audits);
        Assert.DoesNotContain(audits, row => row.Contains(Ls.Needle, StringComparison.Ordinal));
        Assert.DoesNotContain(Ls.Needle, page.Body, StringComparison.Ordinal);
    }
}
