using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-008: one audit row per status change; none for GET, refusals or unchanged; no name or domain in rows or logs (FR-006, FR-012, SC-10, SC-11).</summary>
public sealed class InstallationStatusAuditTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Suspend_WritesInstallationSuspendedRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var ownerId = (await host.OwnerAsync(ct))!.Id;
        var installation = await host.InsertInstallationAsync(ct);
        var installationId = (await host.InstallationAsync(installation, ct))!.Id;
        var rowsBefore = (await host.AuditRowsAsync(ct)).Count;
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await owner.SuspendInstallationAsync(installation, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = Assert.Single((await host.AuditRowsAsync(ct)).Skip(rowsBefore));
        AssertOwnerActedOn(host, row, ownerId, installationId, "installation_suspended");
    }

    [Fact]
    public async Task Resume_WritesInstallationResumedRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var ownerId = (await host.OwnerAsync(ct))!.Id;
        var installation = await host.InsertInstallationAsync(ct, status: "suspended");
        var installationId = (await host.InstallationAsync(installation, ct))!.Id;
        var rowsBefore = (await host.AuditRowsAsync(ct)).Count;
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await owner.ResumeInstallationAsync(installation, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = Assert.Single((await host.AuditRowsAsync(ct)).Skip(rowsBefore));
        AssertOwnerActedOn(host, row, ownerId, installationId, "installation_resumed");
    }

    [Fact]
    public async Task GetCancelRefusedAndUnchangedRequests_WriteNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var active = await host.InsertInstallationAsync(ct);
        var suspended = await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId,
            status: "suspended");
        var unknown = Guid.Parse(InstallationTestData.UnknownIdentifier);
        var rowsBefore = (await host.AuditRowsAsync(ct)).Count;
        var before = await host.InstallationsAsync(ct);

        var statuses = new List<HttpStatusCode>
        {
            (await owner.GetAsync(InstallationStatusTestData.SuspensionPath(active), ct)).Status,
            (await owner.GetAsync(InstallationStatusTestData.ResumptionPath(suspended), ct)).Status,
            (await owner.GetAsync(InstallationStatusTestData.DetailPath(active), ct)).Status,
            (await owner.SuspendInstallationAsync(suspended, ct)).Status,
            (await owner.ResumeInstallationAsync(active, ct)).Status,
            (await owner.SuspendInstallationAsync(unknown, ct)).Status,
            (await owner.ResumeInstallationAsync(unknown, ct)).Status,
            (await owner.PostFormAsync(InstallationStatusTestData.SuspensionPath(active), [], ct, withToken: false)).Status,
            (await owner.PostFormAsync(InstallationStatusTestData.ResumptionPath(suspended), [], ct, withToken: false)).Status,
        };

        Assert.Equal(
            new[]
            {
                HttpStatusCode.OK,
                HttpStatusCode.OK,
                HttpStatusCode.OK,
                HttpStatusCode.Redirect,
                HttpStatusCode.Redirect,
                HttpStatusCode.NotFound,
                HttpStatusCode.NotFound,
                HttpStatusCode.BadRequest,
                HttpStatusCode.BadRequest,
            },
            statuses);
        Assert.Equal(rowsBefore, (await host.AuditRowsAsync(ct)).Count);
        Assert.Equal(before, await host.InstallationsAsync(ct));
    }

    [Fact]
    public async Task AuditRows_CarryNoNameDomainOrIdentifier()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await owner.SuspendInstallationAsync(installation, ct);
        await owner.ResumeInstallationAsync(installation, ct);

        var rows = await host.AuditRowsAsJsonAsync(ct);

        Assert.Contains(rows, r => r.Contains("installation_suspended", StringComparison.Ordinal));
        Assert.Contains(rows, r => r.Contains("installation_resumed", StringComparison.Ordinal));
        foreach (var value in new[] { InstallationTestData.Name, InstallationTestData.Domain, installation.ToString("D"), installation.ToString("N") })
        {
            Assert.All(rows, r => Assert.DoesNotContain(value, r, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task StatusChangeAuditRow_CannotBeUpdatedOrDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await owner.SuspendInstallationAsync(installation, ct);
        var row = (await host.AuditRowsAsync(ct)).Last();
        Assert.Equal("installation_suspended", row.Action);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => host.ExecuteAsync(
            "UPDATE audit_event SET action = 'installation_resumed' WHERE id = @id", ct, ("id", row.Id)));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => host.ExecuteAsync(
            "DELETE FROM audit_event WHERE id = @id", ct, ("id", row.Id)));

        Assert.Equal(row, (await host.AuditRowsAsync(ct)).Last());
    }

    [Fact]
    public async Task InstallationNameAndDomain_NeverReachTheLogFile()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        const string name = "Log Probe Lyceum";
        const string domain = "log-probe.example.test";
        using (var owner = await host.CreateOwnerAsync(ct))
        {
            var installation = await host.InsertInstallationAsync(ct, name: name, domain: domain);
            await owner.GetAsync(InstallationStatusTestData.SuspensionPath(installation), ct);
            await owner.SuspendInstallationAsync(installation, ct);
            await owner.SuspendInstallationAsync(installation, ct);
            await owner.GetAsync(InstallationStatusTestData.ResumptionPath(installation), ct);
            await owner.ResumeInstallationAsync(installation, ct);
            await owner.ResumeInstallationAsync(installation, ct);
        }

        var logs = await host.ReadLogFilesAsync(ct);

        Assert.Contains(logs, content => content.Length > 0);
        foreach (var value in new[] { name, domain })
        {
            Assert.All(logs, content => Assert.DoesNotContain(value, content, StringComparison.OrdinalIgnoreCase));
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
