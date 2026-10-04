using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-019 AC-006: the press is open to an Admin and a Dean signed in, to nobody else; it needs the antiforgery token,
/// a GET requests nothing, and the user cannot choose where they are sent back to (openapi; spec FR-007; S-01, S-02,
/// S-10; TC-5).
/// </summary>
public sealed class SynchronizationRequestAuthorizationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Anonymous_IsSentToSignIn_AndNothingHappens()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        // No page of the Story is reachable anonymously; the sign-in page supplies a token, so the refusal under
        // test is the authorization one, not the antiforgery one.
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var response = await client.PostFormAsync(SynchronizationRequestTestData.Path, [], ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        Assert.Equal(0, requests.Calls);
        Assert.Empty(await host.SynchronizationRequestAuditRowsAsync(ct));
    }

    [Fact]
    public async Task ADeanWithATemporaryPassword_IsSentToTheForcedChange_AndNothingHappens()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.DeanReturnPage, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/sign-in/change-password", response.LocationPath);
        Assert.Equal(0, requests.Calls);
        Assert.Empty(await host.SynchronizationRequestAuditRowsAsync(ct));
    }

    [Theory]
    [InlineData(Actor.Admin)]
    [InlineData(Actor.Dean)]
    public async Task WithoutAnAntiforgeryToken_TheRequestIs400_AndNothingHappens(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, actor, ct);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(PageOf(actor), ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(0, requests.Calls);
        Assert.Empty(await host.SynchronizationRequestAuditRowsAsync(ct));
    }

    [Fact]
    public async Task AGet_RequestsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var response = await client.GetAsync(SynchronizationRequestTestData.Path, ct);

        Assert.NotEqual(HttpStatusCode.Redirect, response.Status);
        Assert.NotEqual(HttpStatusCode.OK, response.Status);
        Assert.Equal(0, requests.Calls);
        Assert.Empty(await host.SynchronizationRequestAuditRowsAsync(ct));
    }

    [Theory]
    [InlineData(Actor.Admin)]
    [InlineData(Actor.Dean)]
    public async Task ASubmittedReturnUrl_IsIgnored(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, actor, ct);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(
            PageOf(actor),
            ct,
            extraFields: [new("returnUrl", "https://evil.example/")]);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(PageOf(actor), response.LocationPath);
        Assert.DoesNotContain("evil.example", response.Location, StringComparison.OrdinalIgnoreCase);
    }

    private static string PageOf(Actor actor) =>
        actor == Actor.Admin
            ? SynchronizationRequestTestData.AdminReturnPage
            : SynchronizationRequestTestData.DeanReturnPage;
}
