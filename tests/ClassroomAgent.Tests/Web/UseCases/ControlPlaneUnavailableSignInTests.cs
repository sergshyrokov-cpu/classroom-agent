using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-008 AC-009: a Control Plane that does not answer <em>refuses</em> the Admin sign-in — it never falls back
/// to an earlier answer, a cached decision or "they signed in before" — and each cause is classified as
/// api-design 2.4 fixes it (spec FR-008, FR-010, FR-020; S-03; BR-012; TC-5).
/// </summary>
public sealed class ControlPlaneUnavailableSignInTests(PostgreSqlFixture database)
{
    /// <summary>Every row of the api-design 2.4 classification table that must refuse the sign-in.</summary>
    public static TheoryData<string, string> UnavailableAnswers => new()
    {
        { "connection failure", "connection" },
        { "500 with no body", "error-500" },
        { "503 with an outcome body", "error-503" },
        { "200 with unparseable JSON", "unparseable" },
        { "200 with a JSON array", "array" },
        { "200 without the allowed property", "missing-property" },
        { "200 with allowed as a string", "wrong-type" },
        { "302 to the setup page", "setup-redirect" },
        { "404 without the outcome body", "bare-404" },
    };

    [Theory]
    [MemberData(nameof(UnavailableAnswers))]
    public async Task AnUnusableAnswer_RefusesTheSignIn_WithTheUnavailableCategory(string scenario, string shape)
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Answering(shape);
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        Assert.True(
            callback.LocationPath == SignInTestData.SignInPath,
            $"{scenario}: expected a refusal back to the sign-in page, got '{callback.LocationPath}'.");
        Assert.Null(callback.SetCookie(SignInTestData.SessionCookieName));
        Assert.Empty(await host.AppUsersAsync(ct));
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.ControlPlaneUnavailable, audit.RefusalCategory);
    }

    /// <summary>AC-009, api-design 2.4: a 404 <em>with</em> the outcome body is the unknown-installation category.</summary>
    [Fact]
    public async Task A404WithTheOutcomeBody_IsTheUnknownInstallationCategory()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = ScriptedHttpHandler.AdminLoginCheckJson(
            HttpStatusCode.NotFound,
            AdminLoginCheckTestData.OutcomeJson("unknown_installation"));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(SignInTestData.SignInPath, callback.LocationPath);
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.UnknownInstallation, audit.RefusalCategory);
    }

    /// <summary>
    /// AC-009, api-design 2.4: the load-bearing distinction. A Control Plane too old to route the new path
    /// answers a bare 404, and reading that as "not approved" would tell an operator the Owner revoked
    /// somebody after a failed deployment.
    /// </summary>
    [Fact]
    public async Task ABare404_IsNeverReadAsNotApproved()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = ScriptedHttpHandler.AdminLoginCheck(
            _ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.NotFound));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);

        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.ControlPlaneUnavailable, audit.RefusalCategory);
        Assert.NotEqual(SignInTestData.RefusalCategories.NotInAllowedAdmin, audit.RefusalCategory);
    }

    /// <summary>AC-009, S-03: a successful sign-in yesterday grants nothing today.</summary>
    [Fact]
    public async Task AnEarlierSuccess_DoesNotAdmitTheAdminWhenTheControlPlaneIsSilent()
    {
        var ct = TestContext.Current.CancellationToken;
        var answers = new Queue<string>(["allowed", "connection"]);
        var channel = ScriptedHttpHandler.AdminLoginCheck(
            request => Answer(answers.Dequeue(), request, CancellationToken.None).GetAwaiter().GetResult());
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        var (_, first) = await host.SignInWithGoogleAsync(ct);
        var stampAfterSuccess = (await host.AppUsersAsync(ct)).Single().LastSuccessfulSignInAt;
        host.Time.Advance(TimeSpan.FromHours(20));
        var (_, second) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(SignInTestData.LandingPath, first.LocationPath);
        Assert.Equal(SignInTestData.SignInPath, second.LocationPath);
        Assert.Null(second.SetCookie(SignInTestData.SessionCookieName));
        Assert.Equal(stampAfterSuccess, (await host.AppUsersAsync(ct)).Single().LastSuccessfulSignInAt);
    }

    /// <summary>AC-009: the refusal is a translated message, not a raw error or a stack trace.</summary>
    [Fact]
    public async Task TheRefusalMessage_SaysTheApprovalCouldNotBeConfirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Answering("connection");
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        var (client, _) = await host.SignInWithGoogleAsync(ct);
        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Contains(
            host.Text(SignInTestData.TextKeys.RefusedCouldNotConfirm, "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpRequestException", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-009, AC-018, SC-10: the cause is logged at Error as a category, without the response body.</summary>
    [Fact]
    public async Task TheCause_IsLoggedAsAnErrorCategory_WithoutTheResponseBody()
    {
        var ct = TestContext.Current.CancellationToken;
        const string marker = "canary-body-of-the-control-plane-answer";
        var channel = ScriptedHttpHandler.AdminLoginCheckJson(
            HttpStatusCode.InternalServerError,
            $"{{\"detail\":\"{marker}\"}}");
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);
        var events = await host.ReadLogEventsAsync(ct);

        var log = string.Join("\n", events.Select(e => e.Line));
        Assert.Contains(events, e => e.Level == "Error");
        Assert.DoesNotContain(marker, log, StringComparison.Ordinal);
        Assert.DoesNotContain(SignInTestData.AdminEmail, log, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-009, BR-012: the background legitimacy check is not affected by a refused Admin sign-in.</summary>
    [Fact]
    public async Task TheLegitimacyState_IsUntouchedByARefusedSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Answering("connection");
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var before = await host.LegitimacyStatesAsync(ct);

        await host.SignInWithGoogleAsync(ct);

        Assert.Equal(before, await host.LegitimacyStatesAsync(ct));
    }

    private static ScriptedHttpHandler Answering(string shape) =>
        ScriptedHttpHandler.AdminLoginCheck(request => Answer(shape, request, CancellationToken.None).GetAwaiter().GetResult());

    private static Task<HttpResponseMessage> Answer(string shape, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        return shape switch
        {
            "allowed" => Task.FromResult(ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true))),
            "connection" => Task.FromException<HttpResponseMessage>(new HttpRequestException("No route to host.")),
            "error-500" => Task.FromResult(ScriptedHttpHandler.EmptyResponse(HttpStatusCode.InternalServerError)),
            "error-503" => Task.FromResult(ScriptedHttpHandler.JsonResponse(HttpStatusCode.ServiceUnavailable, AdminLoginCheckTestData.OutcomeJson("invalid_request"))),
            "unparseable" => Task.FromResult(ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, "<html>not json</html>")),
            "array" => Task.FromResult(ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, """[{"allowed":true}]""")),
            "missing-property" => Task.FromResult(ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, """{"permitted":true}""")),
            "wrong-type" => Task.FromResult(ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, """{"allowed":"yes"}""")),
            "setup-redirect" => Task.FromResult(Redirect()),
            "bare-404" => Task.FromResult(ScriptedHttpHandler.EmptyResponse(HttpStatusCode.NotFound)),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
        };
    }

    private static HttpResponseMessage Redirect()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Redirect);
        response.Headers.Location = new Uri("/setup", UriKind.Relative);
        return response;
    }
}
