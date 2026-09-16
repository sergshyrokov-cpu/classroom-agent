using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-011: global antiforgery on every state-changing endpoint, anonymous forms included (FR-015, TC-5).</summary>
public sealed class AntiforgeryTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task EveryNonGetEndpoint_WithoutToken_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var pageExpired = host.Text("Error.PageExpired", "uk");

        var stateChanging = HostEndpoint.All(host.Services)
            .Where(e => !e.IsFallback && !e.IsStaticFile && e.UnsafeMethodsAccepted.Count > 0)
            .ToList();
        Assert.Contains(stateChanging, e => e.Pattern == "setup");
        Assert.Contains(stateChanging, e => e.Pattern == "sign-in");
        Assert.Contains(stateChanging, e => e.Pattern == "sign-out");

        var failures = new List<string>();
        foreach (var endpoint in stateChanging)
        {
            foreach (var method in endpoint.UnsafeMethodsAccepted)
            {
                using var anonymous = host.CreateClient();
                var client = endpoint.AllowsAnonymous ? anonymous : owner;
                var response = await client.SendAsync(
                    new HttpMethod(method),
                    endpoint.SamplePath,
                    new FormUrlEncodedContent(TestData.SignInFields()),
                    ct);
                if (response.Status != HttpStatusCode.BadRequest || !response.Text.Contains(pageExpired, StringComparison.Ordinal))
                {
                    failures.Add($"{method} {endpoint.SamplePath} → {(int)response.Status}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task SetupWithoutToken_CreatesNoOwner_ShowsPageExpired()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        await client.GetAsync("/setup", ct);

        var response = await client.PostFormAsync("/setup", TestData.SetupFields(), ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Error.PageExpired", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Null(response.SetCookie(SetCookieHeader.SessionCookieName));
        Assert.DoesNotContain(TestData.Login, response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await host.OwnerCountAsync(ct));
        Assert.Empty(await host.AuditRowsAsync(ct));
    }

    [Fact]
    public async Task SignInWithoutToken_DoesNotSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        using var client = host.CreateClient();
        await client.GetAsync("/sign-in", ct);

        var response = await client.PostFormAsync("/sign-in", TestData.SignInFields(), ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Error.PageExpired", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Null(response.SetCookie(SetCookieHeader.SessionCookieName));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/", ct)).Status);
    }

    [Fact]
    public async Task NoEndpointIsExemptFromAntiforgery()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services);

        Assert.Contains(endpoints, e => e.Pattern == "sign-out");
        Assert.Empty(endpoints.Where(e => e.IsExemptFromAntiforgery).Select(e => e.ToString()));
    }
}
