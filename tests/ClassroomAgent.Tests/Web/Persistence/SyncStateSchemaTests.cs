using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Web.Persistence;

/// <summary>
/// US-013 db-design §3.1, §3.2, §3.4, §5: the migration creates <c>sync_state</c> with exactly the columns the
/// design fixes, and the database — not application code alone — enforces the singleton row, the status
/// vocabulary, the counter, the ordering of the two instants and the three terminal-field disagreements. Against
/// real PostgreSQL via Testcontainers — the InMemory provider is forbidden (TC-2).
/// </summary>
public sealed class SyncStateSchemaTests(PostgreSqlFixture database)
{
    /// <summary>US-013 db-design §3.1: the migration creates the table with exactly its eleven columns.</summary>
    [Fact]
    public async Task TheMigration_CreatesTheTable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await host.QueryAsync(
            """
            SELECT column_name, is_nullable, data_type, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'sync_state'
            ORDER BY column_name
            """,
            r => (Name: r.GetString(0), Nullable: r.GetString(1), Type: r.GetString(2), Length: r.IsDBNull(3) ? (int?)null : r.GetInt32(3)),
            ct);

        Assert.Equal(11, columns.Count);
        Assert.Contains(columns, c => c.Name == "id" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "singleton" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "status" && c.Nullable == "NO" && c.Length == 16);
        Assert.Contains(columns, c => c.Name == "run_id" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "started_at" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "finished_at" && c.Nullable == "YES");
        Assert.Contains(columns, c => c.Name == "processed_count" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "last_error" && c.Nullable == "YES" && c.Length == 512);
        Assert.Contains(columns, c => c.Name == "last_successful_run_at" && c.Nullable == "YES");
        Assert.Contains(columns, c => c.Name == "created_at" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "updated_at" && c.Nullable == "NO");
    }

    /// <summary>US-013 db-design §3.2, §3.4: the primary key and the singleton unique index exist.</summary>
    [Fact]
    public async Task ThePrimaryKeyAndSingletonIndex_Exist()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var primaryKey = await InstallationSchemaQueries.PrimaryKeyAsync(host, SyncTestData.Table, ct);
        var indexes = await InstallationSchemaQueries.IndexNamesAsync(host, SyncTestData.Table, ct);

        Assert.Equal("pk_sync_state", primaryKey);
        Assert.Contains(SyncTestData.Constraints.UniqueSingleton, indexes);
    }

    /// <summary>US-013 db-design §3.2, §3.4: the table holds at most one row, enforced by the database.</summary>
    [Fact]
    public async Task ASecondRow_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await InsertSyncStateAsync(host, ct);

        var second = async () => await InsertSyncStateAsync(host, ct, runId: Guid.NewGuid());

        var error = await Assert.ThrowsAsync<PostgresException>(second);
        Assert.Equal("23505", error.SqlState);
        Assert.Equal(SyncTestData.Constraints.UniqueSingleton, error.ConstraintName);
    }

    /// <summary>US-013 db-design §3.2: a status outside the three-value vocabulary is rejected.</summary>
    [Fact]
    public async Task AnUnknownStatus_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var insert = async () => await InsertSyncStateAsync(host, ct, status: "interrupted");

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(SyncTestData.Constraints.Status, error.ConstraintName);
    }

    /// <summary>US-013 db-design §3.2: the three disagreements <c>ck_sync_state_terminal_fields</c> forbids.</summary>
    [Theory]
    [InlineData(SyncTestData.Status.Running, true, null)]
    [InlineData(SyncTestData.Status.Completed, true, "Transient:the synthetic port refused this run")]
    [InlineData(SyncTestData.Status.Failed, true, null)]
    public async Task ATerminalFieldsDisagreement_IsRejected(string status, bool finishedAtIsSet, string? lastError)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var startedAt = host.Time.GetUtcNow();
        var finishedAt = finishedAtIsSet ? startedAt : (DateTimeOffset?)null;

        var insert = async () => await InsertSyncStateAsync(
            host,
            ct,
            status: status,
            startedAt: startedAt,
            finishedAt: finishedAt,
            lastError: lastError);

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(SyncTestData.Constraints.TerminalFields, error.ConstraintName);
    }

    /// <summary>US-013 db-design §3.2, spec VR-002: the counter is never negative.</summary>
    [Fact]
    public async Task ANegativeProcessedCount_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var insert = async () => await InsertSyncStateAsync(host, ct, processedCount: -1);

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(SyncTestData.Constraints.Counter, error.ConstraintName);
    }

    /// <summary>US-013 db-design §3.2, spec VR-002: the end instant is never earlier than the start instant.</summary>
    [Fact]
    public async Task AFinishedAtEarlierThanStartedAt_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var startedAt = host.Time.GetUtcNow();

        var insert = async () => await InsertSyncStateAsync(
            host,
            ct,
            status: SyncTestData.Status.Completed,
            startedAt: startedAt,
            finishedAt: startedAt - TimeSpan.FromMinutes(1));

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(SyncTestData.Constraints.FinishedAfterStarted, error.ConstraintName);
    }

    /// <summary>US-013 db-design §3.4: the table has no foreign key, and nothing points at it.</summary>
    [Fact]
    public async Task TheTable_HasNoForeignKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var keys = await InstallationSchemaQueries.ForeignKeysAsync(host, SyncTestData.Table, ct);

        Assert.Empty(keys);
    }

    /// <summary>US-013 db-design §3.4: the only index besides the primary key is the singleton one.</summary>
    [Fact]
    public async Task TheOnlyIndex_IsTheSingletonOne()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var indexes = await InstallationSchemaQueries.IndexNamesAsync(host, SyncTestData.Table, ct);

        Assert.Equal(new[] { "pk_sync_state", SyncTestData.Constraints.UniqueSingleton }, indexes);
    }

    /// <summary>
    /// Inserts a <c>sync_state</c> row directly, following the shape of
    /// <see cref="InstallationTestHost.InsertLegitimacyStateAsync"/> and
    /// <see cref="InstallationTestHost.InsertWorkspaceConnectionAsync"/> — a default that satisfies every
    /// constraint (a running run, just started, with nothing terminal yet), overridden per test.
    /// </summary>
    private static Task<int> InsertSyncStateAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        string status = SyncTestData.Status.Running,
        Guid? runId = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? finishedAt = null,
        int processedCount = 0,
        string? lastError = null,
        DateTimeOffset? lastSuccessfulRunAt = null) =>
        host.ExecuteAsync(
            """
            INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count,
                                    last_error, last_successful_run_at, created_at, updated_at)
            VALUES (true, @status, @runId, @startedAt, @finishedAt, @processedCount, @lastError,
                    @lastSuccessfulRunAt, @stamp, @stamp)
            """,
            cancellationToken,
            ("status", status),
            ("runId", runId ?? Guid.NewGuid()),
            ("startedAt", startedAt ?? host.Time.GetUtcNow()),
            ("finishedAt", finishedAt),
            ("processedCount", processedCount),
            ("lastError", lastError),
            ("lastSuccessfulRunAt", lastSuccessfulRunAt),
            ("stamp", host.Time.GetUtcNow()));
}
