using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-008 AC-003: the callback is the one <c>GET</c> allowed to change state, and it proves the OAuth
/// <c>state</c> parameter and the correlation cookie before anything is created, asked or written; the email
/// must be present and verified, and is lower-cased before use (spec FR-007, VR-005, VR-007, I-6; S-10;
/// BR-079).
/// </summary>
public sealed class GoogleSignInCallbackTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    [Fact]
    public async Task ValidCallback_SignsIn_AndIssuesTheSessionCookie()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        Assert.Equal(SignInTestData.LandingPath, callback.LocationPath);
        Assert.NotNull(callback.SetCookie(SignInTestData.SessionCookieName));
    }

    /// <summary>AC-003, VR-007: no <c>state</c> at all refuses before anything happens.</summary>
    [Fact]
    public async Task CallbackWithoutState_IsRefused_AndNothingHappens()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        await client.StartGoogleSignInAsync(ct);

        var callback = await client.CompleteGoogleCallbackAsync(state: null, ct);

        AssertRefused(callback);
        Assert.Empty(channel.AdminLoginCheckRequests);
        Assert.Empty(await host.AppUsersAsync(ct));
    }

    /// <summary>AC-003, VR-007: a forged <c>state</c> refuses.</summary>
    [Fact]
    public async Task CallbackWithAnUnknownState_IsRefused_AndNothingHappens()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        await client.StartGoogleSignInAsync(ct);

        var callback = await client.CompleteGoogleCallbackAsync("a-state-nobody-issued", ct);

        AssertRefused(callback);
        Assert.Empty(channel.AdminLoginCheckRequests);
        Assert.Empty(await host.AppUsersAsync(ct));
    }

    /// <summary>AC-003, VR-007: without the correlation cookie the callback refuses, even with a genuine state.</summary>
    [Fact]
    public async Task CallbackWithoutTheCorrelationCookie_IsRefused_AndNothingHappens()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        var start = await client.StartGoogleSignInAsync(ct);
        var state = InstallationSignInExtensions.StateOf(start);

        // A second client holds no correlation cookie, so the genuine state alone must not be enough.
        using var withoutCookie = host.CreateClient();
        var callback = await withoutCookie.CompleteGoogleCallbackAsync(state, ct);

        AssertRefused(callback);
        Assert.Empty(channel.AdminLoginCheckRequests);
        Assert.Empty(await host.AppUsersAsync(ct));
    }

    /// <summary>AC-003, VR-007: a state may be used once; replaying it refuses.</summary>
    [Fact]
    public async Task ReplayingAState_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        var start = await client.StartGoogleSignInAsync(ct);
        var state = InstallationSignInExtensions.StateOf(start);

        var first = await client.CompleteGoogleCallbackAsync(state, ct);
        var replay = await client.CompleteGoogleCallbackAsync(state, ct);

        Assert.Equal(SignInTestData.LandingPath, first.LocationPath);
        AssertRefused(replay);
        Assert.Single(await host.AppUsersAsync(ct));
    }

    /// <summary>AC-003, VR-005, I-6: an unverified email is a failed callback, not an AllowedAdmin refusal.</summary>
    [Fact]
    public async Task UnverifiedEmail_IsRefusedAsAFailedCallback_WithoutAskingTheControlPlane()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.Google.EmailVerified = false;

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        AssertRefused(callback);
        Assert.Empty(channel.AdminLoginCheckRequests);
        Assert.Empty(await host.AppUsersAsync(ct));
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.CallbackFailed, audit.RefusalCategory);
    }

    /// <summary>AC-003, VR-005: no email at all is the same failed callback.</summary>
    [Fact]
    public async Task NoEmailAtAll_IsRefusedAsAFailedCallback()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.Google.IncludeEmail = false;

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        AssertRefused(callback);
        Assert.Empty(channel.AdminLoginCheckRequests);
        Assert.Empty(await host.AppUsersAsync(ct));
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.CallbackFailed, audit.RefusalCategory);
    }

    /// <summary>AC-003, BR-079: a mixed-case address is lower-cased before the call, the lookup and the insert.</summary>
    [Fact]
    public async Task MixedCaseEmail_IsLowerCasedBeforeEverything()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.Google.Email = SignInTestData.AdminEmailMixedCase;

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(SignInTestData.LandingPath, callback.LocationPath);
        var request = Assert.Single(channel.AdminLoginCheckRequests);
        Assert.Contains(SignInTestData.AdminEmail, request.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(SignInTestData.AdminEmailMixedCase, request.Body, StringComparison.Ordinal);
        var user = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal(SignInTestData.AdminEmail, user.Email);
        Assert.Equal(SignInTestData.AdminEmail, user.NormalizedEmail);
    }

    /// <summary>AC-003, S-09: no Google token and no subject identifier is stored anywhere.</summary>
    [Fact]
    public async Task NoGoogleTokenAndNoSubjectIdentifier_IsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);

        var columns = await host.QueryAsync(
            """
            SELECT table_name || '.' || column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
            """,
            r => r.GetString(0),
            ct);
        Assert.DoesNotContain(columns, c => c.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("subject", StringComparison.OrdinalIgnoreCase));
        var rows = await host.AuditRowsAsJsonAsync(ct);
        Assert.All(rows, row => Assert.DoesNotContain(host.Google.Subject, row, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC-003, SC-4: the callback is a GET and therefore carries no antiforgery token (v64). It is served by the
    /// authentication handler rather than by a routed endpoint, so this is asserted as behaviour: a POST to the
    /// path is not treated as a callback, and the GET needs no token.
    /// </summary>
    [Fact]
    public async Task TheCallback_NeedsNoAntiforgeryToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        var start = await client.StartGoogleSignInAsync(ct);

        // No token is sent or needed, and the sign-in completes.
        var callback = await client.CompleteGoogleCallbackAsync(InstallationSignInExtensions.StateOf(start), ct);

        Assert.Equal(SignInTestData.LandingPath, callback.LocationPath);
        Assert.DoesNotContain(
            HostEndpoint.All(host.Services),
            e => e.Pattern == "signin-google" && e.UnsafeMethodsAccepted.Count > 0);
    }

    /// <summary>AC-003, AC-018, SC-10: the code and the state never reach the log.</summary>
    [Fact]
    public async Task TheCallback_LogsNeitherTheCodeNorTheState()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        var start = await client.StartGoogleSignInAsync(ct);
        var state = InstallationSignInExtensions.StateOf(start);
        await client.CompleteGoogleCallbackAsync(state, ct);

        var files = await host.ReadLogFilesAsync(ct);

        var log = string.Join("\n", files);
        Assert.DoesNotContain(InstallationSignInExtensions.AuthorizationCode, log, StringComparison.Ordinal);
        Assert.DoesNotContain(state, log, StringComparison.Ordinal);
        Assert.DoesNotContain(SignInTestData.AdminEmail, log, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC-003, SC-4 (security review F-3): the authentication handler claims its callback path for any method and
    /// reads the code and state from the query, so a POST is refused before it can reach the one writing path of
    /// the sign-in. The callback stays the only GET that writes (trebovaniya.md section 8, v64).
    /// </summary>
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task TheCallback_RefusesAnyMethodOtherThanGet_AndWritesNothing(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.UseGoogleStub();
        using var client = host.CreateClient();
        var start = await client.StartGoogleSignInAsync(ct);
        var state = InstallationSignInExtensions.StateOf(start);

        var response = await client.SendAsync(
            new HttpMethod(method),
            SignInTestData.CallbackPath + "?code=" + InstallationSignInExtensions.AuthorizationCode
                + "&state=" + Uri.EscapeDataString(state),
            content: null,
            ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.Status);
        Assert.Null(response.SetCookie(SignInTestData.SessionCookieName));
        Assert.Empty(channel.AdminLoginCheckRequests);
        Assert.Empty(await host.AppUsersAsync(ct));
        Assert.Empty(await host.AuditRowsAsync(ct));
    }

    /// <summary>A refusal goes back to the sign-in page with no session and no message in the address (api-design 2.2).</summary>
    private static void AssertRefused(PageResponse callback)
    {
        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        Assert.Equal(SignInTestData.SignInPath, callback.LocationPath);
        Assert.Null(callback.SetCookie(SignInTestData.SessionCookieName));
    }
}
