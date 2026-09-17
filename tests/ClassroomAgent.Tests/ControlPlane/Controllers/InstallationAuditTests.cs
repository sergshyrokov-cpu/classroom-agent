using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-008: one audit row per completed create, rename and client ID change; none for refusals (FR-009, SC-11).</summary>
public sealed class InstallationAuditTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Registration_WritesInstallationCreatedRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var ownerId = (await host.OwnerAsync(ct))!.Id;
        var rowsBefore = (await host.AuditRowsAsync(ct)).Count;
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var identifier = await host.RegisterInstallationAsync(owner, ct);

        var installation = await host.InstallationAsync(identifier, ct);
        var row = Assert.Single((await host.AuditRowsAsync(ct)).Skip(rowsBefore));
        AssertOwnerActedOn(host, row, ownerId, installation!.Id, "installation_created");
    }

    [Fact]
    public async Task Rename_WritesInstallationRenamedRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var ownerId = (await host.OwnerAsync(ct))!.Id;
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        var rowsBefore = (await host.AuditRowsAsync(ct)).Count;
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/name",
            $"/installations/{identifier:D}/name",
            InstallationTestData.NameFields(InstallationTestData.OtherName),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var installation = await host.InstallationAsync(identifier, ct);
        var row = Assert.Single((await host.AuditRowsAsync(ct)).Skip(rowsBefore));
        AssertOwnerActedOn(host, row, ownerId, installation!.Id, "installation_renamed");
    }

    [Fact]
    public async Task ClientIdChange_WritesInstallationClientIdChangedRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var ownerId = (await host.OwnerAsync(ct))!.Id;
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        var rowsBefore = (await host.AuditRowsAsync(ct)).Count;
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/client-id",
            $"/installations/{identifier:D}/client-id",
            InstallationTestData.ClientIdFields(InstallationTestData.OtherClientId),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var installation = await host.InstallationAsync(identifier, ct);
        var row = Assert.Single((await host.AuditRowsAsync(ct)).Skip(rowsBefore));
        AssertOwnerActedOn(host, row, ownerId, installation!.Id, "installation_client_id_changed");
    }

    [Fact]
    public async Task RefusedSubmissions_WriteNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        var rowsBefore = (await host.AuditRowsAsync(ct)).Count;
        var detail = $"/installations/{identifier:D}";

        var statuses = new List<HttpStatusCode>
        {
            (await owner.PostFromPageAsync("/installations/new", "/installations", InstallationTestData.RegisterFields(name: string.Empty), ct)).Status,
            (await owner.PostFromPageAsync("/installations/new", "/installations", InstallationTestData.RegisterFields(InstallationTestData.OtherName, InstallationTestData.Domain, InstallationTestData.OtherClientId), ct)).Status,
            (await owner.PostFromPageAsync(detail + "/name", detail + "/name", InstallationTestData.NameFields(" bad"), ct)).Status,
            (await owner.PostFromPageAsync(detail + "/name", detail + "/name", InstallationTestData.NameFields(InstallationTestData.Name), ct)).Status,
            (await owner.PostFromPageAsync(detail + "/client-id", detail + "/client-id", InstallationTestData.ClientIdFields("bad"), ct)).Status,
            (await owner.PostFromPageAsync(detail + "/client-id", detail + "/client-id", InstallationTestData.ClientIdFields(InstallationTestData.ClientId), ct)).Status,
            (await owner.PostFromPageAsync(detail + "/name", $"/installations/{InstallationTestData.UnknownIdentifier}/name", InstallationTestData.NameFields(InstallationTestData.OtherName), ct)).Status,
            (await owner.PostFormAsync("/installations", InstallationTestData.RegisterFields(InstallationTestData.OtherName, InstallationTestData.OtherDomain, InstallationTestData.OtherClientId), ct, withToken: false)).Status,
        };

        Assert.Equal(
            new[]
            {
                HttpStatusCode.BadRequest,
                HttpStatusCode.Conflict,
                HttpStatusCode.BadRequest,
                HttpStatusCode.Redirect,
                HttpStatusCode.BadRequest,
                HttpStatusCode.Redirect,
                HttpStatusCode.NotFound,
                HttpStatusCode.BadRequest,
            },
            statuses);
        Assert.Equal(rowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task AuditRows_CarryNoNameDomainClientIdOrIdentifier()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/name",
            $"/installations/{identifier:D}/name",
            InstallationTestData.NameFields(InstallationTestData.OtherName),
            ct);
        await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/client-id",
            $"/installations/{identifier:D}/client-id",
            InstallationTestData.ClientIdFields(InstallationTestData.OtherClientId),
            ct);

        var rows = await host.AuditRowsAsJsonAsync(ct);

        Assert.Contains(rows, r => r.Contains("installation_client_id_changed", StringComparison.Ordinal));
        var values = new[]
        {
            InstallationTestData.Name,
            InstallationTestData.OtherName,
            InstallationTestData.Domain,
            InstallationTestData.ClientId,
            InstallationTestData.OtherClientId,
            identifier.ToString("D"),
            identifier.ToString("N"),
        };
        foreach (var value in values)
        {
            Assert.All(rows, r => Assert.DoesNotContain(value, r, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AssertOwnerActedOn(ControlPlaneTestHost host, AuditRow row, long ownerId, long installationId, string action)
    {
        Assert.Equal("owner", row.ActorType);
        Assert.Equal(ownerId, row.ActorId);
        Assert.Equal(action, row.Action);
        Assert.Equal("installation", row.TargetType);
        Assert.Equal(installationId, row.TargetId);
        Assert.Equal("succeeded", row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.False(string.IsNullOrEmpty(row.RequestId));
        Assert.Equal(host.Time.GetUtcNow(), row.OccurredAt);
    }
}
