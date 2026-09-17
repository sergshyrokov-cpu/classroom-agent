using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-001: the list of installations and the link from the home page (FR-001, FR-002, OD-003).</summary>
public sealed class InstallationListTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task List_NoInstallations_ShowsEmptyMessageAndRegisterLink()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync("/installations", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(host.Text("Installations.Empty", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Matches("href=\"(https://localhost)?/installations/new\"", response.Body);
    }

    [Fact]
    public async Task List_ShowsNameDomainStatusCreationTimeAndDetailLink()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var createdAt = new DateTimeOffset(2026, 3, 7, 21, 5, 42, TimeSpan.Zero);
        var identifier = await host.InsertInstallationAsync(ct, createdAt: createdAt);

        var response = await owner.GetAsync("/installations", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(InstallationTestData.Name, response.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.Domain, response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.Status.Active", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains("07.03.2026 21:05 UTC", response.Text, StringComparison.Ordinal);
        Assert.Contains($"/installations/{identifier:D}\"", response.Body, StringComparison.Ordinal);
        Assert.Matches("href=\"(https://localhost)?/installations/new\"", response.Body);
        Assert.DoesNotContain(host.Text("Installations.Empty", "uk"), response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_CreationTimeIsUtcNotConvertedToAnotherZone()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var createdAt = new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero);
        await host.InsertInstallationAsync(ct, createdAt: createdAt);

        var response = await owner.GetAsync("/installations", ct);

        Assert.Contains("31.12.2026 23:30 UTC", response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("01.01.2027", response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_SuspendedInstallation_ShowsSuspendedLabel()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.InsertInstallationAsync(ct, status: "suspended");

        var response = await owner.GetAsync("/installations", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(host.Text("Installation.Status.Suspended", "uk"), response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_OrdersByNameIgnoringCase_ThenByDomain()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.InsertInstallationAsync(ct, name: "beta", domain: "b-school.example.test", clientId: "3000000001");
        await host.InsertInstallationAsync(ct, name: "Alpha", domain: "z-school.example.test", clientId: "3000000002");
        await host.InsertInstallationAsync(ct, name: "alpha", domain: "a-school.example.test", clientId: "3000000003");
        await host.InsertInstallationAsync(ct, name: "Beta", domain: "a-beta.example.test", clientId: "3000000004");

        var response = await owner.GetAsync("/installations", ct);

        var positions = new[] { "a-school.example.test", "z-school.example.test", "a-beta.example.test", "b-school.example.test" }
            .Select(domain => response.Text.IndexOf(domain, StringComparison.Ordinal))
            .ToList();
        Assert.All(positions, position => Assert.True(position >= 0));
        Assert.Equal(positions.Order().ToList(), positions);
    }

    [Fact]
    public async Task Home_LinksToInstallations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Matches("href=\"(https://localhost)?/installations\"", response.Body);
        Assert.Contains(host.Text("Home.Installations", "uk"), response.Text, StringComparison.Ordinal);
    }
}
