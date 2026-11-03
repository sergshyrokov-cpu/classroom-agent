using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using S = ClassroomAgent.Tests.TestInfrastructure.SearchTestData;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-071 AC-012, FR-010, I-13, api-design D-3: the error page holds neither the language switcher nor the search field, under
/// any endpoint, so it can never copy the raw query into a hidden field. The defect was established by reading the code; the
/// red phase of <see cref="AMalformedCode_OnTheCourseChoice_LeavesTheMarkerNowhere"/> shows it by a request.
/// </summary>
public sealed class ErrorPageHeaderTests(PostgreSqlFixture database)
{
    /// <summary>A text that appears only if a raw query is copied into the page.</summary>
    private const string Marker = "zzrawmarker";

    /// <summary>Over the 64 characters of US-032 VR-001.</summary>
    private static readonly string MalformedCode = new('x', 65);

    private static string MalformedChoiceUrl(string language = "") =>
        Mc.Path + "/" + MalformedCode + "/course-choice?returnPage=1&" + Ls.ReturnQ + "=" + Marker + language;

    private static void AssertBareErrorPage(PageResponse page)
    {
        // The marker first: in the red phase this line is the defect of AC-012 shown by a request.
        Assert.DoesNotContain(Marker, page.Body, StringComparison.Ordinal);
        Assert.False(UiLanguageTestData.HasSwitcher(page.Body), "The error page has a language switcher.");
        Assert.False(S.HasHeaderField(page.Body), "The error page has the search field.");
    }

    /// <summary>AC-012: the marker is nowhere in the page, the switcher's hidden field included; the page keeps its text and back link.</summary>
    [Theory]
    [InlineData(Actor.Admin)]
    [InlineData(Actor.Dean)]
    public async Task AMalformedCode_OnTheCourseChoice_LeavesTheMarkerNowhere(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;

        var page = await client.GetAsync(MalformedChoiceUrl(), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(host.Text(SignInTestData.TextKeys.PageExpired, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(Mc.Path, S.Hrefs(page.Body));
        AssertBareErrorPage(page);
    }

    /// <summary>FR-010: the error page keeps the user's language with no switcher.</summary>
    [Fact]
    public async Task TheErrorPage_StaysInTheUsersLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await client.ChooseLanguageAsync(SignInTestData.LandingPath, UiLanguageTestData.English, ct);

        var page = await client.GetAsync(MalformedChoiceUrl(), ct);

        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Contains(host.Text(SignInTestData.TextKeys.PageExpired, "en"), page.Text, StringComparison.Ordinal);
        AssertBareErrorPage(page);
    }

    /// <summary>FR-010: the read-only refusal, answered under a protected POST endpoint, has neither control.</summary>
    [Fact]
    public async Task TheReadOnlyRefusal_HasNoHeaderControls()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;
        await host.PrepareAsync(ct);
        await host.SeedMeetingAsync("act-aaaa-bbb", Mc.Email("org.one"), SearchHostExtensions.At, ct);

        var page = await client.PostWithTokenAsync(
            Mc.MarkPath("act-aaaa-bbb") + "?zz=" + Marker,
            [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned), new(Ls.ReturnQ, Marker)],
            ct);

        Assert.Equal(HttpStatusCode.Conflict, page.Status);
        AssertBareErrorPage(page);
    }

    /// <summary>FR-010: the antiforgery refusal, answered under a protected POST endpoint, has neither control.</summary>
    [Fact]
    public async Task TheAntiforgeryRefusal_HasNoHeaderControls()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.PrepareAsync(ct);

        var page = await client.PostWithTokenAsync(
            Mc.MarkPath("act-aaaa-bbb") + "?zz=" + Marker,
            [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned)],
            ct,
            withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        AssertBareErrorPage(page);
    }

    /// <summary>FR-010: a not-found page answered under a protected endpoint (an unknown course card) has neither control.</summary>
    [Fact]
    public async Task ANotFoundCard_HasNoHeaderControls()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;

        var page = await client.GetAsync(CourseCardTestData.Path(987654) + "?zz=" + Marker, ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        AssertBareErrorPage(page);
    }

    /// <summary>FR-010, section 7: other pages' switchers are unchanged: a page that sets no return path keeps the fallback.</summary>
    [Fact]
    public async Task AnOrdinaryPage_KeepsItsSwitcher_AndItsFallback()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;

        var page = await client.GetAsync(SignInTestData.LandingPath + "?zz=" + Marker, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(UiLanguageTestData.HasSwitcher(page.Body));
        Assert.Equal(SignInTestData.LandingPath + "?zz=" + Marker, UiLanguageTestData.RenderedReturnPath(page.Body));
    }
}
