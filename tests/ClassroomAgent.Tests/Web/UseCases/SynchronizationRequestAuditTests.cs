using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-019 AC-001, AC-002, AC-004: every accepted press is audited without personal data, with the pressing user as
/// actor and no target (db-design §2.2; spec FR-008; S-11). Two presses are two rows.
/// </summary>
public sealed class SynchronizationRequestAuditTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AnAcceptedAdminPress_IsAuditedWithTheAdminAsActor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var admin = Assert.Single(await host.AppUsersAsync(ct));

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = Assert.Single(await host.SynchronizationRequestAuditRowsAsync(ct));
        Assert.Equal(SynchronizationRequestTestData.Audit.ActorTypeAppUser, row.ActorType);
        Assert.Equal(admin.Id, row.ActorId);
        Assert.Equal(SynchronizationRequestTestData.Audit.RoleAdmin, row.ActorRole);
        Assert.Equal(SynchronizationRequestTestData.Audit.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
        Assert.False(string.IsNullOrWhiteSpace(row.RequestId));
    }

    [Fact]
    public async Task AnAcceptedDeanPress_IsAuditedWithTheDeanAsActor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var dean = Assert.Single(
            await host.AppUsersAsync(ct),
            u => string.Equals(u.Role, SynchronizationRequestTestData.Audit.RoleDean, StringComparison.OrdinalIgnoreCase));

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.DeanReturnPage, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = Assert.Single(await host.SynchronizationRequestAuditRowsAsync(ct));
        Assert.Equal(SynchronizationRequestTestData.Audit.ActorTypeAppUser, row.ActorType);
        Assert.Equal(dean.Id, row.ActorId);
        Assert.Equal(SynchronizationRequestTestData.Audit.RoleDean, row.ActorRole);
        Assert.Equal(SynchronizationRequestTestData.Audit.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
    }

    /// <summary>AC-004: a second press is not collapsed away — it is answered and audited like the first.</summary>
    [Fact]
    public async Task TwoPresses_AreEachAnsweredAndAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var first = await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);
        var second = await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);

        Assert.Equal(HttpStatusCode.Redirect, first.Status);
        Assert.Equal(HttpStatusCode.Redirect, second.Status);
        Assert.Equal(2, requests.Calls);
        var rows = await host.SynchronizationRequestAuditRowsAsync(ct);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(SynchronizationRequestTestData.Audit.Succeeded, r.Outcome));
    }
}
