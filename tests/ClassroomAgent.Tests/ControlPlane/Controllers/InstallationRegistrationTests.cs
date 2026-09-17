using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-002: registering an installation (FR-003, FR-004).</summary>
public sealed class InstallationRegistrationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task RegistrationForm_HasNameDomainClientIdAndToken_NoStatusField()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync("/installations/new", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(string.Empty, Html.InputValue(response.Body, "name"));
        Assert.Equal(string.Empty, Html.InputValue(response.Body, "domain"));
        Assert.Equal(string.Empty, Html.InputValue(response.Body, "clientId"));
        Assert.True(Html.HasInput(response.Body, Html.AntiforgeryFieldName));
        Assert.DoesNotContain("name=\"status\"", response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("<form[^>]*action=\"(https://localhost)?/installations\"", response.Body);
    }

    [Fact]
    public async Task ValidRegistration_RedirectsToDetailPageOfNewInstallation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.PostFromPageAsync(
            "/installations/new",
            "/installations",
            InstallationTestData.RegisterFields(),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var identifier = InstallationHostExtensions.IdentifierFromLocation(response);
        var detail = await owner.GetAsync($"/installations/{identifier:D}", ct);
        Assert.Equal(HttpStatusCode.OK, detail.Status);
        var row = Assert.Single(await host.InstallationsAsync(ct));
        Assert.Equal(identifier, row.Identifier);
    }

    [Fact]
    public async Task ValidRegistration_StoresActiveInstallationWithSubmittedValuesAndUtcCreationTime()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        host.Time.Advance(TimeSpan.FromMinutes(3));
        var now = host.Time.GetUtcNow();

        var identifier = await host.RegisterInstallationAsync(owner, ct);

        var row = await host.InstallationAsync(identifier, ct);
        Assert.NotNull(row);
        Assert.Equal(InstallationTestData.Name, row.Name);
        Assert.Equal(InstallationTestData.Domain, row.Domain);
        Assert.Equal(InstallationTestData.ClientId, row.ClientId);
        Assert.Equal("active", row.Status);
        Assert.Equal(now, row.CreatedAt);
        Assert.Equal(TimeSpan.Zero, row.CreatedAt.Offset);
    }

    [Fact]
    public async Task ValidRegistration_MixedCaseDomain_IsStoredInLowerCase()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var identifier = await host.RegisterInstallationAsync(owner, ct, domain: "School-One.Example.TEST");

        var row = await host.InstallationAsync(identifier, ct);
        Assert.NotNull(row);
        Assert.Equal("school-one.example.test", row.Domain);
    }

    [Fact]
    public async Task ValidRegistration_GeneratesDistinctVersion4Identifiers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var first = await host.RegisterInstallationAsync(owner, ct);
        var second = await host.RegisterInstallationAsync(
            owner,
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);

        Assert.NotEqual(first, second);
        Assert.Equal(4, first.Version);
        Assert.Equal(4, second.Version);
        Assert.NotEqual(Guid.Empty, first);
    }

    [Fact]
    public async Task PostedStatusIdentifierAndCreationTime_AreIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var fields = InstallationTestData.RegisterFields()
            .Append(new("status", "suspended"))
            .Append(new("identifier", InstallationTestData.UnknownIdentifier))
            .Append(new("id", "999"))
            .Append(new("createdAt", "2001-01-01T00:00:00Z"))
            .ToList();

        var response = await owner.PostFromPageAsync("/installations/new", "/installations", fields, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = Assert.Single(await host.InstallationsAsync(ct));
        Assert.Equal("active", row.Status);
        Assert.NotEqual(Guid.Parse(InstallationTestData.UnknownIdentifier), row.Identifier);
        Assert.NotEqual(999, row.Id);
        Assert.Equal(host.Time.GetUtcNow(), row.CreatedAt);
    }
}
