using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-010: only the signed-in Owner reaches the AllowedAdmin pages (FR-009, SC-4, TC-5).</summary>
public sealed class AllowedAdminAuthorizationTests(PostgreSqlFixture database)
{
    public static TheoryData<string, string> Operations => new()
    {
        { "GET", "/installations/{id}" },
        { "GET", "/installations/{id}/admins/new" },
        { "POST", "/installations/{id}/admins" },
        { "GET", "/installations/{id}/admins/{adminId}/revocation" },
        { "POST", "/installations/{id}/admins/{adminId}/revocation" },
    };

    public static TheoryData<string> Pages => new()
    {
        "/installations/{id}/admins/new",
        "/installations/{id}/admins/{adminId}/revocation",
    };

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Anonymous_WithOwnerAccount_RedirectsToSignIn_ChangesNothing(string method, string template)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        using var anonymous = host.CreateClient();

        var response = await anonymous.SendAsync(
            new HttpMethod(method),
            Path(template, installation, admin),
            method == "POST" ? new FormUrlEncodedContent(AllowedAdminTestData.AddFields(AllowedAdminTestData.OtherEmail)) : null,
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/sign-in", response.LocationPath);
        Assert.DoesNotContain(AllowedAdminTestData.Email, response.Body, StringComparison.Ordinal);
        Assert.Single(await host.AllowedAdminsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task BeforeSetup_RedirectsToSetup(string method, string template)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();
        var unknown = Guid.Parse(InstallationTestData.UnknownIdentifier);

        var response = await anonymous.SendAsync(
            new HttpMethod(method),
            Path(template, unknown, unknown),
            method == "POST" ? new FormUrlEncodedContent(AllowedAdminTestData.AddFields(AllowedAdminTestData.Email)) : null,
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/setup", response.LocationPath);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task SignedInOwner_Returns200(string template)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);

        var response = await owner.GetAsync(Path(template, installation, admin), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task PrincipalWithoutOwnerRole_Returns403_ChangesNothing(string method, string template)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        await owner.GetAsync("/installations/new", ct);
        var token = owner.LastToken;
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        using var forged = host.CreateClient();
        forged.ReplaceCookies(new Dictionary<string, string>(owner.Cookies)
        {
            [SetCookieHeader.SessionCookieName] = await host.SessionWithoutOwnerRoleAsync(owner),
        });

        var fields = AllowedAdminTestData.AddFields(AllowedAdminTestData.OtherEmail)
            .Append(new(Html.AntiforgeryFieldName, token ?? string.Empty));
        var response = await forged.SendAsync(
            new HttpMethod(method),
            Path(template, installation, admin),
            method == "POST" ? new FormUrlEncodedContent(fields) : null,
            ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Contains(host.Text("Error.Forbidden", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Single(await host.AllowedAdminsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task AllowedAdminEndpoints_ExistAndNoneAllowsAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services)
            .Where(e => e.Pattern.StartsWith("installations/{id}/admins", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(
            new[] { "installations/{id}/admins", "installations/{id}/admins/new", "installations/{id}/admins/{adminid}/revocation" },
            endpoints.Select(e => e.Pattern).Distinct().Order(StringComparer.Ordinal));
        Assert.DoesNotContain(endpoints, e => e.AllowsAnonymous);
        Assert.DoesNotContain(endpoints, e => e.Methods is null || e.Methods.Any(m => m is "PUT" or "PATCH" or "DELETE"));
    }

    private static string Path(string template, Guid installation, Guid admin) =>
        template
            .Replace("{id}", installation.ToString("D"), StringComparison.Ordinal)
            .Replace("{adminId}", admin.ToString("D"), StringComparison.Ordinal);
}
