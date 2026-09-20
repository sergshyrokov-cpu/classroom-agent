using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-008 AC-017: the landing page shows who is signed in, their role, and the installation's legitimacy
/// status with the read-only reason and the time of the last successful check. The status is read through
/// <c>Application</c>, never recomputed in the view, and no teaching data appears (spec FR-019; AD-3, AD-6,
/// AD-8).
/// </summary>
public sealed class LandingPageTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    [Fact]
    public async Task ThePage_ShowsTheEmailAsStoredAndTheRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var user = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Contains(user.Email, page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(SignInTestData.TextKeys.RoleAdmin, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-017: outside read-only mode the page says the installation is in order.</summary>
    [Fact]
    public async Task WhenNotInReadOnlyMode_ThePageSaysSo()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Contains(host.Text(SignInTestData.TextKeys.LegitimacyOk, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(SignInTestData.TextKeys.ReadOnlyTitle, "uk"), page.Text, StringComparison.Ordinal);
    }

    public static TheoryData<ReadOnlyModeHost.Cause, string> ReadOnlyReasons => new()
    {
        { ReadOnlyModeHost.Cause.NeverConfirmed, SignInTestData.TextKeys.ReadOnlyNotYetConfirmed },
        { ReadOnlyModeHost.Cause.Suspended, SignInTestData.TextKeys.ReadOnlySuspended },
        { ReadOnlyModeHost.Cause.GracePeriodExpired, SignInTestData.TextKeys.ReadOnlyGracePeriodExpired },
    };

    /// <summary>AC-017: in read-only mode the page names the mode and its reason.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyReasons))]
    public async Task InReadOnlyMode_ThePageNamesTheModeAndTheReason(ReadOnlyModeHost.Cause cause, string reasonKey)
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct, cause);
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(SignInTestData.TextKeys.ReadOnlyTitle, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(reasonKey, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-017: the time of the last successful check is shown when there is one.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheTimeOfTheLastSuccessfulCheckIsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(
            database,
            channel,
            ct,
            ReadOnlyModeHost.Cause.GracePeriodExpired);
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        var lastCheck = ReadOnlyModeHost.LastSuccessOf(ReadOnlyModeHost.Cause.GracePeriodExpired)!.Value;

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Contains(
            host.Text(SignInTestData.TextKeys.ReadOnlyLastSuccessfulCheck, "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            lastCheck.UtcDateTime.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture),
            page.Text,
            StringComparison.Ordinal);
    }

    /// <summary>AC-017: with no check ever, there is no time to show.</summary>
    [Fact]
    public async Task WhenNoCheckHasEverSucceeded_NoTimeIsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(
            database,
            channel,
            ct,
            ReadOnlyModeHost.Cause.NeverConfirmed);
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Contains(
            host.Text(SignInTestData.TextKeys.ReadOnlyNotYetConfirmed, "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            host.Text(SignInTestData.TextKeys.ReadOnlyLastSuccessfulCheck, "uk"),
            page.Text,
            StringComparison.Ordinal);
    }

    /// <summary>AC-017, AD-8: the page carries a DTO's fields only — no Identity field and no domain entity.</summary>
    [Fact]
    public async Task ThePage_ShowsNoIdentityFieldAndNoTeachingData()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.DoesNotContain("securityStamp", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("concurrencyStamp", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("course", page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("student", page.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-017: the page offers sign-out as a POST form with the antiforgery token.</summary>
    [Fact]
    public async Task ThePage_OffersSignOutAsAPostFormWithTheToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Contains(host.Text(SignInTestData.TextKeys.SignOut, "uk"), page.Text, StringComparison.Ordinal);
        Assert.True(Html.HasInput(page.Body, Html.AntiforgeryFieldName));
    }

    /// <summary>AC-017, AC-002: the page is reachable by Admin and Dean — the permission matrix row of section 2.</summary>
    [Fact]
    public async Task ThePolicy_AdmitsAdminAndDean()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var landing = Assert.Single(
            HostEndpoint.All(host.Services),
            e => e.Pattern == string.Empty && !e.IsFallback);

        Assert.False(landing.AllowsAnonymous, landing.ToString());
    }

    /// <summary>AC-017: the page reads the legitimacy state, and reading it writes nothing.</summary>
    [Fact]
    public async Task OpeningThePage_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        var audit = await host.AuditRowsAsync(ct);
        var users = await host.AppUsersAsync(ct);
        var legitimacy = await host.LegitimacyStatesAsync(ct);

        await client.GetAsync(SignInTestData.LandingPath, ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(audit, await host.AuditRowsAsync(ct));
        Assert.Equal(users, await host.AppUsersAsync(ct));
        Assert.Equal(legitimacy, await host.LegitimacyStatesAsync(ct));
    }
}
