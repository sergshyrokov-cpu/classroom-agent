using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-012 AC-012, AC-013: the forced change of a temporary password and the Dean's own password page
/// (api-design §2.5, §2.6). The forced change is reachable only by a session that has just passed step 5, and
/// that session may reach nothing else.
/// </summary>
public sealed class DeanPasswordPageTests(PostgreSqlFixture database)
{
    /// <summary>AC-012: after the forced change the Dean is signed in and lands on the landing page.</summary>
    [Fact]
    public async Task TheForcedChange_SignsTheDeanIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        using var client = host.CreateClient();
        await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);

        var page = await client.CompleteForcedChangeAsync(DeanAccountTestData.NewPassword, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.Landing, page.LocationPath);
    }

    /// <summary>
    /// AC-012: while the password is temporary the Dean may reach nothing but the change form — any other
    /// authenticated request is sent back to it (spec FR-006).
    /// </summary>
    [Fact]
    public async Task WhileThePasswordIsTemporary_EveryOtherPageSendsTheDeanBack()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        using var client = host.CreateClient();
        await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);

        var landing = await client.GetAsync(DeanAccountTestData.Paths.Landing, ct);

        Assert.Equal(HttpStatusCode.Redirect, landing.Status);
        Assert.Equal(DeanAccountTestData.Paths.ForcedChange, landing.LocationPath);
    }

    /// <summary>AC-012: the form is unreachable with no session at all (api-design §2.6).</summary>
    [Fact]
    public async Task WithNoSession_TheFormSendsTheVisitorToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(DeanAccountTestData.Paths.ForcedChange, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.SignIn, page.LocationPath);
    }

    /// <summary>
    /// AC-012: once the password is the Dean's own, the forced form is not a way back in — it sends them to
    /// the landing page instead, so it can never change a password without the old one having been proved.
    /// </summary>
    [Fact]
    public async Task WithAnOrdinarySession_TheFormSendsTheDeanAway()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        using var client = host.CreateClient();
        await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);
        await client.CompleteForcedChangeAsync(DeanAccountTestData.NewPassword, ct);

        var page = await client.GetAsync(DeanAccountTestData.Paths.ForcedChange, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.Landing, page.LocationPath);
    }

    /// <summary>AC-013: the Dean's own password page answers 200 to a Dean.</summary>
    [Fact]
    public async Task TheOwnPasswordPage_IsServedToADean()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = await SignedInDeanAsync(host, ct);

        var page = await client.GetAsync(DeanAccountTestData.Paths.OwnPassword, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
    }

    /// <summary>AC-013, S-07: and 403 to an Admin, who has no password at all (SC-2).</summary>
    [Fact]
    public async Task TheOwnPasswordPage_IsForbiddenToAnAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(DeanAccountTestData.Paths.OwnPassword, ct);

        Assert.Equal(HttpStatusCode.Forbidden, page.Status);
    }

    /// <summary>AC-013: a successful change redirects back to the page (Post-Redirect-Get).</summary>
    [Fact]
    public async Task AChange_RedirectsBackToThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = await SignedInDeanAsync(host, ct);

        var page = await client.ChangeOwnPasswordAsync(DeanAccountTestData.NewPassword, "a third good password", ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.OwnPassword, page.LocationPath);
    }

    /// <summary>AC-013: a wrong current password answers 400 and nothing submitted is echoed back (VR-006).</summary>
    [Fact]
    public async Task AWrongCurrentPassword_Answers400AndEchoesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = await SignedInDeanAsync(host, ct);

        var page = await client.ChangeOwnPasswordAsync(DeanAccountTestData.WrongPassword, "a third good password", ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.DoesNotContain(DeanAccountTestData.WrongPassword, page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("a third good password", page.Body, StringComparison.Ordinal);
    }

    /// <summary>A Dean signed in with a password of their own.</summary>
    private static async Task<FormClient> SignedInDeanAsync(InstallationTestHost host, CancellationToken cancellationToken)
    {
        await host.InsertDeanAsync(cancellationToken);
        var client = host.CreateClient();
        await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, cancellationToken);
        await client.CompleteForcedChangeAsync(DeanAccountTestData.NewPassword, cancellationToken);
        return client;
    }
}
