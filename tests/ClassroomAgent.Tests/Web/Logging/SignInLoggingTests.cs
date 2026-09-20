using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-008 AC-018: every line this Story writes carries identifiers, categories, outcomes and states only, at
/// the level FR-020 fixes, and a line written inside a request carries the request id that ties it to its audit
/// row (spec FR-020; S-15; SC-10, SC-11, DC-10).
/// </summary>
public sealed class SignInLoggingTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.Json(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    /// <summary>FR-020: a successful sign-in is Information, with the account id.</summary>
    [Fact]
    public async Task ASuccessfulSignIn_IsLoggedAtInformationWithTheAccountId()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.SignInWithGoogleAsync(ct);
        var user = Assert.Single(await host.AppUsersAsync(ct));

        var events = await host.ReadLogEventsAsync(ct);

        var identifier = user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(
            events,
            e => e.Level == "Information" && e.Line.Contains(identifier, StringComparison.Ordinal));
    }

    /// <summary>FR-020: a refusal because the email is not approved is a Warning.</summary>
    [Fact]
    public async Task ANotApprovedRefusal_IsLoggedAtWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = ScriptedHttpHandler.Json(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(false));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);
        var events = await host.ReadLogEventsAsync(ct);

        Assert.Contains(events, e => e.Level == "Warning");
        Assert.DoesNotContain(events, e => e.Level == "Error");
    }

    /// <summary>FR-020: an unreachable Control Plane is an Error.</summary>
    [Fact]
    public async Task AnUnavailableControlPlane_IsLoggedAtError()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = new ScriptedHttpHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("No route to host.")));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);
        var events = await host.ReadLogEventsAsync(ct);

        Assert.Contains(events, e => e.Level == "Error");
    }

    /// <summary>FR-020: a failed callback is a Warning, recorded as a category.</summary>
    [Fact]
    public async Task AFailedCallback_IsLoggedAtWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.Google.EmailVerified = false;

        await host.SignInWithGoogleAsync(ct);
        var events = await host.ReadLogEventsAsync(ct);

        Assert.Contains(events, e => e.Level == "Warning");
    }

    /// <summary>AC-018, SC-11: the audit row's request id appears in the log, so the two can be tied together.</summary>
    [Fact]
    public async Task EveryAuditRowsRequestId_AppearsInTheLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var answers = new Queue<bool>([true, false]);
        var channel = new ScriptedHttpHandler((_, _) => Task.FromResult(
            ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(answers.Dequeue()))));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.SignInWithGoogleAsync(ct);
        await host.SignInWithGoogleAsync(ct);
        var rows = await host.AuditRowsAsync(ct);

        var files = await host.ReadLogFilesAsync(ct);

        var log = string.Join("\n", files);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.RequestId), $"Row {row.Id} carries no request id.");
            Assert.Contains(row.RequestId!, log, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// AC-018, S-15: across the whole flow — success, refusal and an unusable answer — the log carries no email,
    /// code, state, cookie, client id, secret reference or response body.
    /// </summary>
    [Fact]
    public async Task AcrossTheWholeFlow_TheLogCarriesNoSecretAndNoPersonalDatum()
    {
        var ct = TestContext.Current.CancellationToken;
        const string answerMarker = "canary-response-body-of-the-control-plane";
        var answers = new Queue<string>(["allowed", "refused", "error"]);
        var channel = new ScriptedHttpHandler((_, _) => Task.FromResult(answers.Dequeue() switch
        {
            "allowed" => ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true)),
            "refused" => ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(false)),
            _ => ScriptedHttpHandler.JsonResponse(
                HttpStatusCode.InternalServerError,
                $"{{\"detail\":\"{answerMarker}\"}}"),
        }));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        var (client, _) = await host.SignInWithGoogleAsync(ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);
        await host.SignInWithGoogleAsync(ct);
        await host.SignInWithGoogleAsync(ct);

        var files = await host.ReadLogFilesAsync(ct);

        var log = string.Join("\n", files);
        Assert.DoesNotContain(SignInTestData.AdminEmail, log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("petrenko", log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InstallationSignInExtensions.AuthorizationCode, log, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallationConfigurationKeys.OAuthClientIdValue, log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InstallationConfigurationKeys.OAuthClientSecretReferenceValue, log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(answerMarker, log, StringComparison.Ordinal);
        Assert.DoesNotContain(SignInTestData.SessionCookieName, log, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Google.Subject, log, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-access-token", log, StringComparison.Ordinal);
    }

    /// <summary>AC-018: the installation's own log never carries a Google profile name either.</summary>
    [Fact]
    public async Task TheLog_CarriesNoGoogleProfileName()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);
        var files = await host.ReadLogFilesAsync(ct);

        Assert.DoesNotContain(host.Google.DisplayName, string.Join("\n", files), StringComparison.Ordinal);
    }
}
