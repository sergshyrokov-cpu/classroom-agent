using System.Net;
using System.Security.Claims;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-010: the home page admits the Owner and refuses a principal without the role (SC-4, TC-5).</summary>
public sealed class AuthorizationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Home_SignedInOwner_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Fact]
    public async Task Home_PrincipalWithoutOwnerRole_Returns403ErrorPage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var forbidden = host.Text("Error.Forbidden", "uk");

        var sessionValue = Assert.Contains(SetCookieHeader.SessionCookieName, owner.Cookies);
        Assert.False(sessionValue.StartsWith("chunks-", StringComparison.Ordinal), "The session cookie is expected in one piece.");
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
        var forged = format.Protect(new AuthenticationTicket(new ClaimsPrincipal(withoutRole), ticket.Properties, ticket.AuthenticationScheme));

        using var client = host.CreateClient();
        client.ReplaceCookies(new Dictionary<string, string> { [SetCookieHeader.SessionCookieName] = forged });
        var response = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Null(response.Location);
        Assert.Contains(forbidden, response.Text, StringComparison.Ordinal);
    }
}
