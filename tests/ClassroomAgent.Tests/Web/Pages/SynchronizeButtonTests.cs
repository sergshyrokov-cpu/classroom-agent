using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-019 FR-004: the "Synchronize" button is a form posting to <c>/synchronization/requests</c> on the Admin's
/// connection page and the Dean's home page, never on the Admin's home page (I-6), and still shown in read-only mode
/// (OD-006) — enforcement is in <c>Application</c>, not in the markup (AD-6).
/// </summary>
public sealed partial class SynchronizeButtonTests(PostgreSqlFixture database)
{
    public static TheoryData<string> BothPages => new(
        SynchronizationRequestTestData.AdminReturnPage,
        SynchronizationRequestTestData.DeanReturnPage);

    [Fact]
    public async Task TheConnectionPage_CarriesTheButton_ForAnAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(SynchronizationRequestTestData.AdminReturnPage, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var form = FormOf(page.Body);
        Assert.NotNull(form);
        Assert.Equal(SynchronizationRequestTestData.Path, Attribute(form.Tag, "action"));
        Assert.Equal("post", Attribute(form.Tag, "method"), ignoreCase: true);
        Assert.True(Html.HasInput(form.Inner, Html.AntiforgeryFieldName), "The form must carry the antiforgery token.");
    }

    [Fact]
    public async Task TheHomePage_CarriesTheButton_ForADean()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;

        var page = await client.GetAsync(SynchronizationRequestTestData.DeanReturnPage, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var form = FormOf(page.Body);
        Assert.NotNull(form);
        Assert.Equal(SynchronizationRequestTestData.Path, Attribute(form.Tag, "action"));
        Assert.Equal("post", Attribute(form.Tag, "method"), ignoreCase: true);
        Assert.True(Html.HasInput(form.Inner, Html.AntiforgeryFieldName), "The form must carry the antiforgery token.");
    }

    /// <summary>I-6: the Admin's home page has no button. Passes before the implementation because nothing renders a form yet.</summary>
    [Fact]
    public async Task TheHomePage_HasNoButton_ForAnAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(SynchronizationRequestTestData.DeanReturnPage, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Null(FormOf(page.Body));
    }

    [Theory]
    [MemberData(nameof(BothPages))]
    public async Task InReadOnlyMode_TheButtonIsStillShown(string pagePath)
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = pagePath == SynchronizationRequestTestData.AdminReturnPage ? Actor.Admin : Actor.Dean;
        var (host, client, _) = await StartAsync(database, actor, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;

        var page = await client.GetAsync(pagePath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.NotNull(FormOf(page.Body));
    }

    private static FormMarkup? FormOf(string html)
    {
        foreach (Match match in FormElement().Matches(html))
        {
            if (Attribute(match.Groups["tag"].Value, "id") == SynchronizationRequestTestData.Markup.FormId)
            {
                return new FormMarkup(match.Groups["tag"].Value, match.Groups["inner"].Value);
            }
        }

        return null;
    }

    private static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, @"\s" + name + @"\s*=\s*(""(?<v>[^""]*)""|'(?<v>[^']*)')", RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["v"].Value) : null;
    }

    [GeneratedRegex(@"(?<tag><form\b[^>]*>)(?<inner>.*?)</form>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FormElement();

    private sealed record FormMarkup(string Tag, string Inner);
}
