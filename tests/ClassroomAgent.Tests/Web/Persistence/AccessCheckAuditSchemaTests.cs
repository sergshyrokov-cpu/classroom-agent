using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Web.Persistence;

/// <summary>
/// US-011 db-design §3.1, §7.1: the audit table's closed lists grow by exactly two codes — the action
/// <c>access_check_run</c> and the refusal category <c>connection_not_usable</c> — through one amending migration,
/// against real PostgreSQL (PC-2, TC-2). Unknown codes are still rejected.
/// </summary>
public sealed class AccessCheckAuditSchemaTests(PostgreSqlFixture database)
{
    private const string Insert =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, created_at, updated_at)
        VALUES (@stamp, 'app_user', (SELECT id FROM app_user LIMIT 1), 'admin', @action, 'workspace_connection', @targetId,
                @outcome, @category, 'r-1', @stamp, @stamp)
        """;

    [Fact]
    public async Task TheAuditTable_AcceptsASucceededRun()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertAppUserAsync(ct);

        var written = await host.ExecuteAsync(
            Insert,
            ct,
            ("stamp", host.Time.GetUtcNow()),
            ("action", AccessCheckTestData.Audit.Action),
            ("targetId", 1L),
            ("outcome", AccessCheckTestData.Audit.Succeeded),
            ("category", DBNull.Value));

        Assert.Equal(1, written);
    }

    [Theory]
    [InlineData(AccessCheckTestData.Audit.ConnectionNotUsable)]
    [InlineData(AccessCheckTestData.Audit.ReadOnlyMode)]
    public async Task TheAuditTable_AcceptsARefusedRun(string category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertAppUserAsync(ct);

        var written = await host.ExecuteAsync(
            Insert,
            ct,
            ("stamp", host.Time.GetUtcNow()),
            ("action", AccessCheckTestData.Audit.Action),
            ("targetId", DBNull.Value),
            ("outcome", AccessCheckTestData.Audit.Refused),
            ("category", category));

        Assert.Equal(1, written);
    }

    [Fact]
    public async Task AnUnknownAction_IsStillRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertAppUserAsync(ct);

        var insert = async () => await host.ExecuteAsync(
            Insert,
            ct,
            ("stamp", host.Time.GetUtcNow()),
            ("action", "access_check_result_stored"),
            ("targetId", 1L),
            ("outcome", AccessCheckTestData.Audit.Succeeded),
            ("category", DBNull.Value));

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal("ck_audit_event_action", error.ConstraintName);
    }

    [Fact]
    public async Task AnUnknownRefusalCategory_IsStillRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertAppUserAsync(ct);

        var insert = async () => await host.ExecuteAsync(
            Insert,
            ct,
            ("stamp", host.Time.GetUtcNow()),
            ("action", AccessCheckTestData.Audit.Action),
            ("targetId", DBNull.Value),
            ("outcome", AccessCheckTestData.Audit.Refused),
            ("category", "scope_not_authorized"));

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal("ck_audit_event_refusal_category_value", error.ConstraintName);
    }

    /// <summary>
    /// db-design §1, §4: no table is added — nothing about a check is stored (OD-003). The expected table set
    /// gained <c>sync_state</c> with US-013, which is the Story that adds it — US-011 still adds none.
    /// </summary>
    [Fact]
    public async Task NoTableIsAdded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(
            new[] { "__EFMigrationsHistory", "app_user", "audit_event", "legitimacy_state", "sync_state", "workspace_connection" },
            (await host.TableNamesAsync(ct)).Order(StringComparer.Ordinal));
    }

    /// <summary>db-design §3: no column is added to audit_event.</summary>
    [Fact]
    public async Task TheAuditTable_GainsNoColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await host.QueryAsync(
            "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'audit_event' ORDER BY column_name",
            r => r.GetString(0),
            ct);

        Assert.Equal(
            new[]
            {
                "action", "actor_id", "actor_role", "actor_type", "created_at", "id", "occurred_at", "outcome",
                "refusal_category", "request_id", "target_id", "target_type", "updated_at",
            },
            columns);
    }
}
