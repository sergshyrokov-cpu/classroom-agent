using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-012 AC-010, AC-011, AC-012: the Dean's sign-in over HTTP (api-design §2.3). Every outcome of the sequence
/// answers 302 — the outcome lives in the <c>Location</c> and in TempData, never in the status code — so steps
/// 1, 2 and 3 are indistinguishable to the caller (spec S-05, I-6).
/// </summary>
public sealed class DeanSignInPageTests(PostgreSqlFixture database)
{
    /// <summary>AC-010: an unknown login redirects back to the sign-in page.</summary>
    [Fact]
    public async Task AnUnknownLogin_RedirectsBackToTheSignInPage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.SignInAsDeanAsync("nobody@school-one.example.test", DeanAccountTestData.WrongPassword, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.SignIn, page.LocationPath);
    }

    /// <summary>
    /// AC-010, S-05: an unknown login, a wrong password and a locked-out account answer **identically** — same
    /// status, same <c>Location</c>. A difference here is the oracle SC-2 forbids.
    /// </summary>
    [Fact]
    public async Task TheThreeCommonRefusals_AreIndistinguishable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        using var unknown = host.CreateClient();
        using var wrong = host.CreateClient();
        using var locked = host.CreateClient();

        var unknownAnswer = await unknown.SignInAsDeanAsync("nobody@school-one.example.test", DeanAccountTestData.WrongPassword, ct);
        var wrongAnswer = await wrong.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.WrongPassword, ct);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await locked.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.WrongPassword, ct);
        }

        var lockedAnswer = await locked.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);

        // The three must be a real answer, not three identical 404s from a route that does not exist yet:
        // without this line the test would pass vacuously before the implementation (red-phase check).
        Assert.Equal(HttpStatusCode.Redirect, unknownAnswer.Status);
        Assert.Equal(DeanAccountTestData.Paths.SignIn, unknownAnswer.LocationPath);
        Assert.Equal(unknownAnswer.Status, wrongAnswer.Status);
        Assert.Equal(unknownAnswer.Status, lockedAnswer.Status);
        Assert.Equal(unknownAnswer.LocationPath, wrongAnswer.LocationPath);
        Assert.Equal(unknownAnswer.LocationPath, lockedAnswer.LocationPath);
    }

    /// <summary>AC-012: a correct temporary password sends the Dean to the forced change form (api-design §2.6).</summary>
    [Fact]
    public async Task ATemporaryPassword_SendsTheDeanToTheForcedChange()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        using var client = host.CreateClient();

        var page = await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.ForcedChange, page.LocationPath);
    }

    /// <summary>AC-010: once the password is the Dean's own, a sign-in lands on the landing page.</summary>
    [Fact]
    public async Task ACorrectPassword_SignsTheDeanIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        using var first = host.CreateClient();
        await first.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);
        await first.CompleteForcedChangeAsync(DeanAccountTestData.NewPassword, ct);
        using var client = host.CreateClient();

        var page = await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.NewPassword, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.Landing, page.LocationPath);
    }

    /// <summary>
    /// AC-010, VR-004: the anonymous sign-in form carries the antiforgery token, and a POST without it is
    /// refused before any account is looked up (SC-4 login CSRF, API-7).
    /// </summary>
    [Fact]
    public async Task ASignInWithoutAnAntiforgeryToken_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        using var client = host.CreateClient();

        var page = await client.SignInAsDeanAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            ct,
            withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        var dean = Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "dean");
        Assert.Equal(0, dean.AccessFailedCount);
    }

    /// <summary>AC-010: the sign-in page stays anonymous and answers 200 to a visitor with no session (SC-4).</summary>
    [Fact]
    public async Task TheSignInPage_StaysAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(DeanAccountTestData.Paths.SignIn, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
    }

    /// <summary>
    /// AC-013: the whole sign-in path works in read-only mode — sign-in bookkeeping is on the BR-026 closed
    /// list of permitted service writes (spec FR-015).
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task TheSignIn_WorksInReadOnlyMode(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);
        await host.InsertDeanAsync(ct);
        using var client = host.CreateClient();

        var page = await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.ForcedChange, page.LocationPath);
    }

    /// <summary>S-10: no response of the sign-in path ever carries the password that was typed.</summary>
    [Fact]
    public async Task NoResponse_CarriesTheTypedPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        using var client = host.CreateClient();

        var refused = await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.WrongPassword, ct);
        var page = await client.GetAsync(DeanAccountTestData.Paths.SignIn, ct);

        Assert.DoesNotContain(DeanAccountTestData.WrongPassword, refused.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(DeanAccountTestData.WrongPassword, page.Body, StringComparison.Ordinal);
    }
}
