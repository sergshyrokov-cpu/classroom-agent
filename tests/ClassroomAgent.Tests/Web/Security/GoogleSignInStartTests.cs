using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-008 AC-003: the Google sign-in starts on a <c>POST</c> carrying the antiforgery token, asks for identity
/// scopes only, builds the redirect URI from the configured public base address, sets the correlation cookie
/// and carries the school's domain as the account-picker hint when it is known (spec FR-005, FR-006; S-06,
/// S-07, S-12; OD-002).
/// </summary>
public sealed class GoogleSignInStartTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Post_WithToken_RedirectsToGoogle_AndSetsTheCorrelationCookie()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.StartGoogleSignInAsync(ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.NotNull(response.Location);
        Assert.NotEmpty(InstallationSignInExtensions.StateOf(response));
        Assert.Contains(
            InstallationSignInExtensions.SetCookieNames(response),
            name => name.Contains("Correlation", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>AC-003: the authorization endpoint is Google's own, not something a school configured.</summary>
    [Fact]
    public async Task TheAuthorizationRequest_GoesToGoogle()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.StartGoogleSignInAsync(ct);

        Assert.NotNull(response.Location);
        Assert.Equal("accounts.google.com", new Uri(response.Location!).Host);
    }

    /// <summary>AC-003, S-06: identity scopes only. A Classroom or Reports scope here is a Critical finding.</summary>
    [Fact]
    public async Task TheAuthorizationRequest_AsksForIdentityScopesOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.StartGoogleSignInAsync(ct);

        var scope = InstallationSignInExtensions.QueryOf(response)["scope"];
        Assert.Equal(
            new[] { "email", "openid", "profile" },
            scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
        Assert.DoesNotContain("classroom", response.Location!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("admin.reports", response.Location!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("admin.directory", response.Location!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("drive", response.Location!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-001, AC-003: the redirect URI is built from the configured public base address (FR-001, v78).</summary>
    [Fact]
    public async Task TheRedirectUri_ComesFromTheConfiguredPublicBaseAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.StartGoogleSignInAsync(ct);

        Assert.Equal(
            InstallationConfigurationKeys.PublicBaseAddressValue + SignInTestData.CallbackPath,
            InstallationSignInExtensions.QueryOf(response)["redirect_uri"]);
    }

    /// <summary>AC-003, v78: the incoming host never decides the redirect URI, even when forwarded headers claim it.</summary>
    [Fact]
    public async Task TheRedirectUri_IgnoresTheRequestHostAndForwardedHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        var headers = new Dictionary<string, string>
        {
            ["X-Forwarded-Host"] = "attacker.example.test",
            ["X-Forwarded-Proto"] = "https",
        };

        await client.GetAsync(SignInTestData.SignInPath, ct, headers);
        var response = await client.PostFormAsync(SignInTestData.StartPath, [], ct, withToken: true, headers: headers);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.DoesNotContain("attacker.example.test", response.Location!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            InstallationConfigurationKeys.PublicBaseAddressValue + SignInTestData.CallbackPath,
            InstallationSignInExtensions.QueryOf(response)["redirect_uri"]);
    }

    /// <summary>AC-003, OD-002: the hint carries the school's domain once a legitimacy check has succeeded.</summary>
    [Fact]
    public async Task WhenTheDomainIsKnown_TheAccountPickerHintCarriesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        host.Start();
        using var client = host.CreateClient();

        var response = await client.StartGoogleSignInAsync(ct);

        Assert.Equal(InstallationTestData.Domain, InstallationSignInExtensions.QueryOf(response)["hd"]);
    }

    /// <summary>AC-003, OD-002: with no successful check ever, no hint is sent — a fresh school must still sign in.</summary>
    [Fact]
    public async Task WhenNoCheckHasEverSucceeded_NoAccountPickerHintIsSent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.NeverConfirmed, ct);
        using var client = host.CreateClient();

        var response = await client.StartGoogleSignInAsync(ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.DoesNotContain("hd", InstallationSignInExtensions.QueryOf(response).Keys);
    }

    /// <summary>AC-003: a suspended school still knows its domain, so the hint is still sent.</summary>
    [Fact]
    public async Task WhenSuspended_TheHintIsStillSent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.Suspended, ct);
        using var client = host.CreateClient();

        var response = await client.StartGoogleSignInAsync(ct);

        Assert.Equal(InstallationTestData.Domain, InstallationSignInExtensions.QueryOf(response)["hd"]);
    }

    /// <summary>AC-003, trebovaniya.md section 8: a GET does not start a sign-in.</summary>
    [Fact]
    public async Task Get_DoesNotStartASignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync(SignInTestData.StartPath, ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.Status);
        Assert.Empty(response.SetCookies);
    }

    /// <summary>AC-003, S-12: the anonymous start form is not exempt from antiforgery — this is login CSRF protection.</summary>
    [Fact]
    public async Task PostWithoutTheAntiforgeryToken_StartsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.StartGoogleSignInAsync(ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text(SignInTestData.TextKeys.PageExpired, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Null(response.Location);
    }

    /// <summary>AC-003, AC-018, SC-10: the authorization request's parameters never reach the log.</summary>
    [Fact]
    public async Task TheStart_LogsNoStateAndNoClientId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        var response = await client.StartGoogleSignInAsync(ct);
        var state = InstallationSignInExtensions.StateOf(response);

        var files = await host.ReadLogFilesAsync(ct);

        var log = string.Join("\n", files);
        Assert.DoesNotContain(state, log, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallationConfigurationKeys.OAuthClientIdValue, log, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-003: the sign-in page itself reads and writes nothing.</summary>
    [Fact]
    public async Task TheSignInPage_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(Html.HasInput(response.Body, Html.AntiforgeryFieldName));
        Assert.Empty(await host.AppUsersAsync(ct));
        Assert.Empty(await host.AuditRowsAsync(ct));
    }
}
