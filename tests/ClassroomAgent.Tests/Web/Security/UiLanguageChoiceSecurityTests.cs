using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-039 AC-004, AC-005, AC-009, AC-010 and OD-007 on the installation: who may choose, for whom, with which
/// values, where the redirect may lead, and what the re-issued session keeps (spec FR-003, FR-005, FR-006, FR-012,
/// VR-001 … VR-003, §7; openapi <c>POST /account/language</c>; TC-5). Every refusal is paired with the same
/// request allowed to succeed, so an absence is never asserted in a world where nothing happens.
/// </summary>
public sealed class UiLanguageChoiceSecurityTests(PostgreSqlFixture database)
{
    private const string Landing = SignInTestData.LandingPath;

    /// <summary>AC-004: anonymous → the sign-in challenge, nothing stored; the same request signed in succeeds.</summary>
    [Fact]
    public async Task AnAnonymousRequest_IsChallenged_AndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, deanId) = await host.SignInDeanAsync(ct);
        using var _dean = dean;
        using var anonymous = host.CreateClient();
        await anonymous.GetAsync(SignInTestData.SignInPath, ct);

        var refused = await anonymous.PostFormAsync(
            UiLanguageTestData.ChoosePath,
            [new(UiLanguageTestData.Fields.Language, UiLanguageTestData.English)],
            ct);
        var storedAfterRefusal = await host.StoredLanguageAsync(deanId, ct);
        var allowed = await dean.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct);

        Assert.Equal(HttpStatusCode.Redirect, refused.Status);
        Assert.Equal(SignInTestData.SignInPath, refused.LocationPath?.Split('?')[0]);
        Assert.Equal(UiLanguageTestData.Ukrainian, storedAfterRefusal);
        Assert.Equal(HttpStatusCode.Redirect, allowed.Status);
        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(deanId, ct));
    }

    /// <summary>AC-004: the action is not on SC-4's anonymous list and requires the token (endpoint enumeration).</summary>
    [Fact]
    public async Task TheAction_IsAuthenticated_PostOnly_AndNotExemptFromAntiforgery()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoint = Assert.Single(HostEndpoint.All(host.Services), e => e.Pattern == "account/language");

        Assert.False(endpoint.AllowsAnonymous, endpoint.ToString());
        Assert.False(endpoint.IsExemptFromAntiforgery, endpoint.ToString());
        Assert.Equal(["POST"], endpoint.Methods);
    }

    /// <summary>AC-004, VR-003: a GET changes nothing (404 or 405), while the POST of the same session does.</summary>
    [Fact]
    public async Task AGet_ChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, deanId) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var get = await dean.GetAsync(UiLanguageTestData.ChoosePath + "?language=en&returnPath=%2F", ct);
        var page = await dean.GetAsync(Landing, ct);
        var storedAfterGet = await host.StoredLanguageAsync(deanId, ct);
        var post = await dean.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct);

        Assert.Contains(get.Status, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(UiLanguageTestData.Ukrainian, storedAfterGet);
        Assert.Equal(UiLanguageTestData.Ukrainian, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Equal(HttpStatusCode.Redirect, post.Status);
        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(deanId, ct));
    }

    /// <summary>AC-004: no antiforgery token → 400, the page-expired error page, nothing stored.</summary>
    [Fact]
    public async Task WithoutTheToken_TheChoiceIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, deanId) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var refused = await dean.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct, withToken: false);
        var storedAfterRefusal = await host.StoredLanguageAsync(deanId, ct);
        var allowed = await dean.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct);

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Contains(host.Text("Error.PageExpired", UiLanguageTestData.Ukrainian), refused.Text, StringComparison.Ordinal);
        Assert.Equal(UiLanguageTestData.Ukrainian, storedAfterRefusal);
        Assert.Equal(HttpStatusCode.Redirect, allowed.Status);
    }

    /// <summary>
    /// AC-004: the request names no account. A forged <c>accountId</c> / <c>id</c> field naming another Dean is
    /// ignored — the signed-in Dean's own row changes, the other's does not.
    /// </summary>
    [Fact]
    public async Task AnAccountIdInTheRequest_IsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, deanId) = await host.SignInDeanAsync(ct);
        using var _dean = dean;
        var (other, otherId) = await host.SignInDeanAsync(ct, DeanAccountTestData.SecondDeanEmail);
        using var _other = other;
        var forged = otherId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var choice = await dean.ChooseLanguageAsync(
            Landing,
            UiLanguageTestData.English,
            ct,
            extraFields: [new("accountId", forged), new("id", forged), new("userId", forged)]);

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(deanId, ct));
        Assert.Equal(UiLanguageTestData.Ukrainian, await host.StoredLanguageAsync(otherId, ct));
    }

    /// <summary>
    /// AC-005, VR-001, I-6: anything but exactly <c>uk</c> / <c>en</c> → 400 with the error page, nothing stored,
    /// the value neither echoed nor logged; a valid choice afterwards succeeds.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("EN")]
    [InlineData("Uk")]
    [InlineData("de")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData(" en")]
    [InlineData("uk-UA")]
    [InlineData("english")]
    [InlineData(UiLanguageTestData.RejectedMarker)]
    public async Task AnInvalidCode_IsRefused_NotStored_NotLogged(string code)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, deanId) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var refused = await dean.ChooseLanguageAsync(Landing, code, ct);
        var storedAfterRefusal = await host.StoredLanguageAsync(deanId, ct);
        var allowed = await dean.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct);
        var logs = string.Join("\n", await host.ReadLogFilesWhileRunningAsync(ct));

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Contains(host.Text("Error.PageExpired", UiLanguageTestData.Ukrainian), refused.Text, StringComparison.Ordinal);
        Assert.Equal(UiLanguageTestData.Ukrainian, storedAfterRefusal);
        Assert.Equal(HttpStatusCode.Redirect, allowed.Status);
        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(deanId, ct));
        if (code == UiLanguageTestData.RejectedMarker)
        {
            Assert.DoesNotContain(code, refused.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(code, logs, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-005: a missing code and an oversized one are refused the same way.</summary>
    [Fact]
    public async Task AMissingOrOversizedCode_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, deanId) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var missing = await dean.ChooseLanguageAsync(Landing, language: null, ct);
        var oversized = await dean.ChooseLanguageAsync(Landing, new string('e', 5000), ct);
        var storedAfterRefusals = await host.StoredLanguageAsync(deanId, ct);
        var allowed = await dean.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct);

        Assert.Equal(HttpStatusCode.BadRequest, missing.Status);
        Assert.Equal(HttpStatusCode.BadRequest, oversized.Status);
        Assert.Equal(UiLanguageTestData.Ukrainian, storedAfterRefusals);
        Assert.Equal(HttpStatusCode.Redirect, allowed.Status);
    }

    /// <summary>AC-010, VR-002: a return path that is not a local application path leads to <c>/</c>, never off-host — and the choice is still stored.</summary>
    [Theory]
    [InlineData("//evil.example.test/")]
    [InlineData("https://evil.example.test/")]
    [InlineData("http://evil.example.test")]
    [InlineData("/\\evil.example.test")]
    [InlineData("\\\\evil.example.test")]
    [InlineData("evil.example.test")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    public async Task AForeignReturnPath_LeadsToTheLandingPage(string returnPath)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, deanId) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var choice = await dean.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct, returnPath: returnPath);

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal("/", choice.Location);
        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(deanId, ct));
    }

    /// <summary>AC-010, VR-002: an over-long return path is not used either.</summary>
    [Fact]
    public async Task AnOverLongReturnPath_LeadsToTheLandingPage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, _) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var choice = await dean.ChooseLanguageAsync(
            Landing,
            UiLanguageTestData.English,
            ct,
            returnPath: "/" + new string('a', 2048));

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal("/", choice.Location);
    }

    /// <summary>FR-006: a local path with a query is kept as it is.</summary>
    [Fact]
    public async Task ALocalReturnPath_WithAQuery_IsKept()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, _) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var choice = await dean.ChooseLanguageAsync(
            Landing,
            UiLanguageTestData.English,
            ct,
            returnPath: DeanAccountTestData.Paths.OwnPassword + "?from=header");

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal(DeanAccountTestData.Paths.OwnPassword + "?from=header", choice.Location);
    }

    /// <summary>FR-005, SC-2: the re-issued session cookie keeps the host's attributes and stays non-persistent.</summary>
    [Fact]
    public async Task TheReissuedCookie_KeepsTheSc2Attributes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, _) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var choice = await dean.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct);

        var header = choice.SetCookie(SignInTestData.SessionCookieName);
        Assert.NotNull(header);
        var cookie = SetCookieHeader.Parse(header!);
        Assert.True(cookie.Has("secure"), header);
        Assert.True(cookie.Has("httponly"), header);
        Assert.Equal("lax", cookie.Get("samesite"), ignoreCase: true);
        Assert.Equal("/", cookie.Get("path"));
        Assert.False(cookie.Has("expires"), header);
        Assert.False(cookie.Has("max-age"), header);
    }

    /// <summary>
    /// AC-009, I-2: the re-issued session still ends 8 hours after the ORIGINAL sign-in. A re-issue that restamped
    /// the sign-in time would keep this session alive past the limit.
    /// </summary>
    [Fact]
    public async Task TheReissuedSession_StillEndsEightHoursAfterTheOriginalSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, _) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        for (var step = 1; step <= 23; step++)
        {
            host.Time.Advance(TimeSpan.FromMinutes(20));
            if (step == 21)
            {
                var choice = await dean.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct);
                Assert.Equal(HttpStatusCode.Redirect, choice.Status);
                Assert.Equal(Landing, choice.Location);
            }
            else
            {
                var active = await dean.GetAsync(Landing, ct);
                Assert.True(active.Status == HttpStatusCode.OK, $"Expected 200 at {step * 20} minutes, got {(int)active.Status}.");
            }
        }

        host.Time.Advance(TimeSpan.FromMinutes(19));
        var justBefore = await dean.GetAsync(Landing, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));
        var past = await dean.GetAsync(Landing, ct);

        Assert.Equal(HttpStatusCode.OK, justBefore.Status);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(justBefore.Body));
        Assert.Equal(HttpStatusCode.Redirect, past.Status);
        Assert.Equal(SignInTestData.SignInPath, past.LocationPath);
    }

    /// <summary>
    /// AC-009, I-3: the choice does not rotate the security stamp — and the re-issued cookie still carries the
    /// current one, so signing out still kills a kept copy of it (US-008 AC-014).
    /// </summary>
    [Fact]
    public async Task TheReissuedCookie_StillDiesAtSignOut_AndTheStampIsNotRotated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, deanId) = await host.SignInDeanAsync(ct);
        using var _dean = dean;
        var stampBefore = await host.SecurityStampAsync(deanId, ct);

        var choice = await dean.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct);
        var stampAfterChoice = await host.SecurityStampAsync(deanId, ct);
        var kept = dean.Cookies;
        var alive = await dean.GetAsync(Landing, ct);
        await dean.PostFormAsync(SignInTestData.SignOutPath, [], ct);
        using var thief = host.CreateClient();
        thief.ReplaceCookies(kept);
        var replay = await thief.GetAsync(Landing, ct);

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal(stampBefore, stampAfterChoice);
        Assert.Equal(HttpStatusCode.OK, alive.Status);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(alive.Body));
        Assert.Equal(HttpStatusCode.Redirect, replay.Status);
        Assert.Equal(SignInTestData.SignInPath, replay.LocationPath);
    }

    /// <summary>AC-009: the re-issued session keeps the role — the Admin still reaches the Admin-only screen.</summary>
    [Fact]
    public async Task TheReissuedSession_KeepsTheRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _admin = admin;
        var (dean, _) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var adminChoice = await admin.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct);
        var deanChoice = await dean.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct);
        var adminScreen = await admin.GetAsync(DeanAccountTestData.Paths.Deans, ct);
        var deanScreen = await dean.GetAsync(DeanAccountTestData.Paths.Deans, ct);

        Assert.Equal(HttpStatusCode.Redirect, adminChoice.Status);
        Assert.Equal(HttpStatusCode.Redirect, deanChoice.Status);
        Assert.Equal(HttpStatusCode.OK, adminScreen.Status);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(adminScreen.Body));
        Assert.Equal(HttpStatusCode.Forbidden, deanScreen.Status);
    }

    /// <summary>AC-003 over HTTP: in read-only mode the Admin still chooses — never a 409.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheChoiceIsStored_NotRefused(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;
        using var _admin = admin;
        var adminId = (await host.AppUsersAsync(ct)).Single(u => u.Role == "admin").Id;

        var choice = await admin.ChooseLanguageAsync(Landing, UiLanguageTestData.English, ct);
        var page = await admin.GetAsync(Landing, ct);

        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(adminId, ct));
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(page.Body));
    }

    /// <summary>
    /// OD-007, FR-012: a Dean held at the forced password change sees the switcher there, may choose, and is still
    /// held at the form afterwards — now in the chosen language. Nothing else opens.
    /// </summary>
    [Fact]
    public async Task ADeanWithATemporaryPassword_ChoosesOnTheForcedChangePage_AndStaysHeldThere()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct, passwordIsTemporary: true);
        using var dean = host.CreateClient();
        var signIn = await dean.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);
        var deanId = (await host.AppUsersAsync(ct)).Single().Id;

        var form = await dean.GetAsync(DeanAccountTestData.Paths.ForcedChange, ct);
        var choice = await dean.ChooseLanguageAsync(
            DeanAccountTestData.Paths.ForcedChange,
            UiLanguageTestData.English,
            ct,
            returnPath: DeanAccountTestData.Paths.OwnPassword);
        var elsewhere = await dean.GetAsync(choice.LocationPath ?? Landing, ct);
        var formAfter = await dean.GetAsync(DeanAccountTestData.Paths.ForcedChange, ct);

        Assert.Equal(DeanAccountTestData.Paths.ForcedChange, signIn.LocationPath);
        Assert.True(UiLanguageTestData.HasSwitcher(form.Body));
        Assert.Equal(HttpStatusCode.Redirect, choice.Status);
        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(deanId, ct));
        Assert.Equal(HttpStatusCode.Redirect, elsewhere.Status);
        Assert.Equal(DeanAccountTestData.Paths.ForcedChange, elsewhere.LocationPath);
        Assert.Equal(HttpStatusCode.OK, formAfter.Status);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(formAfter.Body));
    }
}
