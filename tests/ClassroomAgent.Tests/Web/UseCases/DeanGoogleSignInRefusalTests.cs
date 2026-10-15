using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-051: the Google sign-in admits only an Admin account. A Dean account whose email the Control Plane approves is
/// refused with its own message and audit category and is left untouched; the order of checks is AllowedAdmin, then
/// the role, then the disabled state (spec FR-001…FR-004, FR-006, FR-007; trebovaniya.md §2 v96, SC-3, SC-11).
/// Driven over HTTP through the real callback, with the Control Plane scripted and Google stubbed (TC-4).
/// </summary>
public sealed class DeanGoogleSignInRefusalTests(PostgreSqlFixture database)
{
    private const string Dean = DeanAccountTestData.DeanEmail;

    /// <summary>The log event names of the refusals that existed before US-051 (US-008 FR-020).</summary>
    private static readonly string[] PreviousRefusalEvents =
    [
        "AdminSignInNotApproved",
        "AdminSignInCouldNotConfirm",
        "AdminSignInCallbackFailed",
        "AdminSignInAccountDisabled",
    ];

    public static TheoryData<ReadOnlyModeHost.Cause> ReadOnlyCauses => ReadOnlyModeHost.ReadOnlyCauses;

    private static ScriptedHttpHandler Approving() =>
        ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    /// <summary>AC-001: no session, back to the sign-in page, which shows the "Dean account" text.</summary>
    [Fact]
    public async Task AnApprovedDeanAccount_IsRefused_AndSeesTheDeanAccountMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, Approving(), ct);
        await host.InsertDeanAsync(ct);
        host.Google.Email = Dean;

        var (client, callback) = await host.SignInWithGoogleAsync(ct);
        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        Assert.Equal(SignInTestData.SignInPath, callback.LocationPath);
        Assert.Null(callback.SetCookie(SignInTestData.SessionCookieName));
        Assert.Contains(
            host.Text(SignInTestData.TextKeys.RefusedNotAdminAccount, "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(Dean, page.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC-001, FR-002: the Dean account is not touched — every column, the security and concurrency stamps, the
    /// sign-in time, the counter, the lockout, the role and the disabled flag included. No Admin account appears.
    /// </summary>
    [Fact]
    public async Task TheRefusal_LeavesTheDeanAccountExactlyAsItWas()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, Approving(), ct);
        await host.InsertDeanAsync(ct, lastSuccessfulSignInAt: InstallationTestHost.DefaultStart - TimeSpan.FromDays(2));
        host.Google.Email = Dean;
        var before = await AccountsAsJsonAsync(host, ct);
        host.Time.Advance(TimeSpan.FromHours(3));

        await host.SignInWithGoogleAsync(ct);

        Assert.Equal(before, await AccountsAsJsonAsync(host, ct));
        var account = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal("dean", account.Role);
    }

    /// <summary>AC-002, FR-003: exactly one row — Admin sign-in, refused, the Dean as actor, the new category, no email.</summary>
    [Fact]
    public async Task TheRefusal_IsAuditedAgainstTheDeanAccount_WithTheNewCategory()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, Approving(), ct);
        await host.InsertDeanAsync(ct);
        host.Google.Email = Dean;
        var dean = Assert.Single(await host.AppUsersAsync(ct));

        await host.SignInWithGoogleAsync(ct);

        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("admin_sign_in", audit.Action);
        Assert.Equal("refused", audit.Outcome);
        Assert.Equal("app_user", audit.ActorType);
        Assert.Equal(dean.Id, audit.ActorId);
        Assert.Equal("dean", audit.ActorRole);
        Assert.Equal(SignInTestData.RefusalCategories.NotAnAdminAccount, audit.RefusalCategory);
        Assert.NotNull(audit.RequestId);
        var json = Assert.Single(await SignInRowsAsJsonAsync(host, ct));
        Assert.DoesNotContain(Dean, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", json, StringComparison.Ordinal);
    }

    /// <summary>AC-003, FR-004: "not approved" comes first and keeps its category whatever the account's role.</summary>
    [Fact]
    public async Task ADeanAccountNotInAllowedAdmin_KeepsTheNotApprovedRefusal()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(false));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.InsertDeanAsync(ct);
        host.Google.Email = Dean;

        var (client, _) = await host.SignInWithGoogleAsync(ct);
        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.NotInAllowedAdmin, audit.RefusalCategory);
        Assert.Contains(host.Text(SignInTestData.TextKeys.RefusedNotApproved, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-003, FR-004: an unknown installation keeps its category whatever the account's role.</summary>
    [Fact]
    public async Task ADeanAccount_WhenTheInstallationIsUnknown_KeepsTheUnknownInstallationRefusal()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = ScriptedHttpHandler.AdminLoginCheckJson(
            HttpStatusCode.NotFound,
            AdminLoginCheckTestData.OutcomeJson("unknown_installation"));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.InsertDeanAsync(ct);
        host.Google.Email = Dean;

        var (client, _) = await host.SignInWithGoogleAsync(ct);
        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.UnknownInstallation, audit.RefusalCategory);
        Assert.Contains(host.Text(SignInTestData.TextKeys.RefusedCouldNotConfirm, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-003, FR-004: a Control Plane that does not answer keeps its category whatever the account's role.</summary>
    [Fact]
    public async Task ADeanAccount_WhenTheControlPlaneDoesNotAnswer_KeepsTheUnavailableRefusal()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = ScriptedHttpHandler.AdminLoginCheck(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.InternalServerError));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.InsertDeanAsync(ct);
        host.Google.Email = Dean;

        var (client, _) = await host.SignInWithGoogleAsync(ct);
        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.ControlPlaneUnavailable, audit.RefusalCategory);
        Assert.Contains(host.Text(SignInTestData.TextKeys.RefusedCouldNotConfirm, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-003, FR-004, I-2: for a disabled Dean the role is checked before the disabled state.</summary>
    [Fact]
    public async Task ADisabledDeanAccount_IsRefusedAsNotAnAdminAccount_NotAsDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, Approving(), ct);
        await host.InsertDeanAsync(ct, isDisabled: true);
        host.Google.Email = Dean;
        var before = await AccountsAsJsonAsync(host, ct);

        var (client, callback) = await host.SignInWithGoogleAsync(ct);
        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Null(callback.SetCookie(SignInTestData.SessionCookieName));
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.NotAnAdminAccount, audit.RefusalCategory);
        Assert.Contains(
            host.Text(SignInTestData.TextKeys.RefusedNotAdminAccount, "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            host.Text(SignInTestData.TextKeys.RefusedAccountDisabled, "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.Equal(before, await AccountsAsJsonAsync(host, ct));
    }

    /// <summary>AC-004, FR-009: an approved address with no account still gets a new Admin account and signs in.</summary>
    [Fact]
    public async Task AnApprovedAddressWithNoAccount_StillGetsAnAdminAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, Approving(), ct);
        await host.InsertDeanAsync(ct);

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(SignInTestData.LandingPath, callback.LocationPath);
        Assert.NotNull(callback.SetCookie(SignInTestData.SessionCookieName));
        var admin = Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "admin");
        Assert.Equal(SignInTestData.AdminEmail, admin.NormalizedEmail);
        Assert.Equal("succeeded", Assert.Single(await host.AuditRowsAsync(ct)).Outcome);
    }

    /// <summary>AC-004, FR-009: an existing enabled Admin account is admitted as before.</summary>
    [Fact]
    public async Task AnExistingAdminAccount_IsStillAdmitted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, Approving(), ct);
        await host.InsertAppUserAsync(ct);
        await host.InsertDeanAsync(ct);

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(SignInTestData.LandingPath, callback.LocationPath);
        Assert.NotNull(callback.SetCookie(SignInTestData.SessionCookieName));
        var admin = Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "admin");
        Assert.Equal(InstallationTestHost.DefaultStart, admin.LastSuccessfulSignInAt);
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("succeeded", audit.Outcome);
        Assert.Equal(admin.Id, audit.ActorId);
    }

    /// <summary>AC-004, FR-004: a disabled Admin account is still refused as "account disabled".</summary>
    [Fact]
    public async Task ADisabledAdminAccount_IsStillRefusedAsDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, Approving(), ct);
        await host.InsertAppUserAsync(ct, isDisabled: true);

        var (client, callback) = await host.SignInWithGoogleAsync(ct);
        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Null(callback.SetCookie(SignInTestData.SessionCookieName));
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.AccountDisabled, audit.RefusalCategory);
        Assert.Equal("admin", audit.ActorRole);
        Assert.Contains(host.Text(SignInTestData.TextKeys.RefusedAccountDisabled, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-005, FR-003: in every read-only cause the refusal and its audit row are written as outside it.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyCauses))]
    public async Task InReadOnlyMode_TheDeanAccountIsRefusedAndAuditedTheSameWay(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, Approving(), ct, cause);
        await host.InsertDeanAsync(ct);
        host.Google.Email = Dean;
        var dean = Assert.Single(await host.AppUsersAsync(ct));
        var before = await AccountsAsJsonAsync(host, ct);

        var (client, callback) = await host.SignInWithGoogleAsync(ct);
        var page = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Equal(SignInTestData.SignInPath, callback.LocationPath);
        Assert.Null(callback.SetCookie(SignInTestData.SessionCookieName));
        Assert.Contains(
            host.Text(SignInTestData.TextKeys.RefusedNotAdminAccount, "uk"),
            page.Text,
            StringComparison.Ordinal);
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("refused", audit.Outcome);
        Assert.Equal(dean.Id, audit.ActorId);
        Assert.Equal("dean", audit.ActorRole);
        Assert.Equal(SignInTestData.RefusalCategories.NotAnAdminAccount, audit.RefusalCategory);
        Assert.Equal(before, await AccountsAsJsonAsync(host, ct));
    }

    /// <summary>
    /// AC-006, FR-009: after the refusal the Dean signs in with login and password as before — a temporary password
    /// still leads to the forced change, and the change completes.
    /// </summary>
    [Fact]
    public async Task AfterTheRefusal_TheDeanStillSignsInWithThePassword_ThroughTheForcedChange()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, Approving(), ct);
        await host.InsertDeanAsync(ct);
        host.Google.Email = Dean;
        await host.SignInWithGoogleAsync(ct);
        using var client = host.CreateClient();

        var signIn = await client.SignInAsDeanAsync(Dean, DeanAccountTestData.TemporaryPassword, ct);
        var change = await client.CompleteForcedChangeAsync(DeanAccountTestData.NewPassword, ct);

        Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
        Assert.Equal(DeanAccountTestData.Paths.ForcedChange, signIn.LocationPath);
        Assert.Equal(HttpStatusCode.Redirect, change.Status);
        Assert.Equal(DeanAccountTestData.Paths.Landing, change.LocationPath);
        var dean = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal("dean", dean.Role);
        Assert.Equal(0, dean.AccessFailedCount);
    }

    /// <summary>
    /// AC-009, FR-007: the refusal is a decision — logged at Warning, under an event of its own (none of the four that
    /// existed before), with the request id and without the email.
    /// </summary>
    [Fact]
    public async Task TheRefusal_IsLoggedAsAWarningWithItsOwnEvent_AndWithoutTheEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, Approving(), ct);
        await host.InsertDeanAsync(ct);
        host.Google.Email = Dean;

        await host.SignInWithGoogleAsync(ct);
        var requestId = Assert.Single(await host.AuditRowsAsync(ct)).RequestId;
        var events = await host.ReadLogEventsAsync(ct);

        var refusal = Assert.Single(events, e => e.EventName is { } name && name.StartsWith("AdminSignIn", StringComparison.Ordinal));
        Assert.Equal("Warning", refusal.Level);
        Assert.DoesNotContain(refusal.EventName, PreviousRefusalEvents);
        Assert.NotEqual("AdminSignInSucceeded", refusal.EventName);
        Assert.Equal(requestId, refusal.Property("RequestId"));
        var log = string.Join("\n", events.Select(e => e.Line));
        Assert.DoesNotContain(Dean, log, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The sign-in audit rows as their full JSON text. The background retention purge writes its own row at start, so
    /// it is excluded here as <see cref="InstallationTestHost.AuditRowsAsync"/> excludes it.
    /// </summary>
    private static async Task<IReadOnlyList<string>> SignInRowsAsJsonAsync(InstallationTestHost host, CancellationToken ct) =>
        await host.QueryAsync(
            "SELECT row_to_json(a)::text FROM audit_event a WHERE a.action = 'admin_sign_in' ORDER BY a.id",
            r => r.GetString(0),
            ct);

    /// <summary>Every account row as its full JSON text — any column a sign-in could touch is in it.</summary>
    private static async Task<IReadOnlyList<string>> AccountsAsJsonAsync(InstallationTestHost host, CancellationToken ct) =>
        await host.QueryAsync("SELECT row_to_json(u)::text FROM app_user u ORDER BY u.id", r => r.GetString(0), ct);
}
