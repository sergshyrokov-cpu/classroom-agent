using System.Net;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-008 AC-011: an approved Admin signs in while the installation is in read-only mode — creating the
/// account, stamping the sign-in time and writing the audit row are the BR-026 service writes and they run —
/// and the closed list is <b>not</b> widened to let them (spec FR-013; US-007 FR-004, FR-005; TC-5).
/// </summary>
public sealed class SignInInReadOnlyModeTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    public static TheoryData<ReadOnlyModeHost.Cause> ReadOnlyCauses => ReadOnlyModeHost.ReadOnlyCauses;

    [Theory]
    [MemberData(nameof(ReadOnlyCauses))]
    public async Task InEveryReadOnlyCause_AnApprovedAdminSignsIn(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct, cause);

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        Assert.Equal(SignInTestData.LandingPath, callback.LocationPath);
        Assert.NotNull(callback.SetCookie(SignInTestData.SessionCookieName));
    }

    /// <summary>AC-011: the account is created and stamped in read-only mode — both are SignInBookkeeping.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyCauses))]
    public async Task InReadOnlyMode_TheAccountIsCreatedAndStamped(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct, cause);

        await host.SignInWithGoogleAsync(ct);

        var user = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal("admin", user.Role);
        Assert.Equal(InstallationTestHost.DefaultStart, user.LastSuccessfulSignInAt);
    }

    /// <summary>AC-011: the audit row is written in read-only mode — it is the AuditEvent member of BR-026.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyCauses))]
    public async Task InReadOnlyMode_TheAuditRowIsWritten(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct, cause);

        await host.SignInWithGoogleAsync(ct);

        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("succeeded", audit.Outcome);
    }

    /// <summary>AC-011: a refusal is audited in read-only mode too.</summary>
    [Fact]
    public async Task InReadOnlyMode_ARefusalIsStillAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(false));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(
            database,
            channel,
            ct,
            ReadOnlyModeHost.Cause.Suspended);

        await host.SignInWithGoogleAsync(ct);

        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("refused", audit.Outcome);
        Assert.Equal(SignInTestData.RefusalCategories.NotInAllowedAdmin, audit.RefusalCategory);
        Assert.Empty(await host.AppUsersAsync(ct));
    }

    /// <summary>
    /// AC-011, FR-013: the BR-026 closed list keeps exactly its four members. US-007 test-locked this; the
    /// sign-in registers against the existing members rather than adding one.
    /// </summary>
    [Fact]
    public void TheBr026List_StillHasExactlyFourMembers()
    {
        Assert.Equal(
            new[]
            {
                nameof(PermittedServiceWrite.AuditEvent),
                nameof(PermittedServiceWrite.LegitimacyCheckState),
                nameof(PermittedServiceWrite.RetentionPurge),
                nameof(PermittedServiceWrite.SignInBookkeeping),
            },
            Enum.GetNames<PermittedServiceWrite>().Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC-011, US-007 FR-005: the sign-in use case is a declared service write, against the existing members
    /// only. A private exemption inside the use case fails this test.
    /// </summary>
    [Fact]
    public void TheSignInWrite_IsDeclaredAgainstSignInBookkeepingOrAuditEvent()
    {
        var declared = PermittedServiceWrites.Declarations;

        Assert.Contains(
            declared,
            entry => entry.Value is PermittedServiceWrite.SignInBookkeeping or PermittedServiceWrite.AuditEvent);
        Assert.All(
            declared.Values,
            write => Assert.Contains(write, Enum.GetValues<PermittedServiceWrite>()));
    }

    /// <summary>
    /// AC-014, BR-026 (trebovaniya.md v79): signing out rotates the account's security stamp, and that write is on
    /// the closed list, so it runs in every read-only cause — otherwise a user of a suspended school could not end
    /// a session. Raised as security-review finding F-2.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadOnlyCauses))]
    public async Task InReadOnlyMode_SigningOutEndsTheSession(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct, cause);
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);
        var stampBefore = (await host.AppUsersAsync(ct)).Single();
        var cookies = client.Cookies;

        var signOut = await client.PostFormAsync(SignInTestData.SignOutPath, [], ct);

        Assert.Equal(HttpStatusCode.Redirect, signOut.Status);
        Assert.Equal(SignInTestData.SignInPath, signOut.LocationPath);

        // The write ran: the stamp moved, so the cookie someone kept a copy of no longer authenticates.
        var after = Assert.Single(await host.AppUsersAsync(ct));
        Assert.NotEqual(stampBefore.CreatedAt, default);
        using var replay = host.CreateClient();
        replay.ReplaceCookies(cookies);
        var replayed = await replay.GetAsync(SignInTestData.LandingPath, ct);
        Assert.Equal(HttpStatusCode.Redirect, replayed.Status);
        Assert.Equal(SignInTestData.SignInPath, replayed.LocationPath);
        Assert.Equal(stampBefore.Id, after.Id);
    }

    /// <summary>AC-011, I-13: signing out in read-only mode still writes no audit row.</summary>
    [Fact]
    public async Task InReadOnlyMode_SigningOutWritesNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(
            database,
            channel,
            ct,
            ReadOnlyModeHost.Cause.Suspended);
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);
        var before = await host.AuditRowsAsync(ct);

        await client.PostFormAsync(SignInTestData.SignOutPath, [], ct);

        Assert.Equal(before, await host.AuditRowsAsync(ct));
    }

    /// <summary>AC-011: nothing else this Story adds writes in read-only mode — the legitimacy state is untouched.</summary>
    [Fact]
    public async Task InReadOnlyMode_NothingElseIsWritten()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(
            database,
            channel,
            ct,
            ReadOnlyModeHost.Cause.GracePeriodExpired);
        var before = await host.LegitimacyStatesAsync(ct);

        var (client, _) = await host.SignInWithGoogleAsync(ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(before, await host.LegitimacyStatesAsync(ct));
    }
}
