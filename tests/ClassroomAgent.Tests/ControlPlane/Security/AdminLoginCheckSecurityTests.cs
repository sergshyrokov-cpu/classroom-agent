using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>
/// US-008 AC-006: the Admin login check is a declared member of the SC-4 anonymous and antiforgery-exemption
/// lists — the existing entry "Legitimacy check and Admin login check" already names it — and no other
/// Control Plane endpoint becomes anonymous or exempt because of this Story (spec FR-009, S-01; TC-5).
/// </summary>
public sealed class AdminLoginCheckSecurityTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task TheLoginCheck_IsAnonymousPostOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var endpoint = Assert.Single(
            HostEndpoint.All(host.Services),
            e => e.Pattern == "service/v1/admin-login-checks");

        Assert.True(endpoint.AllowsAnonymous, endpoint.ToString());
        Assert.Equal(["POST"], endpoint.Methods);
    }

    [Fact]
    public async Task TheLoginCheck_IsExemptFromAntiforgery()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var endpoint = Assert.Single(
            HostEndpoint.All(host.Services),
            e => e.Pattern == "service/v1/admin-login-checks");

        Assert.True(endpoint.IsExemptFromAntiforgery, endpoint.ToString());
    }

    /// <summary>The closed lists grow by exactly one endpoint: the two service-channel POSTs and nothing more.</summary>
    [Fact]
    public async Task OnlyTheTwoServiceChannelPosts_AreAnonymousAndExempt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var exempt = HostEndpoint.All(host.Services)
            .Where(e => !e.IsFallback && !e.IsStaticFile && e.UnsafeMethodsAccepted.Count > 0 && e.IsExemptFromAntiforgery)
            .Select(e => e.Pattern)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "service/v1/admin-login-checks", "service/v1/legitimacy-checks" }, exempt);
    }

    /// <summary>No Owner page changes in this Story: the Control Plane's own pages stay authenticated.</summary>
    [Fact]
    public async Task OwnerPages_StayAuthenticated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services);

        Assert.Contains(endpoints, e => e.Pattern == string.Empty && !e.AllowsAnonymous);
        Assert.Contains(endpoints, e => e.Pattern == "installations" && !e.AllowsAnonymous);
        Assert.Contains(endpoints, e => e.Pattern == "sign-out" && !e.AllowsAnonymous);
    }
}
