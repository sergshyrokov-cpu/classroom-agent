using ClassroomAgent.ControlPlane.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClassroomAgent.Tests.ControlPlane.Persistence;

/// <summary>AC-008 and db-design §4: the <c>audit_event</c> table, its checks and its immutability.</summary>
public sealed class AuditEventSchemaTests(PostgreSqlFixture database)
{
    private const string InsertRow =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, action, target_type, target_id,
                                 outcome, refusal_category, request_id, created_at, updated_at)
        VALUES (now(), @actor_type, @actor_id, 'owner_sign_in', @target_type, @target_id,
                @outcome, @refusal_category, 'request', now(), now())
        """;

    [Fact]
    public async Task UpdateOfAuditRow_IsRejectedByDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        await InsertAsync(host, "anonymous", null, null, null, "refused", "unknown_login", ct);

        await Assert.ThrowsAsync<PostgresException>(
            () => host.ExecuteAsync("UPDATE audit_event SET request_id = 'changed'", ct));

        Assert.Equal("request", await host.ScalarAsync<string>("SELECT request_id FROM audit_event", ct));
    }

    [Fact]
    public async Task DeleteOfAuditRow_IsRejectedByDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        await InsertAsync(host, "anonymous", null, null, null, "refused", "unknown_login", ct);

        await Assert.ThrowsAsync<PostgresException>(() => host.ExecuteAsync("DELETE FROM audit_event", ct));

        Assert.Equal(1L, await host.ScalarAsync<long>("SELECT count(*) FROM audit_event", ct));
    }

    [Fact]
    public async Task ModifiedOrDeletedAuditEntity_SaveChangesThrows_NothingSaved()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        long id;
        using (var scope = host.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
            var auditEvent = AuditEvent.OwnerSignInRefusedUnknownLogin(host.Time.GetUtcNow(), "request");
            context.Add(auditEvent);
            await context.SaveChangesAsync(ct);
            id = auditEvent.Id;
        }

        using (var scope = host.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
            var stored = await context.Set<AuditEvent>().SingleAsync(e => e.Id == id, ct);
            context.Entry(stored).State = EntityState.Modified;
            await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync(ct));
        }

        using (var scope = host.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
            var stored = await context.Set<AuditEvent>().SingleAsync(e => e.Id == id, ct);
            context.Remove(stored);
            await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync(ct));
        }

        var rows = await host.AuditRowsAsync(ct);
        var row = Assert.Single(rows);
        Assert.Equal(id, row.Id);
        Assert.Equal(row.CreatedAt, row.UpdatedAt);
    }

    [Theory]
    [InlineData("admin", 5, null, null, "refused", "unknown_login", "ck_audit_event_actor_type")]
    [InlineData("anonymous", 5, null, null, "refused", "unknown_login", "ck_audit_event_actor")]
    [InlineData("owner", null, null, null, "refused", "wrong_password", "ck_audit_event_actor")]
    [InlineData("owner", 5, "owner", null, "refused", "wrong_password", "ck_audit_event_target")]
    [InlineData("owner", 5, null, 5, "refused", "wrong_password", "ck_audit_event_target")]
    [InlineData("owner", 5, "owner", 5, "failed", null, "ck_audit_event_outcome")]
    [InlineData("owner", 5, "owner", 5, "refused", null, "ck_audit_event_refusal_category")]
    [InlineData("owner", 5, "owner", 5, "succeeded", "wrong_password", "ck_audit_event_refusal_category")]
    public async Task CheckConstraints_RejectInconsistentRows(
        string actorType,
        int? actorId,
        string? targetType,
        int? targetId,
        string outcome,
        string? refusalCategory,
        string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => InsertAsync(host, actorType, actorId, targetType, targetId, outcome, refusalCategory, ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task ConsistentRows_AreAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        await InsertAsync(host, "owner", 5, "owner", 5, "succeeded", null, ct);
        await InsertAsync(host, "owner", 5, "owner", 5, "refused", "locked_out", ct);
        await InsertAsync(host, "anonymous", null, null, null, "refused", "wrong_setup_code", ct);

        Assert.Equal(3L, await host.ScalarAsync<long>("SELECT count(*) FROM audit_event", ct));
    }

    [Fact]
    public async Task AuditEventColumns_MatchDesign_NoPersonalDataColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var columns = await SchemaQueries.ColumnsAsync(host, "audit_event", ct);

        Assert.Equal(
            new[]
            {
                "action character varying 64 NO -",
                "actor_id bigint - YES -",
                "actor_type character varying 16 NO -",
                "created_at timestamp with time zone - NO -",
                "id bigint - NO -",
                "occurred_at timestamp with time zone - NO -",
                "outcome character varying 16 NO -",
                "refusal_category character varying 32 YES -",
                "request_id character varying 128 YES -",
                "target_id bigint - YES -",
                "target_type character varying 32 YES -",
                "updated_at timestamp with time zone - NO -",
            },
            columns);
        Assert.Equal("id", await SchemaQueries.PrimaryKeyAsync(host, "audit_event", "pk_audit_event", ct));
        Assert.Equal(
            0L,
            await host.ScalarAsync<long>(
                "SELECT count(*) FROM information_schema.table_constraints WHERE table_name = 'audit_event' AND constraint_type = 'FOREIGN KEY'",
                ct));
    }

    private static Task<int> InsertAsync(
        ControlPlaneTestHost host,
        string actorType,
        int? actorId,
        string? targetType,
        int? targetId,
        string outcome,
        string? refusalCategory,
        CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            InsertRow,
            cancellationToken,
            ("actor_type", actorType),
            ("actor_id", (long?)actorId),
            ("target_type", targetType),
            ("target_id", (long?)targetId),
            ("outcome", outcome),
            ("refusal_category", refusalCategory));
}
