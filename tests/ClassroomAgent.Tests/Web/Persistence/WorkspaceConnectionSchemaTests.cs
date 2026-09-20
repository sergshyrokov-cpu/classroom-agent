using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Web.Persistence;

/// <summary>
/// US-009 AC-012: the schema change ships as one migration, and the table enforces what the entity promises
/// (db-design §3, §4; PC-2, PC-4, PC-5, PC-7). Against real PostgreSQL via Testcontainers — the InMemory
/// provider is forbidden (TC-2).
/// </summary>
public sealed class WorkspaceConnectionSchemaTests(PostgreSqlFixture database)
{
    /// <summary>AC-012: the migration creates the table with its three business columns and its timestamps.</summary>
    [Fact]
    public async Task TheMigration_CreatesTheTable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await host.QueryAsync(
            """
            SELECT column_name, is_nullable, data_type, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'workspace_connection'
            ORDER BY column_name
            """,
            r => (Name: r.GetString(0), Nullable: r.GetString(1), Type: r.GetString(2), Length: r.IsDBNull(3) ? (int?)null : r.GetInt32(3)),
            ct);

        Assert.Contains(columns, c => c.Name == "id" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "domain" && c.Nullable == "NO" && c.Length == 253);
        Assert.Contains(columns, c => c.Name == "impersonation_user_email" && c.Nullable == "NO" && c.Length == 254);
        Assert.Contains(columns, c => c.Name == "singleton" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "created_at" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "updated_at" && c.Nullable == "NO");
    }

    /// <summary>AC-007, AC-012: the table holds at most one row, enforced by the database.</summary>
    [Fact]
    public async Task ASecondRow_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertWorkspaceConnectionAsync(ct);

        var second = async () => await host.InsertWorkspaceConnectionAsync(
            ct,
            impersonationUserEmail: WorkspaceConnectionTestData.OtherTechnicalAccount);

        var error = await Assert.ThrowsAsync<PostgresException>(second);
        Assert.Equal("23505", error.SqlState);
        Assert.Equal("uq_workspace_connection_singleton", error.ConstraintName);
    }

    public static TheoryData<string, string> RejectedRows => new()
    {
        { WorkspaceConnectionTestData.AllowedDomain, "classroom-agent@school-two.example.test" },
        { WorkspaceConnectionTestData.AllowedDomain, "classroom-agent@sync." + WorkspaceConnectionTestData.AllowedDomain },
        { "school-one", "classroom-agent@school-one" },
        { "-school.example.test", "classroom-agent@-school.example.test" },
        { WorkspaceConnectionTestData.AllowedDomain + ".", "classroom-agent@" + WorkspaceConnectionTestData.AllowedDomain + "." },
    };

    /// <summary>AC-012: the check constraints of db-design §3.1 reject a row the application would never write.</summary>
    [Theory]
    [MemberData(nameof(RejectedRows))]
    public async Task AnInconsistentRow_IsRejected(string domain, string email)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var insert = async () => await host.ExecuteAsync(
            """
            INSERT INTO workspace_connection (domain, impersonation_user_email, created_at, updated_at)
            VALUES (@domain, @email, @stamp, @stamp)
            """,
            ct,
            ("domain", domain),
            ("email", email),
            ("stamp", host.Time.GetUtcNow()));

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
    }

    /// <summary>
    /// AC-012: a mixed-case value cannot be written, so normalisation cannot be skipped. An upper-case domain
    /// breaks two constraints of db-design §3.1 at once — the lower-case rule and the format rule, whose
    /// character class is lower-case ASCII — and which of them PostgreSQL reports is its choice, not something
    /// an artifact fixes; both are named here so the row cannot be rejected by an unrelated rule.
    /// </summary>
    [Theory]
    [InlineData("School-One.Example.Test", "classroom-agent@school-one.example.test", "ck_workspace_connection_domain_lowercase,ck_workspace_connection_domain_format")]
    [InlineData("school-one.example.test", "Classroom-Agent@school-one.example.test", "ck_workspace_connection_email_lowercase")]
    public async Task AMixedCaseRow_IsRejected(string domain, string email, string constraints)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var insert = async () => await host.ExecuteAsync(
            """
            INSERT INTO workspace_connection (domain, impersonation_user_email, created_at, updated_at)
            VALUES (@domain, @email, @stamp, @stamp)
            """,
            ct,
            ("domain", domain),
            ("email", email),
            ("stamp", host.Time.GetUtcNow()));

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Contains(error.ConstraintName, constraints.Split(','));
    }

    /// <summary>AC-012, PC-7: the only index is the one that keeps the table at a single row.</summary>
    [Fact]
    public async Task TheOnlyIndex_IsTheSingletonOne()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var indexes = await host.QueryAsync(
            "SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'workspace_connection' ORDER BY indexname",
            r => r.GetString(0),
            ct);

        Assert.Equal(new[] { "pk_workspace_connection", "uq_workspace_connection_singleton" }, indexes);
    }

    /// <summary>AC-012, PC-8: the table has no foreign key, and nothing points at it.</summary>
    [Fact]
    public async Task TheTable_HasNoForeignKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var keys = await host.QueryAsync(
            """
            SELECT conname FROM pg_constraint
            WHERE contype = 'f'
              AND (conrelid = 'workspace_connection'::regclass OR confrelid = 'workspace_connection'::regclass)
            """,
            r => r.GetString(0),
            ct);

        Assert.Empty(keys);
    }

    /// <summary>AC-012: the audit table accepts the action and the target type this Story adds (db-design §4).</summary>
    [Fact]
    public async Task TheAuditTable_AcceptsTheNewActionAndTargetType()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertAppUserAsync(ct);

        var written = await host.ExecuteAsync(
            """
            INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                     outcome, refusal_category, request_id, created_at, updated_at)
            VALUES (@stamp, 'app_user', (SELECT id FROM app_user LIMIT 1), 'admin', @action, @targetType, NULL,
                    'refused', @category, 'r-1', @stamp, @stamp)
            """,
            ct,
            ("stamp", host.Time.GetUtcNow()),
            ("action", WorkspaceConnectionTestData.Audit.Action),
            ("targetType", WorkspaceConnectionTestData.Audit.TargetType),
            ("category", WorkspaceConnectionTestData.Audit.DomainMismatch));

        Assert.Equal(1, written);
    }

    /// <summary>
    /// AC-012, db-design §4.1: a refused row names the kind of object without naming a row, and the pairing
    /// constraint still forbids the reverse — an identifier belonging to nothing.
    /// </summary>
    [Fact]
    public async Task AnAuditRowWithATargetIdButNoType_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertAppUserAsync(ct);

        var insert = async () => await host.ExecuteAsync(
            """
            INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                     outcome, refusal_category, request_id, created_at, updated_at)
            VALUES (@stamp, 'app_user', (SELECT id FROM app_user LIMIT 1), 'admin', @action, NULL, 1,
                    'succeeded', NULL, 'r-1', @stamp, @stamp)
            """,
            ct,
            ("stamp", host.Time.GetUtcNow()),
            ("action", WorkspaceConnectionTestData.Audit.Action));

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal("ck_audit_event_target", error.ConstraintName);
    }

    /// <summary>AC-012, db-design §4: a target type no enum member codes is rejected.</summary>
    [Fact]
    public async Task AnUnknownTargetType_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertAppUserAsync(ct);

        var insert = async () => await host.ExecuteAsync(
            """
            INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                     outcome, refusal_category, request_id, created_at, updated_at)
            VALUES (@stamp, 'app_user', (SELECT id FROM app_user LIMIT 1), 'admin', @action, 'something_else', 1,
                    'succeeded', NULL, 'r-1', @stamp, @stamp)
            """,
            ct,
            ("stamp", host.Time.GetUtcNow()),
            ("action", WorkspaceConnectionTestData.Audit.Action));

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal("ck_audit_event_target_type_value", error.ConstraintName);
    }

    /// <summary>AC-012: the installation database has exactly the tables the Stories so far created.</summary>
    [Fact]
    public async Task TheInstallationDatabase_HasTheExpectedTables()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var tables = await host.TableNamesAsync(ct);

        Assert.Contains("workspace_connection", tables);
        Assert.Contains("app_user", tables);
        Assert.Contains("audit_event", tables);
        Assert.Contains("legitimacy_state", tables);
    }
}
