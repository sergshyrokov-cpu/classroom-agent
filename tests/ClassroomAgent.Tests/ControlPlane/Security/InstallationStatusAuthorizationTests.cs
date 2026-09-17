using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-009: only the signed-in Owner reaches the suspend and resume pages (FR-008, SC-4, TC-5).</summary>
public sealed class InstallationStatusAuthorizationTests(PostgreSqlFixture database)
{
    public static TheoryData<string, string> Operations => new()
    {
        { "GET", "/installations/{id}/suspension" },
        { "POST", "/installations/{id}/suspension" },
        { "GET", "/installations/{id}/resumption" },
        { "POST", "/installations/{id}/resumption" },
    };

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Anonymous_WithOwnerAccount_RedirectsToSignIn_ChangesNothing(string method, string template)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var active = await host.InsertInstallationAsync(ct);
        var before = await host.InstallationsAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        using var anonymous = host.CreateClient();

        var response = await anonymous.SendAsync(new HttpMethod(method), Path(template, active), Body(method), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/sign-in", response.LocationPath);
        Assert.DoesNotContain(InstallationTestData.Domain, response.Body, StringComparison.Ordinal);
        Assert.Equal(before, await host.InstallationsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task BeforeSetup_RedirectsToSetup(string method, string template)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.SendAsync(
            new HttpMethod(method),
            Path(template, Guid.Parse(InstallationTestData.UnknownIdentifier)),
            Body(method),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/setup", response.LocationPath);
    }

    [Theory]
    [InlineData("active", "/installations/{id}/suspension")]
    [InlineData("suspended", "/installations/{id}/resumption")]
    public async Task SignedInOwner_ConfirmationReturns200(string status, string template)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: status);

        var response = await owner.GetAsync(Path(template, installation), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task PrincipalWithoutOwnerRole_Returns403_ChangesNothing(string method, string template)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var active = await host.InsertInstallationAsync(ct);
        await owner.LoadTokenAsync(ct);
        var token = owner.LastToken;
        var before = await host.InstallationsAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        using var forged = host.CreateClient();
        forged.ReplaceCookies(new Dictionary<string, string>(owner.Cookies)
        {
            [SetCookieHeader.SessionCookieName] = await host.SessionWithoutOwnerRoleAsync(owner),
        });

        var response = await forged.SendAsync(
            new HttpMethod(method),
            Path(template, active),
            method == "POST" ? new FormUrlEncodedContent([new(Html.AntiforgeryFieldName, token ?? string.Empty)]) : null,
            ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Contains(host.Text("Error.Forbidden", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.InstallationsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task StatusEndpoints_ExistAndNoneAllowsAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services)
            .Where(e => e.Pattern is "installations/{id}/suspension" or "installations/{id}/resumption")
            .ToList();

        Assert.Equal(
            new[] { "installations/{id}/resumption", "installations/{id}/suspension" },
            endpoints.Select(e => e.Pattern).Distinct().Order(StringComparer.Ordinal));
        Assert.DoesNotContain(endpoints, e => e.AllowsAnonymous);
        Assert.DoesNotContain(endpoints, e => e.Methods is null || e.Methods.Any(m => m is not ("GET" or "POST" or "HEAD")));
        Assert.Equal(
            new[] { "GET", "POST" },
            endpoints.SelectMany(e => e.Methods!).Where(m => m != "HEAD").Distinct().Order(StringComparer.Ordinal));
    }

    private static FormUrlEncodedContent? Body(string method) =>
        method == "POST" ? new FormUrlEncodedContent([]) : null;

    private static string Path(string template, Guid installation) =>
        template.Replace("{id}", installation.ToString("D"), StringComparison.Ordinal);
}
