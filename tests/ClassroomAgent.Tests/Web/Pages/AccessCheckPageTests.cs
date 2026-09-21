using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-011 openapi <c>GET /settings/access-check</c>: the page explains the check and offers it, and <b>never runs
/// it</b> — a link, a prefetch or a crawler must never make the installation call Google (API-4; spec FR-007, S-10;
/// api-design §3).
/// </summary>
public sealed class AccessCheckPageTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ThePage_ExplainsTheCheck_AndOffersTheRun()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.Title, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.Explanation, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.RunButton, "uk"), page.Text, StringComparison.Ordinal);
        Assert.True(Html.HasInput(page.Body, Html.AntiforgeryFieldName), "The run form must carry the antiforgery token.");
    }

    /// <summary>Openapi <c>technicalAccount</c>: the page shows which account the check would use — data, not translated.</summary>
    [Fact]
    public async Task ThePage_ShowsTheTechnicalAccountItWouldCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.TechnicalAccountLabel, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(AccessCheckTestData.TechnicalAccount, page.Text, StringComparison.Ordinal);
    }

    /// <summary>API-4, S-10: opening the page calls Google not at all and renders no result.</summary>
    [Fact]
    public async Task OpeningThePage_RunsNothing_AndRendersNoResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Empty(probe.RequestCalls);
        Assert.Null(AccessCheckHostExtensions.VerdictOf(page));
        Assert.Empty(AccessCheckHostExtensions.StepsOf(page));
    }

    /// <summary>Openapi <c>x-writes: none</c>: opening the page writes no audit row.</summary>
    [Fact]
    public async Task OpeningThePage_WritesNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        var before = (await host.AuditRowsAsync(ct)).Count;

        var page = await client.OpenAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(before, (await host.AuditRowsAsync(ct)).Count);
    }

    /// <summary>
    /// AC-006, BR-026: in read-only mode the page is still served, with the reason — the run button is neither
    /// hidden nor disabled, because enforcement is in <c>Application</c> (AD-6, TC-5).
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_ThePageIsServed_WithTheButton_AndCallsNothing(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        var page = await client.OpenAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.RunButton, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", ButtonMarkup(page.Body), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(probe.Calls);
    }

    /// <summary>Spec FR-007: the settings section on the landing page gains the entry leading here.</summary>
    [Fact]
    public async Task TheLandingPage_LinksToThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, landing.Status);
        Assert.Contains("href=\"" + AccessCheckTestData.Path + "\"", landing.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.NavigationEntry, "uk"), landing.Text, StringComparison.Ordinal);
    }

    /// <summary>The submit control of the run form, for the "not disabled" assertion.</summary>
    private static string ButtonMarkup(string body)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            body,
            "<form[^>]*action=\"" + AccessCheckTestData.Path + "\"[^>]*>.*?</form>",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The page must hold a form posting to " + AccessCheckTestData.Path + ".");
        return match.Value;
    }
}
