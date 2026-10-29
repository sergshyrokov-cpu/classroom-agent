using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-067: the Admin changes a Dean's login through <c>POST /settings/deans/{deanId}/login</c> (form field
/// <c>newEmail</c>) — the screen, Post-Redirect-Get, refusals, session end, audit, log hygiene, read-only mode and
/// authorization. Red-phase tests written against the approved specification and API design.
/// </summary>
public sealed class ChangeDeanLoginPageTests(PostgreSqlFixture database)
{
    private const string NewEmail = "olena.shevchenko@school-one.example.test";

    private const string AdminMarker = "<admin>";

    private sealed record DeanRow(
        string Email,
        string NormalizedEmail,
        string SecurityStamp,
        string? PasswordHash,
        bool PasswordIsTemporary,
        int AccessFailedCount,
        DateTimeOffset? LockoutEnd,
        bool IsDisabled,
        string UiLanguage,
        DateTimeOffset? LastSuccessfulSignInAt,
        DateTimeOffset CreatedAt);

    private static async Task<DeanRow> DeanRowAsync(InstallationTestHost host, long id, CancellationToken ct)
    {
        var rows = await host.QueryAsync(
            """
            SELECT email, normalized_email, security_stamp, password_hash, password_is_temporary, access_failed_count,
                   lockout_end, is_disabled, ui_language, last_successful_sign_in_at, created_at
            FROM app_user WHERE id = @id
            """,
            r => new DeanRow(
                r.GetString(0),
                r.GetString(1),
                r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.GetBoolean(4),
                r.GetInt32(5),
                r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6),
                r.GetBoolean(7),
                r.GetString(8),
                r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9),
                r.GetFieldValue<DateTimeOffset>(10)),
            ct,
            ("id", id));
        return Assert.Single(rows);
    }

    private static async Task<long> OnlyDeanIdAsync(InstallationTestHost host, CancellationToken ct) =>
        Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "dean").Id;

    private static async Task<long> AdminIdAsync(InstallationTestHost host, CancellationToken ct) =>
        Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "admin").Id;

    private static async Task<IReadOnlyList<InstallationAuditRow>> LoginChangedRowsAsync(InstallationTestHost host, CancellationToken ct) =>
        (await host.AuditRowsAsync(ct)).Where(r => r.Action == DeanAccountTestData.Audit.LoginChanged).ToList();

    /// <summary>FR-010, D-1, D-2: the screen offers a login change form for each Dean.</summary>
    [Fact]
    public async Task TheScreen_OffersALoginChangeFormForEachDean()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var id = await OnlyDeanIdAsync(host, ct);

        var page = await client.OpenDeanAccountsAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains($"action=\"/settings/deans/{id}/login\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("name=\"newEmail\"", page.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text(DeanAccountTestData.TextKeys.ChangeLoginEmailLabel, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(DeanAccountTestData.TextKeys.ActionChangeLogin, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-001, FR-003: a valid change redirects back; the screen shows the new login and the confirmation.</summary>
    [Fact]
    public async Task AValidChange_RedirectsBack_AndTheScreenShowsTheNewLoginAndTheConfirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var id = await OnlyDeanIdAsync(host, ct);

        var posted = await client.ChangeDeanLoginAsync(id, NewEmail, ct);
        var screen = await client.OpenDeanAccountsAsync(ct);

        Assert.Equal(HttpStatusCode.Redirect, posted.Status);
        Assert.Equal(DeanAccountTestData.Paths.Deans, posted.LocationPath);
        Assert.Contains(NewEmail, screen.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(DeanAccountTestData.TextKeys.LoginChangedConfirmation, "uk"), screen.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(DeanAccountTestData.DeanEmail, screen.Text, StringComparison.Ordinal);
        var dean = Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "dean");
        Assert.Equal(id, dean.Id);
        Assert.Equal(NewEmail, dean.Email);
        Assert.Equal(NewEmail, dean.NormalizedEmail);
    }

    /// <summary>AC-001, I-6: the Dean's templates show the new address as their author.</summary>
    [Fact]
    public async Task TheDeansTemplates_ShowTheNewAddressAsAuthor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var id = await OnlyDeanIdAsync(host, ct);
        await host.InsertTemplateAsync(ct, name: "US-067 template", authorId: id);

        var before = await client.GetAsync(ReportTemplateTestData.ListPath, ct);
        var posted = await client.ChangeDeanLoginAsync(id, NewEmail, ct);
        var after = await client.GetAsync(ReportTemplateTestData.ListPath, ct);

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Contains(DeanAccountTestData.DeanEmail, before.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, posted.Status);
        Assert.Equal(HttpStatusCode.OK, after.Status);
        Assert.Contains(NewEmail, after.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(DeanAccountTestData.DeanEmail, after.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-001: earlier audit rows still refer to the same account — its id never changes.</summary>
    [Fact]
    public async Task EarlierAuditRows_StillReferToTheSameAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct, passwordIsTemporary: false, password: JournalTestData.DeanPassword);
        var id = await OnlyDeanIdAsync(host, ct);
        using var dean = host.CreateClient();
        var signIn = await dean.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, JournalTestData.DeanPassword, ct);
        Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
        var earlier = (await host.AuditRowsAsync(ct))
            .Where(r => r.Action == DeanAccountTestData.Audit.SignIn && r.ActorId == id)
            .ToList();
        Assert.NotEmpty(earlier);

        var posted = await client.ChangeDeanLoginAsync(id, NewEmail, ct);

        Assert.Equal(HttpStatusCode.Redirect, posted.Status);
        var afterRows = await host.AuditRowsAsync(ct);
        foreach (var row in earlier)
        {
            Assert.Equal(id, Assert.Single(afterRows, r => r.Id == row.Id).ActorId);
        }

        Assert.Equal(id, await OnlyDeanIdAsync(host, ct));
    }

    /// <summary>AC-002, FR-005: the new address signs in with the old password; the old address is refused.</summary>
    [Fact]
    public async Task TheNewAddressSignsInWithTheOldPassword_AndTheOldAddressIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct, passwordIsTemporary: false, password: JournalTestData.DeanPassword);
        var id = await OnlyDeanIdAsync(host, ct);

        var posted = await client.ChangeDeanLoginAsync(id, NewEmail, ct);
        Assert.Equal(HttpStatusCode.Redirect, posted.Status);
        using var fresh = host.CreateClient();
        var viaNew = await fresh.SignInAsDeanAsync("Olena.Shevchenko@School-One.Example.Test", JournalTestData.DeanPassword, ct);
        var lastAuditId = await host.ScalarAsync<long>("SELECT coalesce(max(id), 0) FROM audit_event", ct);
        using var other = host.CreateClient();
        var viaOld = await other.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, JournalTestData.DeanPassword, ct);

        Assert.Equal(HttpStatusCode.Redirect, viaNew.Status);
        Assert.NotEqual(DeanAccountTestData.Paths.SignIn, viaNew.LocationPath);
        Assert.NotEqual(DeanAccountTestData.Paths.ForcedChange, viaNew.LocationPath);
        Assert.Equal(HttpStatusCode.Redirect, viaOld.Status);
        Assert.Equal(DeanAccountTestData.Paths.SignIn, viaOld.LocationPath);
        Assert.Contains(
            await host.AuditRowsAsync(ct),
            r => r.Id > lastAuditId
                && r.Action == DeanAccountTestData.Audit.SignIn
                && r.RefusalCategory == DeanAccountTestData.Audit.UnknownLogin);
    }

    /// <summary>AC-003: an open session of the Dean ends on its next request.</summary>
    [Fact]
    public async Task AnOpenSessionOfTheDean_EndsOnItsNextRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct, passwordIsTemporary: false, password: JournalTestData.DeanPassword);
        var id = await OnlyDeanIdAsync(host, ct);
        using var dean = host.CreateClient();
        var signIn = await dean.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, JournalTestData.DeanPassword, ct);
        Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
        using var replay = host.CreateClient();
        replay.ReplaceCookies(dean.Cookies);

        var before = await replay.GetAsync(DeanAccountTestData.Paths.OwnPassword, ct);
        var posted = await client.ChangeDeanLoginAsync(id, NewEmail, ct);
        Assert.Equal(HttpStatusCode.Redirect, posted.Status);
        var after = await replay.GetAsync(DeanAccountTestData.Paths.OwnPassword, ct);

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Equal(HttpStatusCode.Redirect, after.Status);
        Assert.Equal(DeanAccountTestData.Paths.SignIn, after.LocationPath);
    }

    /// <summary>
    /// AC-004, FR-011: a refused address changes nothing, writes no audit row, shows the same message as creation and
    /// is not echoed back.
    /// </summary>
    [Theory]
    [InlineData(null, DeanAccountTestData.TextKeys.RefusedNotAnEmail)]
    [InlineData("", DeanAccountTestData.TextKeys.RefusedNotAnEmail)]
    [InlineData("   ", DeanAccountTestData.TextKeys.RefusedNotAnEmail)]
    [InlineData("olena.shevchenko", DeanAccountTestData.TextKeys.RefusedNotAnEmail)]
    [InlineData(DeanAccountTestData.OutsideDomainEmail, DeanAccountTestData.TextKeys.RefusedOutsideDomain)]
    [InlineData("olena.shevchenko@sub.school-one.example.test", DeanAccountTestData.TextKeys.RefusedOutsideDomain)]
    public async Task ARefusedAddress_ChangesNothing_AndShowsTheCreationMessage(string? newEmail, string messageKey)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var id = await OnlyDeanIdAsync(host, ct);
        var before = await DeanRowAsync(host, id, ct);
        var expected = host.Text(messageKey, "uk");

        var posted = await client.ChangeDeanLoginAsync(id, newEmail, ct);
        var creation = await client.CreateDeanAsync(newEmail ?? "", DeanAccountTestData.TemporaryPassword, ct);

        Assert.Equal(HttpStatusCode.BadRequest, posted.Status);
        Assert.Contains(expected, posted.Text, StringComparison.Ordinal);
        Assert.Equal(before, await DeanRowAsync(host, id, ct));
        Assert.Empty(await LoginChangedRowsAsync(host, ct));
        if (!string.IsNullOrWhiteSpace(newEmail))
        {
            Assert.DoesNotContain(newEmail, posted.Body, StringComparison.Ordinal);
        }

        Assert.Equal(HttpStatusCode.BadRequest, creation.Status);
        Assert.Contains(expected, creation.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-004, SC-10: a rejected value is never written to a log.</summary>
    [Fact]
    public async Task TheRejectedValue_IsNotWrittenToTheLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var id = await OnlyDeanIdAsync(host, ct);

        var outside = await client.ChangeDeanLoginAsync(id, "zqrejected.marker@sub.school-one.example.test", ct);
        var malformed = await client.ChangeDeanLoginAsync(id, "zqrejected-malformed", ct);
        Assert.Equal(HttpStatusCode.BadRequest, outside.Status);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.Status);
        var files = await host.ReadLogFilesAsync(ct);

        Assert.All(files, text => Assert.DoesNotContain("zqrejected", text, StringComparison.Ordinal));
    }

    /// <summary>SC-10 (spec section 7): a successful change writes neither the old nor the new address to a log.</summary>
    [Fact]
    public async Task ASuccessfulChange_WritesNeitherAddressToTheLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct, email: "zqold.dean@school-one.example.test");
        var id = await OnlyDeanIdAsync(host, ct);

        var posted = await client.ChangeDeanLoginAsync(id, "zqnew.dean@school-one.example.test", ct);
        Assert.Equal(HttpStatusCode.Redirect, posted.Status);
        var files = await host.ReadLogFilesAsync(ct);

        Assert.All(files, text =>
        {
            Assert.DoesNotContain("zqold.dean", text, StringComparison.Ordinal);
            Assert.DoesNotContain("zqnew.dean", text, StringComparison.Ordinal);
        });
    }

    /// <summary>AC-005: an address already in use (any case, any account including the Admin) changes nothing.</summary>
    [Theory]
    [InlineData(DeanAccountTestData.SecondDeanEmail)]
    [InlineData("Second.Dean@School-One.Example.Test")]
    [InlineData(AdminMarker)]
    public async Task AnAddressInUse_ChangesNothing_AndSaysSo(string taken)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        await host.InsertDeanAsync(ct, email: DeanAccountTestData.SecondDeanEmail);
        var id = await host.AccountIdAsync(DeanAccountTestData.DeanEmail, ct);
        var address = taken == AdminMarker
            ? Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "admin").Email.ToUpperInvariant()
            : taken;
        var before = await DeanRowAsync(host, id, ct);

        var posted = await client.ChangeDeanLoginAsync(id, address, ct);

        Assert.Equal(HttpStatusCode.BadRequest, posted.Status);
        Assert.Contains(host.Text(DeanAccountTestData.TextKeys.RefusedEmailInUse, "uk"), posted.Text, StringComparison.Ordinal);
        Assert.Equal(before, await DeanRowAsync(host, id, ct));
        Assert.Empty(await LoginChangedRowsAsync(host, ct));
    }

    /// <summary>AC-006, OD-001 a: everything but the login and the security stamp stays as it was.</summary>
    [Fact]
    public async Task AllElseOfTheAccountStays_WhenTheLoginChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(
            ct,
            passwordIsTemporary: true,
            isDisabled: true,
            lastSuccessfulSignInAt: host.Time.GetUtcNow().AddDays(-10));
        var id = await OnlyDeanIdAsync(host, ct);
        await host.ExecuteAsync(
            "UPDATE app_user SET access_failed_count = 5, lockout_end = @end WHERE id = @id",
            ct,
            ("end", host.Time.GetUtcNow().AddHours(1)),
            ("id", id));
        var before = await DeanRowAsync(host, id, ct);

        var posted = await client.ChangeDeanLoginAsync(id, NewEmail, ct);
        var after = await DeanRowAsync(host, id, ct);

        Assert.Equal(HttpStatusCode.Redirect, posted.Status);
        Assert.Equal(NewEmail, after.Email);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.True(after.PasswordIsTemporary);
        Assert.Equal(before.PasswordIsTemporary, after.PasswordIsTemporary);
        Assert.Equal(before.AccessFailedCount, after.AccessFailedCount);
        Assert.Equal(before.LockoutEnd, after.LockoutEnd);
        Assert.True(after.IsDisabled);
        Assert.Equal(before.IsDisabled, after.IsDisabled);
        Assert.Equal(before.UiLanguage, after.UiLanguage);
        Assert.Equal(before.LastSuccessfulSignInAt, after.LastSuccessfulSignInAt);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
    }

    /// <summary>AC-007, FR-007: the change is audited once, with the Admin as actor and neither address in the row.</summary>
    [Fact]
    public async Task TheChange_IsAuditedOnce_WithoutEitherAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var id = await OnlyDeanIdAsync(host, ct);
        var adminId = await AdminIdAsync(host, ct);

        var posted = await client.ChangeDeanLoginAsync(id, NewEmail, ct);

        Assert.Equal(HttpStatusCode.Redirect, posted.Status);
        var row = Assert.Single(await LoginChangedRowsAsync(host, ct));
        Assert.Equal("app_user", row.ActorType);
        Assert.Equal(adminId, row.ActorId);
        Assert.Equal("admin", row.ActorRole);
        Assert.Equal(DeanAccountTestData.Audit.TargetType, row.TargetType);
        Assert.Equal(id, row.TargetId);
        Assert.Equal("succeeded", row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.NotNull(row.RequestId);
        var text = Assert.Single(await host.QueryAsync(
            "SELECT row_to_json(a)::text FROM audit_event a WHERE action = 'dean_login_changed'",
            r => r.GetString(0),
            ct));
        Assert.DoesNotContain(NewEmail, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DeanAccountTestData.DeanEmail, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("olena", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-008, FR-008: in read-only mode the change is refused with 409 and only the refusal is audited.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheChangeIsRefused_AndOnlyTheRefusalIsAudited(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var id = await OnlyDeanIdAsync(host, ct);
        var before = await DeanRowAsync(host, id, ct);

        var posted = await client.ChangeDeanLoginAsync(id, NewEmail, ct);

        Assert.Equal(HttpStatusCode.Conflict, posted.Status);
        Assert.Equal(before, await DeanRowAsync(host, id, ct));
        var row = Assert.Single(await LoginChangedRowsAsync(host, ct));
        Assert.Equal("refused", row.Outcome);
        Assert.Equal(DeanAccountTestData.Audit.ReadOnlyMode, row.RefusalCategory);
        Assert.Equal(id, row.TargetId);
        Assert.Equal("admin", row.ActorRole);
    }

    /// <summary>FR-006, OD-002 a, D-3: the account's own current address is answered as unchanged and writes nothing.</summary>
    [Fact]
    public async Task TheAccountsOwnAddress_IsLoginUnchanged_AndNothingIsWritten()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var id = await OnlyDeanIdAsync(host, ct);
        var before = await DeanRowAsync(host, id, ct);

        var posted = await client.ChangeDeanLoginAsync(id, "Dean@School-One.Example.Test", ct);

        Assert.Equal(HttpStatusCode.Conflict, posted.Status);
        Assert.Contains(host.Text(DeanAccountTestData.TextKeys.RefusedLoginUnchanged, "uk"), posted.Text, StringComparison.Ordinal);
        Assert.Equal(before, await DeanRowAsync(host, id, ct));
        Assert.Empty(await LoginChangedRowsAsync(host, ct));
    }

    /// <summary>AC-009, TC-5: a Dean may not change any login, another's or their own.</summary>
    [Fact]
    public async Task ADean_IsForbidden_ForAnotherAccountAndForTheirOwn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct, passwordIsTemporary: false, password: JournalTestData.DeanPassword);
        await host.InsertDeanAsync(ct, email: DeanAccountTestData.SecondDeanEmail);
        var ownId = await host.AccountIdAsync(DeanAccountTestData.DeanEmail, ct);
        var otherId = await host.AccountIdAsync(DeanAccountTestData.SecondDeanEmail, ct);
        using var dean = host.CreateClient();
        var signIn = await dean.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, JournalTestData.DeanPassword, ct);
        Assert.Equal(HttpStatusCode.Redirect, signIn.Status);

        // Taken after the sign-in, which itself records the last sign-in on the Dean's own row.
        var ownBefore = await DeanRowAsync(host, ownId, ct);
        var otherBefore = await DeanRowAsync(host, otherId, ct);

        var onOther = await dean.ChangeDeanLoginAsync(otherId, NewEmail, ct);
        var onOwn = await dean.ChangeDeanLoginAsync(ownId, NewEmail, ct);

        Assert.Equal(HttpStatusCode.Forbidden, onOther.Status);
        Assert.Equal(HttpStatusCode.Forbidden, onOwn.Status);
        Assert.Equal(ownBefore, await DeanRowAsync(host, ownId, ct));
        Assert.Equal(otherBefore, await DeanRowAsync(host, otherId, ct));
        Assert.Empty(await LoginChangedRowsAsync(host, ct));
    }

    /// <summary>AC-009, SC-4: an anonymous request is sent to sign-in and changes nothing.</summary>
    [Fact]
    public async Task AnAnonymousRequest_IsSentToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var id = await OnlyDeanIdAsync(host, ct);
        var before = await DeanRowAsync(host, id, ct);
        using var anonymous = host.CreateClient();

        var posted = await anonymous.ChangeDeanLoginAsync(id, NewEmail, ct, withToken: false);

        Assert.Equal(HttpStatusCode.Redirect, posted.Status);
        Assert.Equal(DeanAccountTestData.Paths.SignIn, posted.LocationPath);
        Assert.Equal(before, await DeanRowAsync(host, id, ct));
    }

    /// <summary>AC-010: the old address is free for a new account once the login has changed.</summary>
    [Fact]
    public async Task TheOldAddress_IsFreeForANewAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var id = await OnlyDeanIdAsync(host, ct);

        var changed = await client.ChangeDeanLoginAsync(id, NewEmail, ct);
        Assert.Equal(HttpStatusCode.Redirect, changed.Status);
        var created = await client.CreateDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);

        Assert.Equal(HttpStatusCode.Redirect, created.Status);
        var deans = (await host.AppUsersAsync(ct)).Where(u => u.Role == "dean").ToList();
        Assert.Equal(2, deans.Count);
        Assert.Equal(id, Assert.Single(deans, u => u.Email == NewEmail).Id);
        Assert.NotEqual(id, Assert.Single(deans, u => u.Email == DeanAccountTestData.DeanEmail).Id);
    }

    /// <summary>FR-002 step 2: an unknown id and an Admin's id both answer 404, never revealing which exists.</summary>
    [Fact]
    public async Task AnUnknownIdAndAnAdminsId_Answer404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        var admin = Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "admin");

        var screen = await client.OpenDeanAccountsAsync(ct);
        var onNobody = await client.ChangeDeanLoginAsync(9999, NewEmail, ct);
        var onAdmin = await client.ChangeDeanLoginAsync(admin.Id, NewEmail, ct);

        // The screen must exist, or the two 404s below would say nothing about the id (red-phase check).
        Assert.Equal(HttpStatusCode.OK, screen.Status);
        Assert.Equal(HttpStatusCode.NotFound, onNobody.Status);
        Assert.Equal(HttpStatusCode.NotFound, onAdmin.Status);
        var after = Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "admin");
        Assert.Equal(admin.Email, after.Email);
    }

    /// <summary>Spec section 8: a POST without an antiforgery token is refused and changes nothing.</summary>
    [Fact]
    public async Task APostWithoutAnAntiforgeryToken_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var id = await OnlyDeanIdAsync(host, ct);
        var before = await DeanRowAsync(host, id, ct);

        var posted = await client.ChangeDeanLoginAsync(id, NewEmail, ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, posted.Status);
        Assert.Equal(before, await DeanRowAsync(host, id, ct));
    }

    /// <summary>Spec section 6: a non-numeric id matches no route.</summary>
    [Fact]
    public async Task ANonNumericId_MatchesNoRoute()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await client.OpenDeanAccountsAsync(ct);

        var posted = await client.PostFormAsync(
            "/settings/deans/abc/login",
            [new(DeanAccountTestData.Fields.NewEmail, NewEmail)],
            ct);

        Assert.Equal(HttpStatusCode.NotFound, posted.Status);
    }
}
