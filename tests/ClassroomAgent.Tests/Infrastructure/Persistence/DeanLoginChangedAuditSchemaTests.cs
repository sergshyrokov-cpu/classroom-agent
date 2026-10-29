using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-067 db-design §3, §4 against real PostgreSQL (TC-2): the closed list <c>ck_audit_event_action</c> accepts
/// <c>dean_login_changed</c> for the success row and the read-only refusal row (SPEC_REVIEW m-1), the change ships as
/// the migration <c>AddDeanLoginChangedAuditAction</c>, and its Down refuses while rows with the code exist — audit
/// rows are never deleted (SC-11). No column changes (spec §9).
/// </summary>
public sealed class DeanLoginChangedAuditSchemaTests(PostgreSqlFixture database)
{
    /// <summary>The last migration before this Story — the schema every existing installation has.</summary>
    private const string BeforeThisStory = "20261028123007_AddErasureExclusionInAdvance";

    private const string MigrationSuffix = "_AddDeanLoginChangedAuditAction";

    private const string ActionCheck = "ck_audit_event_action";

    private const string Insert =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, created_at, updated_at)
        VALUES (now(), 'app_user', 1, 'admin', @action, 'app_user', 2, @outcome, @category, 'r-67', now(), now())
        """;

    private static ClassroomAgentDbContext Context(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<ClassroomAgentDbContext>();
        ClassroomAgentDbContextOptions.Configure(builder, connectionString);
        builder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        return new ClassroomAgentDbContext(builder.Options);
    }

    private static Task<int> InsertAsync(InstallationTestHost host, string outcome, string? category, CancellationToken ct) =>
        host.ExecuteAsync(
            Insert,
            ct,
            ("action", DeanAccountTestData.Audit.LoginChanged),
            ("outcome", outcome),
            ("category", category));

    /// <summary>Db-design §3, FR-007: the success row with the new code is accepted.</summary>
    [Fact]
    public async Task TheSuccessRow_IsAcceptedByTheActionList()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(1, await InsertAsync(host, "succeeded", null, ct));
    }

    /// <summary>
    /// Db-design §3, FR-008, SPEC_REVIEW m-1: the read-only refusal row — the new code, outcome refused, category
    /// read-only — is accepted.
    /// </summary>
    [Fact]
    public async Task TheReadOnlyRefusalRow_IsAcceptedByTheActionList()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(1, await InsertAsync(host, "refused", DeanAccountTestData.Audit.ReadOnlyMode, ct));
    }

    /// <summary>Db-design §3: the list stays closed — a code nobody added is still rejected by the same constraint.</summary>
    [Fact]
    public async Task AnActionOutsideTheList_IsStillRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => host.ExecuteAsync(
            Insert,
            ct,
            ("action", "dean_login_renamed"),
            ("outcome", "succeeded"),
            ("category", null)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(ActionCheck, failure.ConstraintName);
    }

    /// <summary>Db-design §4, PC-2: exactly one migration under the designed name, after the last one before this Story.</summary>
    [Fact]
    public async Task TheMigration_ExistsUnderTheDesignedName_AfterTheLastOne()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var db = Context(host.ConnectionString);

        var migrations = db.Database.GetMigrations().ToList();

        var ours = Assert.Single(migrations, m => m.EndsWith(MigrationSuffix, StringComparison.Ordinal));
        Assert.True(string.CompareOrdinal(ours, BeforeThisStory) > 0);
        Assert.Contains(BeforeThisStory, migrations);
    }

    /// <summary>
    /// A database migrated by the application's own context options — the snake_case history table the migrator reads
    /// back when it migrates down (the <c>DataHealthPersistenceTests</c> pattern).
    /// </summary>
    private async Task<InstallationTestHost> CreateMigratedByTheContextAsync(CancellationToken ct)
    {
        var host = await InstallationTestHost.CreateAsync(database, ct, migrate: false);
        await using var db = Context(host.ConnectionString);
        await db.Database.MigrateAsync(ct);
        return host;
    }

    /// <summary>
    /// Db-design §4: Down refuses while a <c>dean_login_changed</c> row exists — the old list cannot be re-added over it
    /// and audit rows are never deleted (SC-11) — and the row is still there afterwards.
    /// </summary>
    [Fact]
    public async Task Down_IsRefused_WhileALoginChangeRowExists()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await CreateMigratedByTheContextAsync(ct);
        await InsertAsync(host, "succeeded", null, ct);

        await using (var db = Context(host.ConnectionString))
        {
            Assert.Single(db.Database.GetMigrations(), m => m.EndsWith(MigrationSuffix, StringComparison.Ordinal));
            var failure = await Assert.ThrowsAsync<PostgresException>(
                () => db.GetService<IMigrator>().MigrateAsync(BeforeThisStory, ct));
            Assert.Contains("dean_login_changed audit rows exist", failure.MessageText, StringComparison.Ordinal);
        }

        var count = await host.ScalarAsync<long>(
            "SELECT count(*) FROM audit_event WHERE action = 'dean_login_changed'",
            ct);
        Assert.Equal(1, count);
    }

    /// <summary>
    /// Db-design §4: with no such row, Down restores the earlier list — the code is rejected again — and Up accepts it
    /// once more.
    /// </summary>
    [Fact]
    public async Task DownAndUp_WithoutSuchRows_RemoveAndRestoreTheCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await CreateMigratedByTheContextAsync(ct);

        await using (var db = Context(host.ConnectionString))
        {
            Assert.Single(db.Database.GetMigrations(), m => m.EndsWith(MigrationSuffix, StringComparison.Ordinal));
            await db.GetService<IMigrator>().MigrateAsync(BeforeThisStory, ct);
        }

        var refused = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, "succeeded", null, ct));
        Assert.Equal(ActionCheck, refused.ConstraintName);

        await using (var db = Context(host.ConnectionString))
        {
            await db.Database.MigrateAsync(ct);
        }

        Assert.Equal(1, await InsertAsync(host, "succeeded", null, ct));
    }
}
