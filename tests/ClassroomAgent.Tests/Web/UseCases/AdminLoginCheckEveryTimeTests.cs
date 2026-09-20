using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-008 AC-004: the installation asks the Control Plane on <em>every</em> Admin sign-in, with the
/// installation id and the lower-cased email in the body of a POST, and keeps no copy and no cache of the
/// answer (spec FR-008, FR-010; S-02; SC-3, BR-012; TC-5). A first-login-only check fails these tests.
/// </summary>
public sealed class AdminLoginCheckEveryTimeTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.Json(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    /// <summary>AC-004, S-02: the second sign-in of a known Admin calls again.</summary>
    [Fact]
    public async Task ASecondSignIn_AsksTheControlPlaneAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        var (_, first) = await host.SignInWithGoogleAsync(ct);
        var (_, second) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(SignInTestData.LandingPath, first.LocationPath);
        Assert.Equal(SignInTestData.LandingPath, second.LocationPath);
        Assert.Equal(2, channel.Requests.Count);
    }

    /// <summary>AC-004, S-02: a revocation between two sign-ins takes effect at once — nothing is remembered.</summary>
    [Fact]
    public async Task AfterASuccessfulSignIn_ARevocationRefusesTheNextOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var answers = new Queue<bool>([true, false]);
        var channel = new ScriptedHttpHandler((_, _) => Task.FromResult(
            ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(answers.Dequeue()))));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        var (_, allowed) = await host.SignInWithGoogleAsync(ct);
        var (_, revoked) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(SignInTestData.LandingPath, allowed.LocationPath);
        Assert.Equal(SignInTestData.SignInPath, revoked.LocationPath);
        Assert.Null(revoked.SetCookie(SignInTestData.SessionCookieName));
        Assert.Equal(2, channel.Requests.Count);
    }

    /// <summary>AC-004: the installation id and the email travel in the body of a POST, never in the address.</summary>
    [Fact]
    public async Task TheQuestion_IsAPostCarryingTheInstallationIdAndEmailInTheBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);

        var request = Assert.Single(channel.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            InstallationConfigurationKeys.ControlPlaneAddressValue + AdminLoginCheckTestData.Path,
            request.Uri!.GetLeftPart(UriPartial.Path));
        Assert.Empty(request.Uri!.Query);
        Assert.StartsWith("application/json", request.ContentType, StringComparison.OrdinalIgnoreCase);

        using var body = JsonDocument.Parse(request.Body);
        var properties = body.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
        Assert.Equal(new[] { "email", "installationId" }, properties.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(host.InstallationId, Guid.Parse(properties["installationId"].GetString()!));
        Assert.Equal(SignInTestData.AdminEmail, properties["email"].GetString());
    }

    /// <summary>AC-004, S-02: the channel carries no cookie and no authorization header — it is network-isolated.</summary>
    [Fact]
    public async Task TheQuestion_CarriesNoCookieAndNoAuthorizationHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);

        var request = Assert.Single(channel.Requests);
        Assert.False(request.Headers.ContainsKey("Cookie"));
        Assert.False(request.Headers.ContainsKey("Authorization"));
    }

    /// <summary>AC-004, S-02: no table in the installation holds a copy of AllowedAdmin.</summary>
    [Fact]
    public async Task NoInstallationTable_HoldsACopyOfAllowedAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);

        var tables = await host.TableNamesAsync(ct);
        Assert.Equal(
            new[] { "__EFMigrationsHistory", "app_user", "audit_event", "legitimacy_state" },
            tables.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(tables, t => t.Contains("allowed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tables, t => t.Contains("admin", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>AC-004: a still-valid session does not skip the check on the next fresh sign-in.</summary>
    [Fact]
    public async Task AnExistingSession_DoesNotSkipTheCheckForANewSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (signedIn, _) = await host.SignInWithGoogleAsync(ct);

        var landing = await signedIn.GetAsync(SignInTestData.LandingPath, ct);
        var (_, again) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(HttpStatusCode.OK, landing.Status);
        Assert.Equal(SignInTestData.LandingPath, again.LocationPath);

        // Browsing with a session asks nobody; a new sign-in always does.
        Assert.Equal(2, channel.Requests.Count);
    }

    /// <summary>AC-004, S-02: a restart cannot resurrect an earlier answer — the row exists, the question is asked again.</summary>
    [Fact]
    public async Task AfterARestart_TheQuestionIsAskedAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var firstChannel = Allowed();
        await using var first = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, firstChannel, ct);
        await first.SignInWithGoogleAsync(ct);
        await first.StopAsync();

        var secondChannel = ScriptedHttpHandler.Json(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(false));
        await using var restarted = InstallationTestHost.Restart(first, InstallationTestHost.DefaultStart);
        restarted.ControlPlaneHandler = secondChannel;
        restarted.Start();

        var (_, refused) = await restarted.SignInWithGoogleAsync(ct);

        Assert.Equal(SignInTestData.SignInPath, refused.LocationPath);
        Assert.Single(secondChannel.Requests);
        Assert.Single(await restarted.AppUsersAsync(ct));
    }
}
