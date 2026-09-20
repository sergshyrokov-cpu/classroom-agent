using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-008 AC-008: a revoked Admin is refused with a message that reveals nothing, keeps their <c>AppUser</c>
/// row untouched, and does not move the time of the last successful sign-in (spec FR-010, FR-011, FR-012;
/// S-05, S-14; BR-012, PC-11).
/// </summary>
public sealed class RevokedAdminTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler NotAllowed() =>
        ScriptedHttpHandler.Json(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(false));

    [Fact]
    public async Task RevokedAdmin_IsRefused_AndKeepsTheirRowUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = NotAllowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.InsertAppUserAsync(ct, lastSuccessfulSignInAt: InstallationTestHost.DefaultStart - TimeSpan.FromDays(3));
        var before = Assert.Single(await host.AppUsersAsync(ct));
        host.Time.Advance(TimeSpan.FromHours(5));

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        Assert.Equal(SignInTestData.SignInPath, callback.LocationPath);
        Assert.Null(callback.SetCookie(SignInTestData.SessionCookieName));
        var after = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal(before, after);
    }

    /// <summary>AC-008: the row is never deleted — the audit trail and the history need it.</summary>
    [Fact]
    public async Task RevokedAdmin_RowIsNotDeleted_EvenAfterSeveralAttempts()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = NotAllowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.InsertAppUserAsync(ct, lastSuccessfulSignInAt: InstallationTestHost.DefaultStart);

        await host.SignInWithGoogleAsync(ct);
        await host.SignInWithGoogleAsync(ct);
        await host.SignInWithGoogleAsync(ct);

        Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal(3, channel.Requests.Count);
    }

    /// <summary>AC-008, AC-010: the refusal names the existing account as the actor, never the email entered.</summary>
    [Fact]
    public async Task RevokedAdmin_IsAuditedAgainstTheirExistingAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = NotAllowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.InsertAppUserAsync(ct);
        var user = Assert.Single(await host.AppUsersAsync(ct));

        await host.SignInWithGoogleAsync(ct);

        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("app_user", audit.ActorType);
        Assert.Equal(user.Id, audit.ActorId);
        Assert.Equal("admin", audit.ActorRole);
        Assert.Equal("refused", audit.Outcome);
        Assert.Equal(SignInTestData.RefusalCategories.NotInAllowedAdmin, audit.RefusalCategory);
    }

    /// <summary>AC-008, AC-010: an unapproved stranger with no account is audited anonymously, without an identifier.</summary>
    [Fact]
    public async Task AnUnapprovedStrangerWithNoAccount_IsAuditedAnonymously()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = NotAllowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.Google.Email = SignInTestData.UnapprovedEmail;

        await host.SignInWithGoogleAsync(ct);

        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("anonymous", audit.ActorType);
        Assert.Null(audit.ActorId);
        Assert.Null(audit.ActorRole);
        Assert.Equal(SignInTestData.RefusalCategories.NotInAllowedAdmin, audit.RefusalCategory);
        Assert.Empty(await host.AppUsersAsync(ct));
    }

    /// <summary>AC-008, S-14: a stranger cannot learn from the refusal whether the school knows the address.</summary>
    [Fact]
    public async Task TheRefusalIsIdentical_ForAKnownRevokedAdminAndAnUnknownStranger()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = NotAllowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.InsertAppUserAsync(ct);

        var (revokedClient, revoked) = await host.SignInWithGoogleAsync(ct);
        var revokedPage = await revokedClient.GetAsync(SignInTestData.SignInPath, ct);
        host.Google.Email = SignInTestData.UnapprovedEmail;
        var (strangerClient, stranger) = await host.SignInWithGoogleAsync(ct);
        var strangerPage = await strangerClient.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Equal(revoked.Status, stranger.Status);
        Assert.Equal(revoked.LocationPath, stranger.LocationPath);
        Assert.Equal(
            Html.WithoutProtectedTokens(revokedPage.Body),
            Html.WithoutProtectedTokens(strangerPage.Body));
        Assert.Contains(
            host.Text(SignInTestData.TextKeys.RefusedNotApproved, "uk"),
            revokedPage.Text,
            StringComparison.Ordinal);
    }

    /// <summary>AC-008, api-design 2.2: the refusal travels in TempData, never as a query parameter anyone could craft.</summary>
    [Fact]
    public async Task TheRefusal_IsNotCarriedInTheAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = NotAllowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        var (_, callback) = await host.SignInWithGoogleAsync(ct);

        Assert.Equal(SignInTestData.SignInPath, callback.LocationPath);
        Assert.DoesNotContain("?", callback.LocationPath!, StringComparison.Ordinal);
    }

    /// <summary>AC-008, api-design 2.2: a crafted query parameter renders no message of the visitor's choosing.</summary>
    [Fact]
    public async Task ACraftedQueryParameter_RendersNoMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = NotAllowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        using var client = host.CreateClient();
        const string injected = "Your account has been transferred, call this number";

        var page = await client.GetAsync(
            SignInTestData.SignInPath + "?refusal=" + Uri.EscapeDataString(injected),
            ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain(injected, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-008, AC-018: the refusal is logged as a category with the account id, never the email.</summary>
    [Fact]
    public async Task TheRefusal_IsLoggedAsAWarningWithoutTheEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = NotAllowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.InsertAppUserAsync(ct);

        await host.SignInWithGoogleAsync(ct);
        var events = await host.ReadLogEventsAsync(ct);

        var log = string.Join("\n", events.Select(e => e.Line));
        Assert.DoesNotContain(SignInTestData.AdminEmail, log, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(events, e => e.Level == "Warning");
    }
}
