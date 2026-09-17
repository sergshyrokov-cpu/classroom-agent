using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-005 AC-011, FR-009: the first installation migration creates exactly <c>legitimacy_state</c> with
/// its columns and constraints — at most one row — and no row (db-design §4, §6.2; PC-2).
/// </summary>
public sealed class InstallationDatabaseTests(PostgreSqlFixture database)
{
    private const string Insert =
        """
        INSERT INTO legitimacy_state (last_successful_check_at, status, compatibility, domain, client_id, created_at, updated_at)
        VALUES (now(), @status, @compatibility, @domain, @clientId, now(), now())
        """;

    [Fact]
    public async Task Migration_CreatesOnlyLegitimacyState_WithNoRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var tables = await host.QueryAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name",
            r => r.GetString(0),
            ct);
        var migrations = await host.QueryAsync(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"",
            r => r.GetString(0),
            ct);

        Assert.Equal(new[] { "__EFMigrationsHistory", "legitimacy_state" }, tables.Order(StringComparer.Ordinal));
        var migration = Assert.Single(migrations);
        Assert.EndsWith("_InitialLegitimacyState", migration, StringComparison.Ordinal);
        Assert.Empty(await host.LegitimacyStatesAsync(ct));
    }

    [Fact]
    public async Task Model_HasNoPendingChangesAgainstMigrations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var scope = host.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();

        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task HostStart_DoesNotApplyMigrations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct, migrate: false);

        host.Start();

        var tables = await host.QueryAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'",
            r => r.GetString(0),
            ct);
        Assert.Empty(tables);
    }

    [Fact]
    public async Task Columns_MatchDesign_NoInstallationIdKeyOrSecretColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await host.QueryAsync(
            """
            SELECT column_name || ' ' || data_type || ' ' || coalesce(character_maximum_length::text, '-') || ' ' || is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'legitimacy_state'
            ORDER BY column_name
            """,
            r => r.GetString(0),
            ct);
        var singletonDefault = await host.ScalarAsync<string>(
            "SELECT column_default FROM information_schema.columns WHERE table_name = 'legitimacy_state' AND column_name = 'singleton'",
            ct);
        var primaryKey = await host.ScalarAsync<string>(
            "SELECT constraint_name::text FROM information_schema.table_constraints WHERE table_name = 'legitimacy_state' AND constraint_type = 'PRIMARY KEY'",
            ct);

        Assert.Equal(
            new[]
            {
                "client_id character varying 32 NO",
                "compatibility character varying 24 NO",
                "created_at timestamp with time zone - NO",
                "domain character varying 253 NO",
                "id bigint - NO",
                "last_successful_check_at timestamp with time zone - YES",
                "singleton boolean - NO",
                "status character varying 16 NO",
                "updated_at timestamp with time zone - NO",
            },
            columns);
        Assert.Equal("true", singletonDefault);
        Assert.Equal("pk_legitimacy_state", primaryKey);
    }

    [Fact]
    public async Task SecondRow_ViolatesSingletonUniqueConstraint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await InsertAsync(host, ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, ct));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("uq_legitimacy_state_singleton", error.ConstraintName);
        Assert.Single(await host.LegitimacyStatesAsync(ct));
    }

    [Fact]
    public async Task SingletonFalse_ViolatesSingletonCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() => host.ExecuteAsync(
            """
            INSERT INTO legitimacy_state (singleton, status, compatibility, domain, client_id, created_at, updated_at)
            VALUES (false, 'active', 'supported', 'school-one.example.test', '1234567890', now(), now())
            """,
            ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_legitimacy_state_singleton", error.ConstraintName);
    }

    [Theory]
    [InlineData("paused", "supported", "school-one.example.test", "1234567890", "ck_legitimacy_state_status")]
    [InlineData("Active", "supported", "school-one.example.test", "1234567890", "ck_legitimacy_state_status")]
    [InlineData("active", "ok", "school-one.example.test", "1234567890", "ck_legitimacy_state_compatibility")]
    [InlineData("active", "supported", "ab", "1234567890", "ck_legitimacy_state_domain_length")]
    [InlineData("active", "supported", "school-one.example.test", "123456789", "ck_legitimacy_state_client_id_format")]
    [InlineData("active", "supported", "school-one.example.test", "12345678901234567890123456789012a", "ck_legitimacy_state_client_id_format")]
    [InlineData("active", "supported", "school-one.example.test", "12345678a0", "ck_legitimacy_state_client_id_format")]
    public async Task InvalidValue_ViolatesNamedCheckConstraint(string status, string compatibility, string domain, string clientId, string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, ct, status, compatibility, domain, clientId));

        Assert.True(error.SqlState is PostgresErrorCodes.CheckViolation or PostgresErrorCodes.StringDataRightTruncation, error.SqlState);
        if (error.SqlState == PostgresErrorCodes.CheckViolation)
        {
            Assert.Equal(constraint, error.ConstraintName);
        }
    }

    [Theory]
    [InlineData("suspended", "upgrade_required", "abc", "1234567890")]
    [InlineData("active", "upgrade_recommended", "school-one.example.test", "12345678901234567890123456789012")]
    public async Task BoundaryValues_AreAccepted(string status, string compatibility, string domain, string clientId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, ct, status, compatibility, domain, clientId);

        Assert.Single(await host.LegitimacyStatesAsync(ct));
    }

    [Fact]
    public async Task LastSuccessfulCheckAt_MayBeNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await host.InsertLegitimacyStateAsync(ct, null, compatibility: "upgrade_required");

        Assert.Null(Assert.Single(await host.LegitimacyStatesAsync(ct)).LastSuccessfulCheckAt);
    }

    private static Task<int> InsertAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        string status = "active",
        string compatibility = "supported",
        string domain = InstallationTestData.Domain,
        string clientId = InstallationTestData.ClientId) =>
        host.ExecuteAsync(
            Insert,
            cancellationToken,
            ("status", status),
            ("compatibility", compatibility),
            ("domain", domain),
            ("clientId", clientId));
}
