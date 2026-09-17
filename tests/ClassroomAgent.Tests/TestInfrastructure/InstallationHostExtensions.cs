using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>US-002 helpers over a started Control Plane: registering over HTTP and reading <c>installation</c> rows.</summary>
public static partial class InstallationHostExtensions
{
    private const string SelectInstallation =
        "SELECT id, identifier, name, domain, client_id, status, created_at, updated_at FROM installation";

    /// <summary>Registers an installation through the form and returns the UUID from the redirect.</summary>
    public static async Task<Guid> RegisterInstallationAsync(
        this ControlPlaneTestHost host,
        FormClient owner,
        CancellationToken cancellationToken,
        string name = InstallationTestData.Name,
        string domain = InstallationTestData.Domain,
        string clientId = InstallationTestData.ClientId)
    {
        var form = await owner.GetAsync("/installations/new", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, form.Status);
        var response = await owner.PostFormAsync(
            "/installations",
            InstallationTestData.RegisterFields(name, domain, clientId),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        return IdentifierFromLocation(response);
    }

    /// <summary>The UUID of a <c>/installations/{id}</c> redirect; fails on any other target.</summary>
    public static Guid IdentifierFromLocation(PageResponse response)
    {
        Assert.NotNull(response.LocationPath);
        var match = DetailPath().Match(response.LocationPath);
        Assert.True(match.Success, $"Expected a redirect to /installations/{{uuid}}, got '{response.LocationPath}'.");
        return Guid.Parse(match.Groups[1].Value);
    }

    /// <summary>Opens a page first so the client holds a fresh antiforgery token, then posts.</summary>
    public static async Task<PageResponse> PostFromPageAsync(
        this FormClient client,
        string pagePath,
        string postPath,
        IEnumerable<KeyValuePair<string, string>> fields,
        CancellationToken cancellationToken)
    {
        await client.GetAsync(pagePath, cancellationToken);
        return await client.PostFormAsync(postPath, fields, cancellationToken);
    }

    public static Task<IReadOnlyList<InstallationRow>> InstallationsAsync(
        this ControlPlaneTestHost host,
        CancellationToken cancellationToken) =>
        host.QueryAsync(SelectInstallation + " ORDER BY id", Map, cancellationToken);

    public static async Task<InstallationRow?> InstallationAsync(
        this ControlPlaneTestHost host,
        Guid identifier,
        CancellationToken cancellationToken)
    {
        var rows = await host.QueryAsync(
            SelectInstallation + " WHERE identifier = @identifier",
            Map,
            cancellationToken,
            ("identifier", identifier));
        return rows.SingleOrDefault();
    }

    /// <summary>Inserts a row directly — for list and uniqueness scenarios that need a given status or time.</summary>
    public static async Task<Guid> InsertInstallationAsync(
        this ControlPlaneTestHost host,
        CancellationToken cancellationToken,
        string name = InstallationTestData.Name,
        string domain = InstallationTestData.Domain,
        string clientId = InstallationTestData.ClientId,
        string status = "active",
        DateTimeOffset? createdAt = null)
    {
        var identifier = Guid.NewGuid();
        var time = createdAt ?? host.Time.GetUtcNow();
        await host.ExecuteAsync(
            """
            INSERT INTO installation (identifier, name, domain, client_id, status, created_at, updated_at)
            VALUES (@identifier, @name, @domain, @clientId, @status, @createdAt, @createdAt)
            """,
            cancellationToken,
            ("identifier", identifier),
            ("name", name),
            ("domain", domain),
            ("clientId", clientId),
            ("status", status),
            ("createdAt", time));
        return identifier;
    }

    public static Task<int> SetInstallationStatusAsync(
        this ControlPlaneTestHost host,
        Guid identifier,
        string status,
        CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            "UPDATE installation SET status = @status WHERE identifier = @identifier",
            cancellationToken,
            ("status", status),
            ("identifier", identifier));

    /// <summary>A session cookie for the signed-in Owner with the <c>Owner</c> role removed (TC-5 forbidden case).</summary>
    public static async Task<string> SessionWithoutOwnerRoleAsync(this ControlPlaneTestHost host, FormClient owner)
    {
        var sessionValue = Assert.Contains(SetCookieHeader.SessionCookieName, owner.Cookies);
        var scheme = await host.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync();
        Assert.NotNull(scheme);
        var format = host.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme.Name).TicketDataFormat;
        var ticket = format.Unprotect(sessionValue);
        Assert.NotNull(ticket);

        var identity = (ClaimsIdentity)ticket.Principal.Identity!;
        var withoutRole = new ClaimsIdentity(
            identity.Claims.Where(c => c.Type != identity.RoleClaimType && c.Type != ClaimTypes.Role),
            identity.AuthenticationType,
            identity.NameClaimType,
            identity.RoleClaimType);
        return format.Protect(new AuthenticationTicket(new ClaimsPrincipal(withoutRole), ticket.Properties, ticket.AuthenticationScheme));
    }

    private static InstallationRow Map(Npgsql.NpgsqlDataReader r) =>
        new(
            r.GetInt64(0),
            r.GetGuid(1),
            r.GetString(2),
            r.GetString(3),
            r.GetString(4),
            r.GetString(5),
            r.GetFieldValue<DateTimeOffset>(6),
            r.GetFieldValue<DateTimeOffset>(7));

    [GeneratedRegex("^/installations/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$")]
    private static partial Regex DetailPath();
}
