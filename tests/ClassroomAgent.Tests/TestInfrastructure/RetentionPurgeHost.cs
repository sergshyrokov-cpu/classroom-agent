using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-037 against a real host and its real PostgreSQL database (TC-2). Two shapes:
/// <list type="bullet">
/// <item><see cref="StartWithoutPurgeServiceAsync"/> — the host without its purge background service, so a test seeds
/// rows and then runs the use case itself, exactly once, resolved from the host's own DI (the rows would otherwise
/// be purged at start, before the test looks);</item>
/// <item><see cref="StartWithPurgeServiceAsync"/> — the host as production registers it, for the schedule, the
/// coordination with synchronization and the log lines.</item>
/// </list>
/// Nothing reaches Google (TC-4): no connection is seeded, so synchronization skips every run.
/// </summary>
public static class RetentionPurgeHost
{
    public static async Task<InstallationTestHost> StartWithoutPurgeServiceAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly,
        HttpMessageHandler? controlPlaneHandler = null)
    {
        var host = await CreateAsync(database, cause, cancellationToken);
        host.ConfigureServices = RemovePurgeService;
        host.ControlPlaneHandler = controlPlaneHandler;
        host.Start();
        return host;
    }

    /// <summary>
    /// The production host. <paramref name="seed"/> runs before the start, because the first purge begins as soon
    /// as the host has started (spec FR-013).
    /// </summary>
    public static async Task<InstallationTestHost> StartWithPurgeServiceAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        Func<InstallationTestHost, CancellationToken, Task>? seed = null)
    {
        var host = await CreateAsync(database, ReadOnlyModeHost.Cause.NotReadOnly, cancellationToken);
        if (seed is not null)
        {
            await seed(host, cancellationToken);
        }

        host.Start();
        return host;
    }

    /// <summary>One purge run through the use case the host registers (spec FR-009).</summary>
    public static async Task<RetentionPurgeOutcome> RunPurgeAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        using var scope = host.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RunRetentionPurgeUseCase>().ExecuteAsync(cancellationToken);
    }

    /// <summary>The purge's own audit rows (spec FR-010), which <see cref="InstallationTestHost.AuditRowsAsync"/> leaves out.</summary>
    public static Task<IReadOnlyList<PurgeAuditRow>> PurgeAuditRowsAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync(
            """
            SELECT id, occurred_at, actor_type, actor_id, actor_role, target_type, target_id, outcome,
                   refusal_category, request_id, purged_courses, purged_leaver_memberships, purged_participants,
                   purged_accounts, purged_audit_rows
            FROM audit_event WHERE action = 'retention_purge_run' ORDER BY id
            """,
            r => new PurgeAuditRow(
                r.GetInt64(0),
                r.GetFieldValue<DateTimeOffset>(1),
                r.GetString(2),
                r.IsDBNull(3) ? null : r.GetInt64(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetInt64(6),
                r.GetString(7),
                r.IsDBNull(8) ? null : r.GetString(8),
                r.IsDBNull(9) ? null : r.GetString(9),
                r.IsDBNull(10) ? null : r.GetInt32(10),
                r.IsDBNull(11) ? null : r.GetInt32(11),
                r.IsDBNull(12) ? null : r.GetInt32(12),
                r.IsDBNull(13) ? null : r.GetInt32(13),
                r.IsDBNull(14) ? null : r.GetInt32(14)),
            cancellationToken);

    /// <summary>Waits (real time, bounded) until the purge has written that many audit rows.</summary>
    public static Task WaitForPurgeRunsAsync(this InstallationTestHost host, int count, CancellationToken cancellationToken) =>
        host.Time.WaitUntilAsync(
            () => host.PurgeAuditRowsAsync(cancellationToken).GetAwaiter().GetResult().Count >= count,
            $"{count} retention purge audit row(s)",
            cancellationToken);

    /// <summary>A valid audit row of an ordinary action at <paramref name="occurredAt"/>; returns its id.</summary>
    public static Task<long> InsertAuditRowAsync(
        this InstallationTestHost host,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken,
        long actorId = 1,
        string targetType = "workspace_connection",
        long? targetId = null) =>
        host.ScalarAsync<long>(
            """
            INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                     outcome, refusal_category, request_id, created_at, updated_at)
            VALUES (@occurredAt, 'app_user', @actorId, 'admin', 'workspace_connection_saved', @targetType, @targetId,
                    'succeeded', NULL, 'request-id', @occurredAt, @occurredAt)
            RETURNING id
            """,
            cancellationToken,
            ("occurredAt", occurredAt),
            ("actorId", actorId),
            ("targetType", targetType),
            ("targetId", targetId ?? actorId));

    /// <summary>Inserts an account and returns its id (db-design §5: last sign-in, else creation, decides).</summary>
    public static async Task<long> InsertAccountAsync(
        this InstallationTestHost host,
        string email,
        string role,
        DateTimeOffset createdAt,
        DateTimeOffset? lastSuccessfulSignInAt,
        CancellationToken cancellationToken,
        bool isDisabled = false)
    {
        await host.InsertAppUserAsync(
            cancellationToken,
            email: email,
            role: role,
            signInMethod: role == "admin" ? "google" : "password",
            passwordHash: role == "admin" ? null : "synthetic-hash",
            isDisabled: isDisabled,
            lastSuccessfulSignInAt: lastSuccessfulSignInAt,
            stampedAt: createdAt);
        return (await host.AppUsersAsync(cancellationToken)).Single(u => u.Email == email.ToLowerInvariant()).Id;
    }

    /// <summary>Row count of a table, optionally filtered by a fixed SQL predicate written in the test.</summary>
    public static Task<long> CountAsync(
        this InstallationTestHost host,
        string table,
        CancellationToken cancellationToken,
        string where = "true",
        params (string Name, object? Value)[] parameters) =>
        host.ScalarAsync<long>($"SELECT count(*) FROM {table} WHERE {where}", cancellationToken, parameters);

    /// <summary>
    /// Makes every delete of the matching rows fail inside PostgreSQL, so a unit's transaction must roll back (spec
    /// FR-004, FR-009; AC-010, AC-017). A real failure of the real database, not a substituted port.
    /// </summary>
    public static Task FailDeletesAsync(
        this InstallationTestHost host,
        string table,
        CancellationToken cancellationToken,
        long? id = null)
    {
        var name = $"purge_test_fail_{table}";
        var when = id is { } value ? $"WHEN (OLD.id = {value})" : string.Empty;
        return host.ExecuteAsync(
            $"""
            CREATE OR REPLACE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN RAISE EXCEPTION 'injected purge test failure'; END $$;
            CREATE TRIGGER {name} BEFORE DELETE ON {table} FOR EACH ROW {when} EXECUTE FUNCTION {name}();
            """,
            cancellationToken);
    }

    /// <summary>Lifts what <see cref="FailDeletesAsync"/> installed, for the "next run retries" scenario.</summary>
    public static Task StopFailingDeletesAsync(this InstallationTestHost host, string table, CancellationToken cancellationToken) =>
        host.ExecuteAsync($"DROP TRIGGER purge_test_fail_{table} ON {table}", cancellationToken);

    private static async Task<InstallationTestHost> CreateAsync(
        PostgreSqlFixture database,
        ReadOnlyModeHost.Cause cause,
        CancellationToken cancellationToken)
    {
        var host = await InstallationTestHost.CreateAsync(database, cancellationToken);
        await ReadOnlyModeHost.SeedAsync(host, cause, cancellationToken);

        // As in SyncHostExtensions: a read-only cause is answered with a failure so the seeded state survives the
        // check at start; otherwise a success keeps the installation out of read-only mode.
        if (cause == ReadOnlyModeHost.Cause.NotReadOnly)
        {
            host.ControlPlane.ReplySuccess().ReplySuccess().ReplySuccess();
        }
        else
        {
            host.ControlPlane
                .ReplyFailure(CheckFailureCategory.Unreachable)
                .ReplyFailure(CheckFailureCategory.Unreachable)
                .ReplyFailure(CheckFailureCategory.Unreachable);
        }

        return host;
    }

    private static void RemovePurgeService(IServiceCollection services)
    {
        foreach (var descriptor in services
                     .Where(d => d.ServiceType == typeof(IHostedService)
                                 && d.ImplementationType == typeof(RetentionPurgeBackgroundService))
                     .ToList())
        {
            services.Remove(descriptor);
        }
    }

    /// <summary>A purge run's audit row as the database holds it (db-design §2).</summary>
    public sealed record PurgeAuditRow(
        long Id,
        DateTimeOffset OccurredAt,
        string ActorType,
        long? ActorId,
        string? ActorRole,
        string? TargetType,
        long? TargetId,
        string Outcome,
        string? RefusalCategory,
        string? RequestId,
        int? Courses,
        int? LeaverMemberships,
        int? Participants,
        int? Accounts,
        int? AuditRows);
}
