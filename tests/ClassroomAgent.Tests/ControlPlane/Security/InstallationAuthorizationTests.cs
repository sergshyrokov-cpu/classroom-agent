using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-009: only the signed-in Owner reaches the installation pages (FR-010, SC-4, TC-5).</summary>
public sealed class InstallationAuthorizationTests(PostgreSqlFixture database)
{
    public static TheoryData<string, string> Operations => new()
    {
        { "GET", "/installations" },
        { "GET", "/installations/new" },
        { "POST", "/installations" },
        { "GET", "/installations/{id}" },
        { "GET", "/installations/{id}/name" },
        { "POST", "/installations/{id}/name" },
        { "GET", "/installations/{id}/client-id" },
        { "POST", "/installations/{id}/client-id" },
    };

    public static TheoryData<string> Pages => new()
    {
        "/installations",
        "/installations/new",
        "/installations/{id}",
        "/installations/{id}/name",
        "/installations/{id}/client-id",
    };

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Anonymous_WithOwnerAccount_RedirectsToSignIn_ChangesNothing(string method, string template)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        var before = await host.InstallationsAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        using var anonymous = host.CreateClient();

        var response = await anonymous.SendAsync(
            new HttpMethod(method),
            template.Replace("{id}", identifier.ToString("D"), StringComparison.Ordinal),
            method == "POST"
                ? new FormUrlEncodedContent(InstallationTestData.RegisterFields(InstallationTestData.OtherName, InstallationTestData.OtherDomain, InstallationTestData.OtherClientId))
                : null,
            ct);

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
            template.Replace("{id}", InstallationTestData.UnknownIdentifier, StringComparison.Ordinal),
            method == "POST" ? new FormUrlEncodedContent(InstallationTestData.RegisterFields()) : null,
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
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await owner.GetAsync(template.Replace("{id}", identifier.ToString("D"), StringComparison.Ordinal), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task PrincipalWithoutOwnerRole_Returns403_ChangesNothing(string method, string template)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        var path = template.Replace("{id}", identifier.ToString("D"), StringComparison.Ordinal);
        await owner.GetAsync("/installations/new", ct);
        var token = owner.LastToken;
        var before = await host.InstallationsAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        using var forged = host.CreateClient();
        forged.ReplaceCookies(new Dictionary<string, string>(owner.Cookies)
        {
            [SetCookieHeader.SessionCookieName] = await host.SessionWithoutOwnerRoleAsync(owner),
        });

        var fields = InstallationTestData.RegisterFields(InstallationTestData.OtherName, InstallationTestData.OtherDomain, InstallationTestData.OtherClientId)
            .Append(new(Html.AntiforgeryFieldName, token ?? string.Empty));
        var response = await forged.SendAsync(
            new HttpMethod(method),
            path,
            method == "POST" ? new FormUrlEncodedContent(fields) : null,
            ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Contains(host.Text("Error.Forbidden", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.InstallationsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task InstallationEndpoints_ExistAndNoneAllowsAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        // US-003 adds installations/{id}/admins… routes; AllowedAdminAuthorizationTests covers them.
        // US-004 adds installations/{id}/suspension and …/resumption; InstallationStatusAuthorizationTests covers them.
        var installationEndpoints = HostEndpoint.All(host.Services)
            .Where(e => e.Pattern.StartsWith("installations", StringComparison.Ordinal)
                && !e.Pattern.StartsWith("installations/{id}/admins", StringComparison.Ordinal)
                && e.Pattern is not ("installations/{id}/suspension" or "installations/{id}/resumption"))
            .ToList();

        var patterns = installationEndpoints.Select(e => e.Pattern).Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            new[] { "installations", "installations/new", "installations/{id}", "installations/{id}/client-id", "installations/{id}/name" },
            patterns);
        Assert.DoesNotContain(installationEndpoints, e => e.AllowsAnonymous);
        Assert.DoesNotContain(
            installationEndpoints,
            e => e.Methods is null || e.Methods.Any(m => m is "PUT" or "PATCH" or "DELETE"));
    }
}
