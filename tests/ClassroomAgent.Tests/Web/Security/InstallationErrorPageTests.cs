using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-008 AC-016: one error page serves <c>400</c>, <c>403</c>, <c>404</c> and <c>500</c>, each with its own
/// translated text and nothing else — no exception message, no stack trace, no request body, no personal datum
/// (spec FR-005, FR-018; S-19; SC-4, SC-6, NFR-023).
/// </summary>
public sealed class InstallationErrorPageTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.Json(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    public static TheoryData<int, string> Statuses => new()
    {
        { 400, SignInTestData.TextKeys.PageExpired },
        { 403, SignInTestData.TextKeys.Forbidden },
        { 404, SignInTestData.TextKeys.NotFound },
        { 500, SignInTestData.TextKeys.Unexpected },
    };

    [Theory]
    [MemberData(nameof(Statuses))]
    public async Task EachStatus_GetsItsOwnTranslatedText(int status, string textKey)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(SignInTestData.ErrorPath(status), ct);

        Assert.Equal(status, (int)page.Status);
        Assert.Contains(host.Text(textKey, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-016, SC-4: the error page is anonymous and reads and writes nothing.</summary>
    [Fact]
    public async Task TheErrorPage_IsAnonymousAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(SignInTestData.ErrorPath(404), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Empty(await host.AppUsersAsync(ct));
        Assert.Empty(await host.AuditRowsAsync(ct));
    }

    /// <summary>AC-016, S-19: the page leaks no internals.</summary>
    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task TheErrorPage_LeaksNoInternals(int status)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(SignInTestData.ErrorPath(status), ct);

        Assert.DoesNotContain("   at ", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("ClassroomAgent.", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("D:\\", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-016, SC-4 v66: an unmatched address answers 404 from the anonymous catch-all, to anyone.</summary>
    [Fact]
    public async Task AnUnmatchedAddress_AnswersTheErrorPageWith404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync("/no-such-page", ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Contains(host.Text(SignInTestData.TextKeys.NotFound, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Null(page.Location);
    }

    /// <summary>AC-016: the antiforgery text says the page is out of date and offers the same page again.</summary>
    [Fact]
    public async Task TheAntiforgeryRefusal_OffersThePageAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var refused = await client.StartGoogleSignInAsync(ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Contains(host.Text(SignInTestData.TextKeys.PageExpired, "uk"), refused.Text, StringComparison.Ordinal);
        Assert.Contains(SignInTestData.SignInPath, refused.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-016, TC-5 v66: a forbidden role gets 403 with the error page — not a redirect and not 404.</summary>
    [Fact]
    public async Task AForbiddenRequest_Gets403WithTheErrorPage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(SignInTestData.ErrorPath(403), ct);

        Assert.Equal(HttpStatusCode.Forbidden, page.Status);
        Assert.Contains(host.Text(SignInTestData.TextKeys.Forbidden, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Null(page.Location);
    }

    /// <summary>AC-016, SC-6: the developer exception page is not in play outside local development.</summary>
    [Fact]
    public async Task TheDeveloperExceptionPage_IsNotInPlay()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(SignInTestData.ErrorPath(500), ct);

        Assert.DoesNotContain("Developer Exception Page", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stack Query Cookies Headers", page.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-016: the error page renders in the school's language, in both of them.</summary>
    [Fact]
    public async Task TheErrorPage_RendersInTheSchoolsLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.DefaultLanguage] = "en";
        host.Start();
        using var client = host.CreateClient();

        var page = await client.GetAsync(SignInTestData.ErrorPath(404), ct);

        Assert.Contains(host.Text(SignInTestData.TextKeys.NotFound, "en"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-016, SC-4: static files come only from the application's own static-files directory.</summary>
    [Fact]
    public async Task StaticFiles_ComeOnlyFromTheApplicationsOwnDirectory()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var traversal = await client.GetAsync("/../appsettings.json", ct);
        var settings = await client.GetAsync("/appsettings.json", ct);

        Assert.NotEqual(HttpStatusCode.OK, traversal.Status);
        Assert.NotEqual(HttpStatusCode.OK, settings.Status);
    }
}
