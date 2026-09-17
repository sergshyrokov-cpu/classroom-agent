using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-009: one audit row per completed addition and revocation; none for refusals; no email in any row (FR-007, SC-11).</summary>
public sealed class AllowedAdminAuditTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Add_WritesAllowedAdminAddedRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var ownerId = (await host.OwnerAsync(ct))!.Id;
        var installation = await host.InsertInstallationAsync(ct);
        var rowsBefore = (await host.AuditRowsAsync(ct)).Count;
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var admin = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);

        var row = Assert.Single((await host.AuditRowsAsync(ct)).Skip(rowsBefore));
        AssertOwnerActedOn(host, row, ownerId, admin.Id, "allowed_admin_added");
    }

    [Fact]
    public async Task Revoke_WritesAllowedAdminRevokedRow_WithIdOfDeletedEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var ownerId = (await host.OwnerAsync(ct))!.Id;
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);
        var rowsBefore = (await host.AuditRowsAsync(ct)).Count;
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await owner.RevokeAllowedAdminAsync(installation, admin.Identifier, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = Assert.Single((await host.AuditRowsAsync(ct)).Skip(rowsBefore));
        AssertOwnerActedOn(host, row, ownerId, admin.Id, "allowed_admin_revoked");
        Assert.Empty(await host.AllowedAdminsAsync(ct));
    }

    [Fact]
    public async Task RefusedOrNoOpRequests_WriteNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);
        var rowsBefore = (await host.AuditRowsAsync(ct)).Count;
        var formPath = AllowedAdminTestData.AddFormPath(installation);
        var addPath = AllowedAdminTestData.AddPath(installation);
        var revocation = AllowedAdminTestData.RevocationPath(installation, admin.Identifier);
        var unknown = Guid.Parse(InstallationTestData.UnknownIdentifier);

        var statuses = new List<HttpStatusCode>
        {
            (await owner.PostFromPageAsync(formPath, addPath, AllowedAdminTestData.AddFields("bad+email@" + InstallationTestData.Domain), ct)).Status,
            (await owner.PostFromPageAsync(formPath, addPath, AllowedAdminTestData.AddFields("ivan@gmail.com"), ct)).Status,
            (await owner.PostFromPageAsync(formPath, addPath, AllowedAdminTestData.AddFields(AllowedAdminTestData.Email), ct)).Status,
            (await owner.PostFromPageAsync(formPath, AllowedAdminTestData.AddPath(unknown), AllowedAdminTestData.AddFields(AllowedAdminTestData.OtherEmail), ct)).Status,
            (await owner.GetAsync(revocation, ct)).Status,
            (await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct)).Status,
            (await owner.PostFromPageAsync(revocation, AllowedAdminTestData.RevocationPath(installation, unknown), [], ct)).Status,
            (await owner.PostFormAsync(addPath, AllowedAdminTestData.AddFields(AllowedAdminTestData.OtherEmail), ct, withToken: false)).Status,
            (await owner.PostFormAsync(revocation, [], ct, withToken: false)).Status,
        };

        Assert.Equal(
            new[]
            {
                HttpStatusCode.BadRequest,
                HttpStatusCode.BadRequest,
                HttpStatusCode.Conflict,
                HttpStatusCode.NotFound,
                HttpStatusCode.OK,
                HttpStatusCode.OK,
                HttpStatusCode.NotFound,
                HttpStatusCode.BadRequest,
                HttpStatusCode.BadRequest,
            },
            statuses);
        Assert.Equal(rowsBefore, (await host.AuditRowsAsync(ct)).Count);
        Assert.Single(await host.AllowedAdminsAsync(ct));
    }

    [Fact]
    public async Task AuditRows_CarryNoEmailInstallationValuesOrIdentifiers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);
        await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.OtherEmail, ct);
        await owner.RevokeAllowedAdminAsync(installation, admin.Identifier, ct);

        var rows = await host.AuditRowsAsJsonAsync(ct);

        Assert.Contains(rows, r => r.Contains("allowed_admin_revoked", StringComparison.Ordinal));
        var values = new[]
        {
            AllowedAdminTestData.Email,
            AllowedAdminTestData.OtherEmail,
            "ivan.petrenko",
            "olena.koval",
            InstallationTestData.Name,
            InstallationTestData.Domain,
            installation.ToString("D"),
            admin.Identifier.ToString("D"),
            admin.Identifier.ToString("N"),
        };
        foreach (var value in values)
        {
            Assert.All(rows, r => Assert.DoesNotContain(value, r, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task AuditRowOfRevokedEntry_CannotBeUpdatedOrDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);
        await owner.RevokeAllowedAdminAsync(installation, admin.Identifier, ct);
        var row = (await host.AuditRowsAsync(ct)).Last();

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => host.ExecuteAsync(
            "UPDATE audit_event SET target_id = 0 WHERE id = @id", ct, ("id", row.Id)));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => host.ExecuteAsync(
            "DELETE FROM audit_event WHERE id = @id", ct, ("id", row.Id)));

        Assert.Equal(row, (await host.AuditRowsAsync(ct)).Last());
    }

    private static void AssertOwnerActedOn(ControlPlaneTestHost host, AuditRow row, long ownerId, long allowedAdminId, string action)
    {
        Assert.Equal("owner", row.ActorType);
        Assert.Equal(ownerId, row.ActorId);
        Assert.Equal(action, row.Action);
        Assert.Equal("allowed_admin", row.TargetType);
        Assert.Equal(allowedAdminId, row.TargetId);
        Assert.Equal("succeeded", row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.False(string.IsNullOrEmpty(row.RequestId));
        Assert.Equal(host.Time.GetUtcNow(), row.OccurredAt);
    }
}
