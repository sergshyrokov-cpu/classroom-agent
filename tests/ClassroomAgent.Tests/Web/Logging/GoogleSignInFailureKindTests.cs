using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-077 AC-007, spec FR-006, VR-003, S-04, S-07, I-5, I-6, I-10: a failed Google callback writes one application
/// Warning naming the kind from the closed list and the request id, and nothing of the failure itself. The first group
/// drives the real authentication handler, so a framework message that changes is caught; the second calls the event
/// directly for every row and boundary of the FR-006 table.
/// </summary>
public sealed class GoogleSignInFailureKindTests(PostgreSqlFixture database)
{
    private const string Category = "ClassroomAgent.Web.Security.GoogleSignInEvents";
    private const string CallbackFailedEvent = "AdminSignInCallbackFailed";
    private const string Marker = "SYNTHETIC-FAILURE-DETAIL-77a1";

    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    private Task<InstallationTestHost> StartAsync(CancellationToken ct) =>
        InstallationTestHost.StartWithControlPlaneHttpAsync(database, Allowed(), ct);

    // ---- Through the real handler ------------------------------------------------------------------------------------

    /// <summary>AC-007, FR-006 row 1: a genuine state without the correlation cookie.</summary>
    [Fact]
    public async Task AMissingCorrelationCookie_IsLoggedAsCorrelationFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        var state = InstallationSignInExtensions.StateOf(await client.StartGoogleSignInAsync(ct));
        using var withoutCookie = host.CreateClient();

        var callback = await withoutCookie.CompleteGoogleCallbackAsync(state, ct);

        await AssertOneLineAsync(host, callback, "CorrelationFailed", ct, state);
    }

    /// <summary>AC-007, FR-006 row 1, I-6: a state nobody issued.</summary>
    [Fact]
    public async Task AnUnknownState_IsLoggedAsCorrelationFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        await client.StartGoogleSignInAsync(ct);

        var callback = await client.CompleteGoogleCallbackAsync(Marker, ct);

        await AssertOneLineAsync(host, callback, "CorrelationFailed", ct);
    }

    /// <summary>AC-007, FR-006 row 2: Google's token endpoint answers with a failure; rule 2 precedes rule 3.</summary>
    [Fact]
    public async Task ATokenEndpointFailure_IsLoggedAsTokenExchangeFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        host.Google.TokenAnswer = () => ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.BadRequest,
            $$"""{"error":"invalid_grant","error_description":"{{Marker}}"}""");

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        await AssertOneLineAsync(host, callback, "TokenExchangeFailed", ct);
    }

    /// <summary>AC-007, FR-006 row 3: the user refuses consent.</summary>
    [Fact]
    public async Task ARefusedConsent_IsLoggedAsGoogleError()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        var state = InstallationSignInExtensions.StateOf(await client.StartGoogleSignInAsync(ct));

        var callback = await client.GetAsync(
            $"{SignInTestData.CallbackPath}?error=access_denied&error_description={Marker}&state={Uri.EscapeDataString(state)}",
            ct);

        await AssertOneLineAsync(host, callback, "GoogleError", ct, state);
    }

    /// <summary>AC-007, FR-006 row 3: Google returns an error of its own.</summary>
    [Fact]
    public async Task AnErrorFromGoogle_IsLoggedAsGoogleError()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        var state = InstallationSignInExtensions.StateOf(await client.StartGoogleSignInAsync(ct));

        var callback = await client.GetAsync(
            $"{SignInTestData.CallbackPath}?error=server_error&error_description={Marker}&state={Uri.EscapeDataString(state)}",
            ct);

        await AssertOneLineAsync(host, callback, "GoogleError", ct, state);
    }

    /// <summary>AC-007, FR-006 row 4, I-10: a network failure during the exchange cannot be told apart and is Other.</summary>
    [Fact]
    public async Task ANetworkFailureDuringTheExchange_IsLoggedAsOther()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        host.Google.TokenAnswer = () => throw new HttpRequestException(Marker);

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        await AssertOneLineAsync(host, callback, "Other", ct);
    }

    /// <summary>AC-007, FR-006 row 4, I-10: an error answer whose body is not JSON is Other.</summary>
    [Fact]
    public async Task AnUnreadableErrorAnswer_IsLoggedAsOther()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        host.Google.TokenAnswer = () => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>" + Marker + "</html>") };

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        await AssertOneLineAsync(host, callback, "Other", ct);
    }

    /// <summary>AC-007, FR-006 row 4, I-10: a failed reading of Google's user information is Other.</summary>
    [Fact]
    public async Task AFailedUserInformationRead_IsLoggedAsOther()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        host.Google.UserInformationAnswer = () => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.InternalServerError);

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        await AssertOneLineAsync(host, callback, "Other", ct);
    }

    /// <summary>FR-006: the same event written by <c>OnTicketReceivedAsync</c> is unchanged and carries no kind.</summary>
    [Fact]
    public async Task ARefusedIdentity_CarriesNoKind()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        host.Google.EmailVerified = false;

        await host.SignInWithGoogleAsync(ct);
        var events = await host.ReadLogEventsAsync(ct);

        var line = Assert.Single(events, e => e.EventName == CallbackFailedEvent);
        Assert.Null(line.Property("FailureKind"));
    }

    // ---- The FR-006 table, row by row --------------------------------------------------------------------------------

    public static TheoryData<string, bool, string> Failures => new()
    {
        { "Correlation failed.", false, "CorrelationFailed" },
        { "Correlation failed.", true, "CorrelationFailed" },
        { "The oauth state was missing or invalid.", false, "CorrelationFailed" },
        { "OAuth token endpoint failure", false, "TokenExchangeFailed" },
        { "OAuth token endpoint failure: Status: BadRequest;Body: " + Marker, true, "TokenExchangeFailed" },
        { "Failed to retrieve access token.", false, "TokenExchangeFailed" },
        { "Failed to retrieve access token.", true, "TokenExchangeFailed" },
        { "Access was denied by the resource owner or by the remote server.", false, "GoogleError" },
        { Marker, true, "GoogleError" },
        { Marker, false, "Other" },
        { "correlation failed.", false, "Other" },
        { "Correlation failed. " + Marker, false, "Other" },
        { " OAuth token endpoint failure", false, "Other" },
        { "Failed to retrieve access token", false, "Other" },
        { "Access was denied", false, "Other" },
        { "", false, "Other" },
    };

    /// <summary>VR-003, FR-006, S-04: each row in its order, compared exactly, and the failure's message never written.</summary>
    [Theory]
    [MemberData(nameof(Failures))]
    public async Task AFailure_IsClassifiedByTheTable_AndItsMessageIsNotWritten(string message, bool dataError, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var failure = new AuthenticationFailureException(message);
        if (dataError)
        {
            failure.Data["error"] = Marker;
        }

        await RaiseFailureAsync(host, failure, ct);

        var line = await SingleLineAsync(host, expected, ct);
        if (message.Length > 0)
        {
            Assert.DoesNotContain(message, line.Line, StringComparison.Ordinal);
        }
    }

    /// <summary>FR-006 row 4: no failure object at all.</summary>
    [Fact]
    public async Task NoFailureObject_IsOther()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);

        await RaiseFailureAsync(host, null, ct);

        await SingleLineAsync(host, "Other", ct);
    }

    /// <summary>FR-006, I-10: a failure of another exception type is Other, whatever its message says.</summary>
    [Fact]
    public async Task AnotherExceptionType_CarryingNoErrorData_IsOther()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);

        await RaiseFailureAsync(host, new HttpRequestException(Marker), ct);

        var line = await SingleLineAsync(host, "Other", ct);
        Assert.DoesNotContain(Marker, line.Line, StringComparison.Ordinal);
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------

    private static async Task RaiseFailureAsync(InstallationTestHost host, Exception? failure, CancellationToken ct)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider, TraceIdentifier = "us077-request" };
        var context = new RemoteFailureContext(
            httpContext,
            new AuthenticationScheme("Google", "Google", typeof(CookieAuthenticationHandler)),
            new RemoteAuthenticationOptions(),
            failure!);

        await GoogleSignInEvents.OnRemoteFailureAsync(context).WaitAsync(ct);
    }

    private static async Task<LogEvent> SingleLineAsync(InstallationTestHost host, string kind, CancellationToken ct)
    {
        var events = await host.ReadLogEventsAsync(ct);

        // S-04: "one Warning" counts lines of the application's category; the framework's own correlation lines stay.
        var line = Assert.Single(events, e => e.Level == "Warning" && e.Property("SourceContext") == Category);
        Assert.Equal(CallbackFailedEvent, line.EventName);
        Assert.Equal(kind, line.Property("FailureKind"));
        Assert.False(string.IsNullOrWhiteSpace(line.Property("RequestId")), "The line carries no request id.");
        return line;
    }

    /// <summary>The one line, nothing of the failure anywhere in the log, and the user's view and audit row as before (AC-007, S-07).</summary>
    private static async Task AssertOneLineAsync(
        InstallationTestHost host,
        PageResponse callback,
        string kind,
        CancellationToken ct,
        string? state = null)
    {
        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        Assert.Equal(SignInTestData.SignInPath, callback.LocationPath);
        Assert.Null(callback.SetCookie(SignInTestData.SessionCookieName));

        var line = await SingleLineAsync(host, kind, ct);

        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.CallbackFailed, audit.RefusalCategory);
        Assert.Equal(audit.RequestId, line.Property("RequestId"));

        var log = string.Join('\n', await host.ReadLogFilesAsync(ct));
        Assert.DoesNotContain(Marker, log, StringComparison.Ordinal);
        Assert.DoesNotContain(SignInTestData.AdminEmail, log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InstallationSignInExtensions.AuthorizationCode, log, StringComparison.Ordinal);
        if (state is not null)
        {
            Assert.DoesNotContain(state, log, StringComparison.Ordinal);
        }
    }
}
