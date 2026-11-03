using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using S = ClassroomAgent.Tests.TestInfrastructure.SearchTestData;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>
/// US-072 FR-001 … FR-004: the Control Plane's error page has no language switcher under any endpoint, so it can never copy the
/// raw query into a hidden field. The defect was established by reading the code; the red phase of the refusal tests shows it by
/// a request. New class, because AC-004 keeps <see cref="OwnerUiLanguageTests"/> unchanged (I-5).
/// </summary>
public sealed class OwnerErrorPageHeaderTests(PostgreSqlFixture database)
{
    /// <summary>A text that appears only if a raw query is copied into the page.</summary>
    private const string Marker = "zzrawmarker";

    private const string Query = "?zz=" + Marker;

    private const string Home = "/";

    private const string Installations = "/installations";

    /// <summary>A well-formed identifier no installation has, so the protected card answers <c>404</c>.</summary>
    private const string UnknownCard = Installations + "/7f3c0000-0000-4000-8000-000000000072";

    private static void AssertBareErrorPage(PageResponse page)
    {
        // The marker first: in the red phase this line is the defect of AC-001 shown by a request.
        Assert.DoesNotContain(Marker, page.Body, StringComparison.Ordinal);
        Assert.False(UiLanguageTestData.HasSwitcher(page.Body), "The error page has a language switcher.");
        Assert.DoesNotContain("aria-current", page.Body, StringComparison.Ordinal);

        // Section 7: with the switcher gone, nothing on the error page posts.
        Assert.DoesNotContain("<form", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.False(Html.HasInput(page.Body, Html.AntiforgeryFieldName), "The error page has an antiforgery token.");
    }

    /// <summary>AC-001 (a), FR-001 row 1: an antiforgery refusal on either protected POST; text and back link stay (FR-003, I-4).</summary>
    [Theory]
    [InlineData(Installations)]
    [InlineData(UiLanguageTestData.ChoosePath)]
    public async Task TheAntiforgeryRefusal_LeavesTheMarkerNowhere(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await owner.GetAsync(Home, ct);

        var page = await owner.PostFormAsync(path + Query, [], ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(host.Text("Error.PageExpired", UiLanguageTestData.Ukrainian), page.Text, StringComparison.Ordinal);
        Assert.Equal([Home], S.Hrefs(page.Body));
        AssertBareErrorPage(page);
    }

    /// <summary>AC-001 (b), FR-001 row 2: a refused language code; neither the query nor the refused value reaches the page.</summary>
    [Theory]
    [InlineData("de")]
    [InlineData(UiLanguageTestData.RejectedMarker)]
    public async Task ARefusedLanguageCode_LeavesTheMarkerNowhere(string code)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await owner.GetAsync(Home, ct);

        var page = await owner.PostFormAsync(
            UiLanguageTestData.ChoosePath + Query,
            [new(UiLanguageTestData.Fields.Language, code), new(UiLanguageTestData.Fields.ReturnPath, Home)],
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(host.Text("Error.PageExpired", UiLanguageTestData.Ukrainian), page.Text, StringComparison.Ordinal);
        Assert.Equal([Home], S.Hrefs(page.Body));
        Assert.DoesNotContain(UiLanguageTestData.RejectedMarker, page.Body, StringComparison.Ordinal);
        AssertBareErrorPage(page);
    }

    /// <summary>AC-002, FR-001 row 3, I-2: a guard that passes before the fix; an unmatched address and a protected card's <c>404</c>.</summary>
    [Theory]
    [InlineData("/nope")]
    [InlineData(UnknownCard)]
    public async Task ANotFoundAnswer_LeavesTheMarkerNowhere(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var page = await owner.GetAsync(path + Query, ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Contains(host.Text("Error.NotFound", UiLanguageTestData.Ukrainian), page.Text, StringComparison.Ordinal);
        AssertBareErrorPage(page);
    }

    /// <summary>AC-003, FR-003, I-3: an Owner who chose English gets the refusal in English with no switcher.</summary>
    [Theory]
    [InlineData(false, UiLanguageTestData.English)]
    [InlineData(true, "de")]
    public async Task TheRefusal_StaysInTheOwnersLanguage(bool withToken, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await owner.ChooseLanguageAsync(Home, UiLanguageTestData.English, ct);
        await owner.GetAsync(Home, ct);

        var page = await owner.PostFormAsync(
            UiLanguageTestData.ChoosePath + Query,
            [new(UiLanguageTestData.Fields.Language, code)],
            ct,
            withToken);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Contains(host.Text("Error.PageExpired", UiLanguageTestData.English), page.Text, StringComparison.Ordinal);
        AssertBareErrorPage(page);
    }

    /// <summary>AC-004, FR-004: an ordinary page keeps its switcher and the fallback of the raw path and query.</summary>
    [Fact]
    public async Task AnOrdinaryPage_KeepsItsSwitcher_AndItsFallback()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var page = await owner.GetAsync(Installations + Query, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(UiLanguageTestData.HasSwitcher(page.Body), "An ordinary page lost its language switcher.");
        Assert.Equal(Installations + Query, UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>
    /// AC-004, FR-002: the rule looks at the page and not at the status: a form returned with <c>400</c> is an ordinary page and
    /// keeps its switcher.
    /// </summary>
    [Fact]
    public async Task AFormRefusedByValidation_KeepsItsSwitcher()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await owner.GetAsync(Home, ct);

        var page = await owner.PostFormAsync(Installations, [], ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.True(UiLanguageTestData.HasSwitcher(page.Body), "A form refused by validation lost its language switcher.");
    }
}
