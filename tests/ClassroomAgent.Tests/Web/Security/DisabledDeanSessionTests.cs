using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-064: disabling a Dean takes effect on the next request (BR-014, SC-2, <c>trebovaniya.md</c> §2 v103). The Dean's
/// cookies are captured before the Admin disables the account through the Dean-accounts page and replayed from a
/// separate client afterwards (spec FR-001, FR-002; the <see cref="InstallationSignOutTests"/> replay model). Every test
/// first shows the same replayed request succeeding before the disabling, so a refusal afterwards is caused by the
/// disabling and not by a broken fixture (spec FR-003). Normal mode throughout: licensed, not read-only (BR-026).
/// </summary>
public sealed class DisabledDeanSessionTests(PostgreSqlFixture database)
{
    /// <summary>The September journal of the seeded course — a page that shows the course name when it opens.</summary>
    private static string JournalUrl(SeededJournal seeded) =>
        JournalTestData.Url(seeded.CourseId, JournalTestData.Period.FromText, JournalTestData.Period.ToText);

    /// <summary>AC-001, FR-004: the replayed cookie of a disabled Dean is sent to sign-in and gets no journal.</summary>
    [Fact]
    public async Task TheOldCookieOfADisabledDean_IsSentToSignIn_OnTheJournalPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _admin = admin;
        var seeded = await host.SeedJournalAsync(ct);
        using var dean = await SignInDeanAsync(host, DeanAccountTestData.DeanEmail, ct);
        using var replay = ReplayOf(host, dean);

        var before = await replay.GetAsync(JournalUrl(seeded), ct);
        await DisableAsync(host, admin, DeanAccountTestData.DeanEmail, ct);
        var after = await replay.GetAsync(JournalUrl(seeded), ct);

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Contains(SeededJournal.CourseName, before.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, after.Status);
        Assert.Equal(SignInTestData.SignInPath, after.LocationPath);
        Assert.DoesNotContain(SeededJournal.CourseName, after.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-002, FR-005, VR-001: the replayed export — same cookies, same antiforgery token, same body that produced a file
    /// before the disabling — is the <c>401</c> API-6 body, no file, and writes no further export audit event.
    /// </summary>
    [Fact]
    public async Task TheOldCookieOfADisabledDean_DownloadsNoExport()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _admin = admin;
        var seeded = await host.SeedJournalAsync(ct);
        using var dean = await SignInDeanAsync(host, DeanAccountTestData.DeanEmail, ct);
        await dean.GetAsync(ReportTemplateTestData.ReportPath, ct);
        var token = dean.LastToken ?? throw new InvalidOperationException("The report page carried no antiforgery token.");
        using var replay = ReplayOf(host, dean);
        var body = JsonSerializer.Serialize(JournalExportHostExtensions.September(seeded.CourseId));

        var before = await PostExportAsync(replay, body, token, ct);
        var auditedBefore = (await host.ExportAuditRowsAsync(ct)).Count;
        await DisableAsync(host, admin, DeanAccountTestData.DeanEmail, ct);
        var after = await PostExportAsync(replay, body, token, ct);
        var auditedAfter = (await host.ExportAuditRowsAsync(ct)).Count;

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Equal(JournalExportHostExtensions.XlsxType, before.Header("Content-Type"));
        Assert.Equal(1, auditedBefore);

        Assert.Equal(HttpStatusCode.Unauthorized, after.Status);
        Assert.Null(after.Location);
        Assert.StartsWith("application/json", after.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        Assert.Null(after.Header("Content-Disposition"));
        var json = after.Json();
        Assert.True(json.TryGetProperty("timestamp", out _));
        Assert.Equal(401, json.GetProperty("status").GetInt32());
        Assert.Equal("unauthorized", json.GetProperty("error").GetString());
        Assert.Equal(host.Text("Api.Error.SignInRequired", "uk"), json.GetProperty("message").GetString());
        Assert.Equal(JournalExportHostExtensions.ExportPath, json.GetProperty("path").GetString());
        Assert.Equal(auditedBefore, auditedAfter);
    }

    /// <summary>
    /// AC-003, FR-006: enabling the account again does not revive the old cookie; the Dean signs in again with the
    /// password and opens the journal.
    /// </summary>
    [Fact]
    public async Task ReEnabling_DoesNotReviveTheOldCookie_ButANewSignInWorks()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _admin = admin;
        var seeded = await host.SeedJournalAsync(ct);
        using var dean = await SignInDeanAsync(host, DeanAccountTestData.DeanEmail, ct);
        using var replay = ReplayOf(host, dean);

        var before = await replay.GetAsync(JournalUrl(seeded), ct);
        await DisableAsync(host, admin, DeanAccountTestData.DeanEmail, ct);
        var reEnabled = await SetStateAsync(host, admin, DeanAccountTestData.DeanEmail, DeanAccountTestData.Fields.StateActive, ct);
        var after = await replay.GetAsync(JournalUrl(seeded), ct);
        using var again = await SignInDeanAsync(host, DeanAccountTestData.DeanEmail, ct, seed: false);
        var journal = await again.GetAsync(JournalUrl(seeded), ct);

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Equal(HttpStatusCode.Redirect, reEnabled.Status);
        Assert.Equal(HttpStatusCode.Redirect, after.Status);
        Assert.Equal(SignInTestData.SignInPath, after.LocationPath);
        Assert.Equal(HttpStatusCode.OK, journal.Status);
        Assert.Contains(SeededJournal.CourseName, journal.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-004, FR-007: disabling one Dean leaves another Dean's session working, and in the same test the disabled
    /// Dean's cookie is refused — the test cannot pass when the disabling did not happen.
    /// </summary>
    [Fact]
    public async Task DisablingOneDean_LeavesAnotherDeansSessionWorking()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _admin = admin;
        var seeded = await host.SeedJournalAsync(ct);
        using var first = await SignInDeanAsync(host, DeanAccountTestData.DeanEmail, ct);
        using var second = await SignInDeanAsync(host, DeanAccountTestData.SecondDeanEmail, ct);
        using var firstReplay = ReplayOf(host, first);
        using var secondReplay = ReplayOf(host, second);

        var firstBefore = await firstReplay.GetAsync(JournalUrl(seeded), ct);
        var secondBefore = await secondReplay.GetAsync(JournalUrl(seeded), ct);
        await DisableAsync(host, admin, DeanAccountTestData.DeanEmail, ct);
        var firstAfter = await firstReplay.GetAsync(JournalUrl(seeded), ct);
        var secondAfter = await secondReplay.GetAsync(JournalUrl(seeded), ct);

        Assert.Equal(HttpStatusCode.OK, firstBefore.Status);
        Assert.Equal(HttpStatusCode.OK, secondBefore.Status);
        Assert.Equal(HttpStatusCode.Redirect, firstAfter.Status);
        Assert.Equal(SignInTestData.SignInPath, firstAfter.LocationPath);
        Assert.Equal(HttpStatusCode.OK, secondAfter.Status);
        Assert.Contains(SeededJournal.CourseName, secondAfter.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Dean past the first-login password change (BR-014, v64), signed in by password through the real sign-in on a
    /// new client (spec FR-001, FR-006).
    /// </summary>
    private static async Task<FormClient> SignInDeanAsync(
        InstallationTestHost host,
        string email,
        CancellationToken cancellationToken,
        bool seed = true)
    {
        if (seed)
        {
            await host.InsertDeanAsync(cancellationToken, email: email, password: JournalTestData.DeanPassword, passwordIsTemporary: false);
        }

        var client = host.CreateClient();
        var signIn = await client.SignInAsDeanAsync(email, JournalTestData.DeanPassword, cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
        Assert.NotEqual(SignInTestData.SignInPath, signIn.LocationPath);
        Assert.NotEqual(DeanAccountTestData.Paths.ForcedChange, signIn.LocationPath);
        return client;
    }

    /// <summary>A separate client carrying the cookies the Dean's client holds now (spec FR-002).</summary>
    private static FormClient ReplayOf(InstallationTestHost host, FormClient dean)
    {
        var replay = host.CreateClient();
        replay.ReplaceCookies(dean.Cookies);
        return replay;
    }

    /// <summary>The Admin disables the Dean through the Dean-accounts page (spec FR-001).</summary>
    private static async Task DisableAsync(InstallationTestHost host, FormClient admin, string email, CancellationToken cancellationToken)
    {
        var disabled = await SetStateAsync(host, admin, email, DeanAccountTestData.Fields.StateDisabled, cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, disabled.Status);
    }

    private static async Task<PageResponse> SetStateAsync(
        InstallationTestHost host,
        FormClient admin,
        string email,
        string desiredState,
        CancellationToken cancellationToken)
    {
        var deanId = await host.AccountIdAsync(email, cancellationToken);
        return await admin.SetDeanStateAsync(deanId, desiredState, cancellationToken);
    }

    /// <summary>
    /// Posts the export body with a fixed antiforgery token in the header — the token taken before the disabling, since
    /// after it no page would hand the Dean a new one (spec FR-005, VR-001).
    /// </summary>
    private static Task<BinaryResponse> PostExportAsync(FormClient client, string body, string token, CancellationToken cancellationToken)
    {
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
        return client.SendForBytesAsync(
            HttpMethod.Post,
            JournalExportHostExtensions.ExportPath,
            content,
            cancellationToken,
            new Dictionary<string, string> { [JournalExportHostExtensions.TokenHeader] = token });
    }
}
