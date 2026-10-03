using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-037 db-design §2 … §5, §7 and §9, against the migrated database (TC-2): the five count columns are required on a
/// purge row and forbidden on every other, never negative; the purge row's actor, target, outcome and request id are
/// fixed; the two new indexes exist; and every foreign key the purge deletes across stays <c>Restrict</c>.
/// </summary>
public sealed class RetentionPurgeSchemaTests(PostgreSqlFixture database)
{
    private const string InsertPurgeRow =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, purged_courses, purged_leaver_memberships,
                                 purged_participants, purged_accounts, purged_audit_rows, created_at, updated_at)
        VALUES (now(), @actorType, @actorId, @actorRole, @action, @targetType, @targetId, @outcome, NULL, @requestId,
                @courses, @leavers, @participants, @accounts, @auditRows, now(), now())
        """;

    [Fact]
    public async Task AValidPurgeRow_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, ct);

        var row = Assert.Single(await host.PurgeAuditRowsAsync(ct));
        Assert.Equal((1, 2, 3, 4, 5), (row.Courses, row.LeaverMemberships, row.Participants, row.Accounts, row.AuditRows));
    }

    /// <summary>A run that removed nothing is a valid row: zero is not "absent" (VR-003, AC-008).</summary>
    [Fact]
    public async Task APurgeRowOfZeros_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, ct, courses: 0, leavers: 0, participants: 0, accounts: 0, auditRows: 0);

        Assert.Single(await host.PurgeAuditRowsAsync(ct));
    }

    [Theory]
    [InlineData("courses")]
    [InlineData("leavers")]
    [InlineData("participants")]
    [InlineData("accounts")]
    [InlineData("auditRows")]
    public async Task APurgeRowMissingACount_IsRejected(string missing)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            courses: missing == "courses" ? null : 1,
            leavers: missing == "leavers" ? null : 1,
            participants: missing == "participants" ? null : 1,
            accounts: missing == "accounts" ? null : 1,
            auditRows: missing == "auditRows" ? null : 1));

        Assert.Equal(RetentionPurgeTestData.Names.PurgeCounts, failure.ConstraintName);
    }

    [Theory]
    [InlineData("courses")]
    [InlineData("auditRows")]
    public async Task AnotherActionCarryingACount_IsRejected(string present)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            actorType: "app_user",
            actorId: 1,
            actorRole: "admin",
            action: "workspace_connection_saved",
            targetType: "workspace_connection",
            targetId: 1,
            requestId: "request-id",
            courses: present == "courses" ? 1 : null,
            leavers: null,
            participants: null,
            accounts: null,
            auditRows: present == "auditRows" ? 1 : null));

        Assert.True(
            failure.ConstraintName is RetentionPurgeTestData.Names.PurgeCounts or RetentionPurgeTestData.Names.PurgeCountsAbsent,
            $"Rejected by {failure.ConstraintName}.");
    }

    [Theory]
    [InlineData("courses")]
    [InlineData("leavers")]
    [InlineData("participants")]
    [InlineData("accounts")]
    [InlineData("auditRows")]
    public async Task ANegativeCount_IsRejected(string negative)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            courses: negative == "courses" ? -1 : 0,
            leavers: negative == "leavers" ? -1 : 0,
            participants: negative == "participants" ? -1 : 0,
            accounts: negative == "accounts" ? -1 : 0,
            auditRows: negative == "auditRows" ? -1 : 0));

        Assert.Equal(RetentionPurgeTestData.Names.PurgeCountsNonNegative, failure.ConstraintName);
    }

    /// <summary>db-design §2.4: the purge row is <c>system</c>, has no target, succeeded, and carries no request id.</summary>
    [Theory]
    [InlineData("anonymous", null, null, null, null, "succeeded", null)]
    [InlineData("system", null, null, "app_user", 1L, "succeeded", null)]
    [InlineData("system", null, null, "app_user", null, "succeeded", null)]
    [InlineData("system", null, null, null, null, "succeeded", "request-id")]
    public async Task APurgeRowWithAnotherShape_IsRejected(
        string actorType,
        long? actorId,
        string? actorRole,
        string? targetType,
        long? targetId,
        string outcome,
        string? requestId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            actorType: actorType,
            actorId: actorId,
            actorRole: actorRole,
            targetType: targetType,
            targetId: targetId,
            outcome: outcome,
            requestId: requestId));

        Assert.Equal(RetentionPurgeTestData.Names.PurgeActor, failure.ConstraintName);
    }

    /// <summary>db-design §2.3: the action code is accepted by <c>ck_audit_event_action</c>.</summary>
    [Fact]
    public async Task TheActionCode_IsOnTheClosedList()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var definition = await host.ScalarAsync<string>(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_audit_event_action'",
            ct);

        Assert.Contains("'retention_purge_run'", definition, StringComparison.Ordinal);
    }

    /// <summary>db-design §2.5: the delete by <c>occurred_at</c> has its index.</summary>
    [Fact]
    public async Task TheAuditRowDelete_HasItsIndexOnOccurredAt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var definition = await IndexDefinitionAsync(host, RetentionPurgeTestData.Names.AuditOccurredAtIndex, ct);

        Assert.Contains("(occurred_at)", definition, StringComparison.Ordinal);
    }

    /// <summary>db-design §3: the leaver query reads a partial index over off-roster rows only.</summary>
    [Fact]
    public async Task TheLeaverQuery_HasAPartialIndexOverOffRosterRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var definition = await IndexDefinitionAsync(host, RetentionPurgeTestData.Names.OffRosterIndex, ct);

        Assert.Contains("(last_seen_at)", definition, StringComparison.Ordinal);
        Assert.Contains("WHERE (on_roster = false)", definition, StringComparison.Ordinal);
    }

    /// <summary>db-design §5: no index on <c>app_user</c> for the purge — the table holds a handful of rows.</summary>
    [Fact]
    public async Task AppUser_GetsNoPurgeIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(
            new[] { "pk_app_user", "uq_app_user_normalized_email" },
            await InstallationSchemaQueries.IndexNamesAsync(host, "app_user", ct));
    }

    /// <summary>PC-8, PC-11, db-design §1: the purge deletes child first; no foreign key it crosses cascades.</summary>
    [Theory]
    [InlineData("course_work")]
    [InlineData("submission")]
    [InlineData("course_membership")]
    public async Task EveryForeignKeyThePurgeCrosses_StaysRestrict(string table)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var rules = await host.QueryAsync(
            "SELECT confdeltype::text FROM pg_constraint WHERE conrelid = @table::regclass AND contype = 'f'",
            r => r.GetString(0),
            ct,
            ("table", table));

        Assert.NotEmpty(rules);
        Assert.All(rules, rule => Assert.Equal("r", rule));
    }

    /// <summary>db-design §7: rows written before the migration are untouched — every count is null on them.</summary>
    [Fact]
    public async Task OrdinaryRows_CarryNoCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await host.InsertAuditRowAsync(RetentionPurgeTestData.Now, ct);

        Assert.Equal(
            1L,
            await host.CountAsync(
                "audit_event",
                ct,
                "purged_courses IS NULL AND purged_leaver_memberships IS NULL AND purged_participants IS NULL "
                + "AND purged_accounts IS NULL AND purged_audit_rows IS NULL"));
    }

    private static Task<int> InsertAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        string actorType = "system",
        long? actorId = null,
        string? actorRole = null,
        string action = RetentionPurgeTestData.Action,
        string? targetType = null,
        long? targetId = null,
        string outcome = "succeeded",
        string? requestId = null,
        int? courses = 1,
        int? leavers = 2,
        int? participants = 3,
        int? accounts = 4,
        int? auditRows = 5) =>
        host.ExecuteAsync(
            InsertPurgeRow,
            cancellationToken,
            ("actorType", actorType),
            ("actorId", actorId),
            ("actorRole", actorRole),
            ("action", action),
            ("targetType", targetType),
            ("targetId", targetId),
            ("outcome", outcome),
            ("requestId", requestId),
            ("courses", courses),
            ("leavers", leavers),
            ("participants", participants),
            ("accounts", accounts),
            ("auditRows", auditRows));

    private static async Task<string> IndexDefinitionAsync(
        InstallationTestHost host,
        string name,
        CancellationToken cancellationToken) =>
        await host.ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND indexname = @name",
            cancellationToken,
            ("name", name))
        ?? throw new Xunit.Sdk.XunitException($"Index {name} does not exist.");
}
