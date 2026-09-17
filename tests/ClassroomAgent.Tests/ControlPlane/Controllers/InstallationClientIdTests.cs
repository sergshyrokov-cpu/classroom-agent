using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-007: changing the service-account client ID (FR-007, spec I-7, I-9).</summary>
public sealed class InstallationClientIdTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ClientIdForm_IsPrefilled_AndExplainsTheChange()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);

        var response = await owner.GetAsync($"/installations/{identifier:D}/client-id", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(InstallationTestData.ClientId, Html.InputValue(response.Body, "clientId"));
        Assert.True(Html.HasInput(response.Body, Html.AntiforgeryFieldName));
        Assert.Contains(host.Text("Installation.ClientId.ChangeNote", "uk"), response.Text, StringComparison.Ordinal);
        Assert.False(Html.HasInput(response.Body, "name"));
        Assert.False(Html.HasInput(response.Body, "domain"));
    }

    [Fact]
    public async Task ValidChange_ChangesOnlyClientId_RedirectsToDetail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        var before = await host.InstallationAsync(identifier, ct);
        host.Time.Advance(TimeSpan.FromMinutes(5));

        var response = await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/client-id",
            $"/installations/{identifier:D}/client-id",
            InstallationTestData.ClientIdFields("0" + InstallationTestData.OtherClientId),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal($"/installations/{identifier:D}", response.LocationPath);
        var after = await host.InstallationAsync(identifier, ct);
        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal("0" + InstallationTestData.OtherClientId, after.ClientId);
        Assert.Equal(before with { ClientId = after.ClientId, UpdatedAt = after.UpdatedAt }, after);
        Assert.Equal(host.Time.GetUtcNow(), after.UpdatedAt);
    }

    [Fact]
    public async Task UnchangedClientId_RedirectsToDetail_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        var before = await host.InstallationAsync(identifier, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        host.Time.Advance(TimeSpan.FromMinutes(5));

        var response = await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/client-id",
            $"/installations/{identifier:D}/client-id",
            InstallationTestData.ClientIdFields(InstallationTestData.ClientId),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal($"/installations/{identifier:D}", response.LocationPath);
        Assert.Equal(before, await host.InstallationAsync(identifier, ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task ChangeClientId_SuspendedInstallation_IsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct, status: "suspended");

        var response = await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/client-id",
            $"/installations/{identifier:D}/client-id",
            InstallationTestData.ClientIdFields(InstallationTestData.OtherClientId),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = await host.InstallationAsync(identifier, ct);
        Assert.Equal(InstallationTestData.OtherClientId, row?.ClientId);
        Assert.Equal("suspended", row?.Status);
    }
}
