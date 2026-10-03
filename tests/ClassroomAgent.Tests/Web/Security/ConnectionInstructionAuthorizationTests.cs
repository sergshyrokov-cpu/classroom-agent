using System.Net;
using System.Security.Claims;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-010 AC-001: the instruction belongs to the Admin alone — the permission-matrix row "Просмотр инструкции
/// по подключению" is ✔ Admin, ✘ Dean (<c>trebovaniya.md</c> §2, v39; spec FR-011, S-01, S-02; TC-5). The policy
/// is its **own**, not US-009's: §2 split the two rows because one is a write read-only mode blocks and the
/// other a read it permits (spec I-7).
/// </summary>
public sealed class ConnectionInstructionAuthorizationTests(PostgreSqlFixture database)
{
    private static ClaimsPrincipal PrincipalOf(string role) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "1")],
            CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>AC-001: the policy exists and admits an Admin.</summary>
    [Fact]
    public async Task ThePolicy_AdmitsAnAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            PrincipalOf("Admin"),
            resource: null,
            ConnectionInstructionTestData.Policy);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// AC-001: a Dean is refused by the policy itself, not by a hidden link. The Dean principal is synthetic
    /// because no Dean can sign in until US-012 — the limitation US-009 finding F-1 records, carried forward.
    /// </summary>
    [Fact]
    public async Task ThePolicy_RefusesADean()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            PrincipalOf("Dean"),
            resource: null,
            ConnectionInstructionTestData.Policy);

        Assert.False(result.Succeeded);
    }

    /// <summary>AC-001: an anonymous principal is refused as well.</summary>
    [Fact]
    public async Task ThePolicy_RefusesAnAnonymousPrincipal()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource: null,
            ConnectionInstructionTestData.Policy);

        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// AC-001, spec I-7: this policy is separate from US-009's. Both exist, and the instruction's endpoint
    /// declares its own — a single merged policy for the two matrix rows would fail here.
    /// </summary>
    [Fact]
    public async Task ThePolicy_IsSeparateFromTheConnectionSettingsPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var policies = host.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var instruction = await policies.GetPolicyAsync(ConnectionInstructionTestData.Policy);
        var settings = await policies.GetPolicyAsync(WorkspaceConnectionTestData.Policy);

        Assert.NotNull(instruction);
        Assert.NotNull(settings);
        Assert.NotEqual(ConnectionInstructionTestData.Policy, WorkspaceConnectionTestData.Policy);
    }

    /// <summary>AC-001: the endpoint declares a policy rather than relying on the fallback (API-9).</summary>
    [Fact]
    public async Task TheEndpoint_DeclaresAPolicyAndAllowsNoAnonymousAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services)
            .Where(e => e.Pattern == ConnectionInstructionTestData.Path.Trim('/'))
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, e => Assert.False(e.AllowsAnonymous, e.ToString()));
    }

    /// <summary>
    /// AC-001, spec FR-007: the path is a <c>GET</c> and nothing else. No endpoint on it accepts a
    /// state-changing method, so this Story adds no antiforgery concern at all.
    /// </summary>
    [Fact]
    public async Task TheEndpoint_AcceptsOnlyGet()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services)
            .Where(e => e.Pattern == ConnectionInstructionTestData.Path.Trim('/'))
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, e => Assert.NotNull(e.Methods));
        Assert.All(endpoints, e => Assert.Empty(e.UnsafeMethodsAccepted));
        Assert.All(endpoints, e => Assert.Contains("GET", e.Methods!, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>AC-001, S-01: the SC-4 closed list of anonymous endpoints gains nothing.</summary>
    [Fact]
    public async Task TheAnonymousList_GainsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services).ToList();
        var anonymous = endpoints
            .Where(e => e.AllowsAnonymous && !e.IsStaticFile && !e.IsFallback)
            .Select(e => e.Pattern)
            .ToList();

        // The endpoint must exist: "not on the anonymous list" is also true of an endpoint nobody wrote.
        Assert.Contains(ConnectionInstructionTestData.Path.Trim('/'), endpoints.Select(e => e.Pattern));
        Assert.DoesNotContain(ConnectionInstructionTestData.Path.Trim('/'), anonymous);
    }

    /// <summary>AC-001: an anonymous visitor is sent to sign in and never sees the instruction.</summary>
    [Fact]
    public async Task AnAnonymousVisitor_IsSentToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(SignInTestData.SignInPath, page.LocationPath);
    }

    /// <summary>AC-001: and sees none of its content — not the client ID, not the domain.</summary>
    [Fact]
    public async Task AnAnonymousVisitor_SeesNoPartOfTheInstruction()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, InstallationTestHost.DefaultStart - TimeSpan.FromHours(1));
        using var client = host.CreateClient();

        var page = await client.OpenInstructionAsync(ct);

        // The challenge, not a 404: otherwise the absences below would hold for a page that does not exist.
        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(SignInTestData.SignInPath, page.LocationPath);
        Assert.DoesNotContain(ConnectionInstructionTestData.ClientId, page.Body, StringComparison.Ordinal);
        Assert.All(
            ConnectionInstructionTestData.Scopes,
            scope => Assert.DoesNotContain(scope, page.Body, StringComparison.Ordinal));
    }

    /// <summary>AC-001: a signed-in Admin reaches the instruction.</summary>
    [Fact]
    public async Task ASignedInAdmin_ReachesTheInstruction()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
    }

    /// <summary>
    /// AC-001, api-design §5: the route answers no <c>409</c>. Read-only mode is a field of the response here,
    /// not a refusal — BR-026 names the instruction as viewable, so all three causes answer <c>200</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheAnswerIsNeverAConflict(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.NotEqual(HttpStatusCode.Conflict, page.Status);
        Assert.Equal(HttpStatusCode.OK, page.Status);
    }

    /// <summary>
    /// AC-007, spec VR-001: the operation takes no input. A query string is ignored — it changes neither the
    /// answer nor anything stored.
    /// </summary>
    [Fact]
    public async Task AQueryStringIsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        var before = await host.TableRowCountsAsync(ct);

        var plain = await client.OpenInstructionAsync(ct);
        var withQuery = await client.GetAsync(
            ConnectionInstructionTestData.Path + "?clientId=999&domain=attacker.example.test",
            ct);

        Assert.Equal(HttpStatusCode.OK, withQuery.Status);
        Assert.Contains(ConnectionInstructionTestData.ClientId, withQuery.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "attacker.example.test",
            WebUtility.HtmlDecode(UiLanguageTestData.WithoutSwitcher(withQuery.Body)),
            StringComparison.Ordinal);
        Assert.DoesNotContain("999", ConnectionInstructionHostExtensions.HandedOverText(withQuery));
        Assert.Equal(
            ConnectionInstructionHostExtensions.HandedOverText(plain),
            ConnectionInstructionHostExtensions.HandedOverText(withQuery));
        Assert.Equal(before, await host.TableRowCountsAsync(ct));
    }
}
