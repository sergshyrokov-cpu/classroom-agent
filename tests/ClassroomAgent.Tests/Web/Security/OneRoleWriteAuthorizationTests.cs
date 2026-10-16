using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-052 AC-001…AC-004 (pre-pilot audit 2, finding F-1): the four state-changing endpoints that only one role may
/// use, each sent by the forbidden role with a real session and a valid antiforgery token — refused with <c>403</c>
/// and the translated error page, nothing written — and by the allowed role, which succeeds, so the refusal is a
/// role boundary and not a broken request (spec FR-001…FR-008; TC-5). The installation is in normal mode
/// throughout, so no refusal can come from the read-only guard (FR-007).
/// </summary>
public sealed class OneRoleWriteAuthorizationTests(PostgreSqlFixture database)
{
    /// <summary>The fresh address the create form posts (VR-001): in the school domain, used by no account.</summary>
    private const string CreatedEmail = "third.dean@school-one.example.test";

    /// <summary>A second policy-valid password for the Dean's own change (VR-003), unlike every other one here.</summary>
    private const string OwnNewPassword = "yet another fine passphrase";

    /// <summary>AC-001, FR-002…FR-004: a Dean posting a complete, valid create form is refused and nothing is written.</summary>
    [Fact]
    public async Task ADean_CreatingADeanAccount_Gets403_AndNothingIsWritten()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        await host.InsertDeanAsync(ct, email: DeanAccountTestData.SecondDeanEmail, passwordIsTemporary: false);
        using var dean = await SignInAsDeanAsync(host, ct);
        var before = await FingerprintAsync(host, ct);

        var page = await dean.PostFormAsync(
            DeanAccountTestData.Paths.Deans,
            [
                new(DeanAccountTestData.Fields.Email, CreatedEmail),
                new(DeanAccountTestData.Fields.TemporaryPassword, DeanAccountTestData.TemporaryPassword),
            ],
            ct);

        AssertForbiddenPage(host, page);
        Assert.DoesNotContain(await host.AppUsersAsync(ct), u => u.Email == CreatedEmail);
        Assert.Equal(before, await FingerprintAsync(host, ct));
    }

    /// <summary>AC-002, FR-002…FR-004: a Dean disabling a second, active Dean is refused; that account is unchanged.</summary>
    [Fact]
    public async Task ADean_DisablingAnotherDean_Gets403_AndTheAccountIsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var secondDean = await InsertSecondDeanAsync(host, ct);
        using var dean = await SignInAsDeanAsync(host, ct);
        var before = await FingerprintAsync(host, ct);

        var page = await dean.PostFormAsync(
            DeanAccountTestData.Paths.State(secondDean),
            [new(DeanAccountTestData.Fields.DesiredState, DeanAccountTestData.Fields.StateDisabled)],
            ct);

        AssertForbiddenPage(host, page);
        Assert.Equal(before, await FingerprintAsync(host, ct));
    }

    /// <summary>AC-002, FR-002…FR-004: a Dean resetting a second Dean's password is refused; that account is unchanged.</summary>
    [Fact]
    public async Task ADean_ResettingAnotherDeansPassword_Gets403_AndTheAccountIsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var secondDean = await InsertSecondDeanAsync(host, ct);
        using var dean = await SignInAsDeanAsync(host, ct);
        var before = await FingerprintAsync(host, ct);

        var page = await dean.PostFormAsync(
            DeanAccountTestData.Paths.Password(secondDean),
            [new(DeanAccountTestData.Fields.TemporaryPassword, DeanAccountTestData.NewPassword)],
            ct);

        AssertForbiddenPage(host, page);
        Assert.Equal(before, await FingerprintAsync(host, ct));
    }

    /// <summary>AC-003, FR-005, FR-006: an Admin posting a complete own-password form is refused; the account is unchanged.</summary>
    [Fact]
    public async Task AnAdmin_PostingTheOwnPasswordForm_Gets403_AndTheAccountIsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _admin = admin;
        await admin.GetAsync(DeanAccountTestData.Paths.Landing, ct);
        var before = await FingerprintAsync(host, ct);

        var page = await admin.PostFormAsync(
            DeanAccountTestData.Paths.OwnPassword,
            [
                new(DeanAccountTestData.Fields.CurrentPassword, DeanAccountTestData.TemporaryPassword),
                new(DeanAccountTestData.Fields.NewPassword, OwnNewPassword),
            ],
            ct);

        AssertForbiddenPage(host, page);
        var account = Assert.Single(await host.AppUsersAsync(ct), u => u.Email == SignInTestData.AdminEmail);
        Assert.Null(account.PasswordHash);
        Assert.Equal("admin", account.Role);
        Assert.Equal(before, await FingerprintAsync(host, ct));
    }

    /// <summary>AC-004, FR-008: the same create form from an Admin succeeds — redirect, the account, one audit row.</summary>
    [Fact]
    public async Task AnAdmin_CreatingADeanAccount_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _admin = admin;
        await admin.GetAsync(DeanAccountTestData.Paths.Landing, ct);
        var auditBefore = await host.AuditRowsAsync(ct);

        var page = await admin.PostFormAsync(
            DeanAccountTestData.Paths.Deans,
            [
                new(DeanAccountTestData.Fields.Email, CreatedEmail),
                new(DeanAccountTestData.Fields.TemporaryPassword, DeanAccountTestData.TemporaryPassword),
            ],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        var created = Assert.Single(await host.AppUsersAsync(ct), u => u.Email == CreatedEmail);
        Assert.Equal("dean", created.Role);
        AssertOneNewAuditRow(auditBefore, await host.AuditRowsAsync(ct), DeanAccountTestData.Audit.Created);
    }

    /// <summary>AC-004, FR-008: the same state change from an Admin disables the second Dean, with one audit row.</summary>
    [Fact]
    public async Task AnAdmin_DisablingADean_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _admin = admin;
        var secondDean = await InsertSecondDeanAsync(host, ct);
        await admin.GetAsync(DeanAccountTestData.Paths.Landing, ct);
        var auditBefore = await host.AuditRowsAsync(ct);

        var page = await admin.PostFormAsync(
            DeanAccountTestData.Paths.State(secondDean),
            [new(DeanAccountTestData.Fields.DesiredState, DeanAccountTestData.Fields.StateDisabled)],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.True(Assert.Single(await host.AppUsersAsync(ct), u => u.Id == secondDean).IsDisabled);
        AssertOneNewAuditRow(auditBefore, await host.AuditRowsAsync(ct), DeanAccountTestData.Audit.Disabled);
    }

    /// <summary>AC-004, FR-008: the same reset from an Admin changes the second Dean's password, with one audit row.</summary>
    [Fact]
    public async Task AnAdmin_ResettingADeansPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _admin = admin;
        var secondDean = await InsertSecondDeanAsync(host, ct);
        await admin.GetAsync(DeanAccountTestData.Paths.Landing, ct);
        var hashBefore = (await host.AppUsersAsync(ct)).Single(u => u.Id == secondDean).PasswordHash;
        var auditBefore = await host.AuditRowsAsync(ct);

        var page = await admin.PostFormAsync(
            DeanAccountTestData.Paths.Password(secondDean),
            [new(DeanAccountTestData.Fields.TemporaryPassword, DeanAccountTestData.NewPassword)],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.NotEqual(hashBefore, (await host.AppUsersAsync(ct)).Single(u => u.Id == secondDean).PasswordHash);
        AssertOneNewAuditRow(auditBefore, await host.AuditRowsAsync(ct), DeanAccountTestData.Audit.PasswordReset);
    }

    /// <summary>AC-004, FR-008: the same own-password form from a full Dean session changes the hash, with one audit row.</summary>
    [Fact]
    public async Task ADean_ChangingTheOwnPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var dean = await SignInAsDeanAsync(host, ct);
        var hashBefore = (await host.AppUsersAsync(ct)).Single(u => u.Email == DeanAccountTestData.DeanEmail).PasswordHash;
        var auditBefore = await host.AuditRowsAsync(ct);

        var page = await dean.PostFormAsync(
            DeanAccountTestData.Paths.OwnPassword,
            [
                new(DeanAccountTestData.Fields.CurrentPassword, DeanAccountTestData.NewPassword),
                new(DeanAccountTestData.Fields.NewPassword, OwnNewPassword),
            ],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.NotEqual(
            hashBefore,
            (await host.AppUsersAsync(ct)).Single(u => u.Email == DeanAccountTestData.DeanEmail).PasswordHash);
        AssertOneNewAuditRow(auditBefore, await host.AuditRowsAsync(ct), DeanAccountTestData.Audit.PasswordChanged);
    }

    /// <summary>A host in normal mode (FR-007): the Control Plane approves and the last legitimacy check succeeded.</summary>
    private Task<InstallationTestHost> StartAsync(CancellationToken cancellationToken) =>
        InstallationTestHost.StartWithControlPlaneHttpAsync(
            database,
            DeanAccountHostExtensions.ApprovingChannel(),
            cancellationToken);

    /// <summary>
    /// FR-001, spec I-4: a full Dean session — password sign-in, then the forced temporary-password change — left on
    /// the landing page, whose token belongs to this session (VR-004).
    /// </summary>
    private static async Task<FormClient> SignInAsDeanAsync(InstallationTestHost host, CancellationToken cancellationToken)
    {
        await host.InsertDeanAsync(cancellationToken);
        var client = host.CreateClient();
        var signIn = await client.SignInAsDeanAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
        var forced = await client.CompleteForcedChangeAsync(DeanAccountTestData.NewPassword, cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, forced.Status);
        var landing = await client.GetAsync(DeanAccountTestData.Paths.Landing, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, landing.Status);
        return client;
    }

    /// <summary>The second, existing, active Dean account of AC-002, with a permanent password.</summary>
    private static async Task<long> InsertSecondDeanAsync(InstallationTestHost host, CancellationToken cancellationToken)
    {
        await host.InsertDeanAsync(cancellationToken, email: DeanAccountTestData.SecondDeanEmail, passwordIsTemporary: false);
        return (await host.AppUsersAsync(cancellationToken)).Single(u => u.Email == DeanAccountTestData.SecondDeanEmail).Id;
    }

    /// <summary>FR-003, FR-006, SC-4: <c>403</c> with the translated "not permitted" page — not a redirect, 400, 404 or 409.</summary>
    private static void AssertForbiddenPage(InstallationTestHost host, PageResponse page)
    {
        Assert.Equal(HttpStatusCode.Forbidden, page.Status);
        Assert.Contains(host.Text(SignInTestData.TextKeys.Forbidden, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>FR-008, spec I-5: exactly one row was added, and it is the action's own.</summary>
    private static void AssertOneNewAuditRow(
        IReadOnlyList<InstallationAuditRow> before,
        IReadOnlyList<InstallationAuditRow> after,
        string action)
    {
        var known = before.Select(r => r.Id).ToHashSet();
        var added = Assert.Single(after, r => !known.Contains(r.Id));
        Assert.Equal(action, added.Action);
    }

    /// <summary>
    /// FR-004, FR-006: every account — state, password hash, failed-attempt count, lockout end, security stamp and
    /// the rest of its row — and the number of audit rows. Equal before and after means the request had no effect.
    /// </summary>
    private static async Task<string> FingerprintAsync(InstallationTestHost host, CancellationToken cancellationToken)
    {
        var accounts = new List<string>();
        foreach (var account in await host.AppUsersAsync(cancellationToken))
        {
            accounts.Add($"{account} stamp={await host.SecurityStampAsync(account.Id, cancellationToken)}");
        }

        return string.Join("\n", accounts) + $"\naudit rows={(await host.AuditRowsAsync(cancellationToken)).Count}";
    }
}
