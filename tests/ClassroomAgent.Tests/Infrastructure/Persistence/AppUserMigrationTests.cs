using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-008 db-design 7: one installation migration creates both new tables and seeds nothing, the model does not
/// drift from it, and the <b>Control Plane</b> schema is deliberately untouched by this Story (PC-2, DC-4).
/// </summary>
public sealed class AppUserMigrationTests(PostgreSqlFixture database)
{
    // US-014 db-design §9 item 10: the installation's counts change again — six migrations to seven — with
    // _AddCoursesAndRosters last and three tables added to the set. US-013 db-design §8 made the previous such
    // change (five to six, _AddSyncState). Traced here as US-011 and US-012 traced their own.
    [Fact]
    public async Task TheMigrations_CreateLegitimacyStateThenAppUserAndAuditEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var tables = await host.TableNamesAsync(ct);
        var migrations = await host.QueryAsync(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"",
            r => r.GetString(0),
            ct);

        // US-009 adds workspace_connection in its own migration, as its db-design §7.1 fixes. US-011 adds no table:
        // its migration only amends two audit_event check constraints (US-011 db-design §7.1). US-013 adds
        // sync_state in its own migration and touches no other table (US-013 db-design §5). US-014 adds three
        // tables in ONE migration and alters none of the existing ones (US-014 db-design §1, §6). US-015 adds
        // course_work and submission in one further migration and alters none of the existing tables (US-015
        // db-design §1, §6).
        Assert.Equal(
            new[]
            {
                "__EFMigrationsHistory",
                CourseTestData.ParticipantTable,
                CourseTestData.CourseTable,
                CourseTestData.MembershipTable,
                CourseWorkTestData.CourseWorkTable,
                CourseWorkTestData.SubmissionTable,
                "app_user",
                "audit_event",
                "legitimacy_state",
                "sync_state",
                "workspace_connection",
            }.Order(StringComparer.Ordinal),
            tables.Order(StringComparer.Ordinal));
        // US-037 adds no table: its migration amends audit_event and indexes course_membership (db-design §7).
        // US-019 adds none either: its migration amends audit_event's checks only (db-design §3).
        Assert.Equal(10, migrations.Count);
        Assert.EndsWith("_InitialLegitimacyState", migrations[0], StringComparison.Ordinal);
        Assert.EndsWith("_InitialAppUserAndAuditEvent", migrations[1], StringComparison.Ordinal);
        Assert.EndsWith("_AddWorkspaceConnection", migrations[2], StringComparison.Ordinal);
        Assert.EndsWith("_AddAccessCheckAudit", migrations[3], StringComparison.Ordinal);
        Assert.EndsWith("_AddDeanAccounts", migrations[4], StringComparison.Ordinal);
        Assert.EndsWith(SyncTestData.Migration, migrations[5], StringComparison.Ordinal);
        Assert.EndsWith(CourseTestData.Migration, migrations[6], StringComparison.Ordinal);
        Assert.EndsWith(CourseWorkTestData.Migration, migrations[7], StringComparison.Ordinal);
        Assert.EndsWith("_AddRetentionPurge", migrations[8], StringComparison.Ordinal);
        Assert.EndsWith("_AddSynchronizationRequestAudit", migrations[9], StringComparison.Ordinal);
    }

    /// <summary>db-design 7.1: the migration seeds nothing — the first account appears when a person signs in.</summary>
    [Fact]
    public async Task TheMigration_SeedsNoAccountAndNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Empty(await host.AppUsersAsync(ct));
        Assert.Empty(await host.AuditRowsAsync(ct));
    }

    [Fact]
    public async Task TheModel_HasNoPendingChangesAgainstTheMigrations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var scope = host.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();

        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    /// <summary>PC-2: the host still never applies a migration at start-up.</summary>
    [Fact]
    public async Task HostStart_DoesNotApplyMigrations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct, migrate: false);

        host.Start();

        Assert.Empty(await host.TableNamesAsync(ct));
    }

    /// <summary>db-design 7.2: a Control Plane migration in this Story would itself be a defect.</summary>
    [Fact]
    public async Task TheControlPlaneSchema_IsUnchangedByThisStory()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var migrations = await host.QueryAsync(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"",
            r => r.GetString(0),
            ct);
        var tables = await host.QueryAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name",
            r => r.GetString(0),
            ct);

        Assert.Equal(5, migrations.Count);
        Assert.EndsWith("_AddInstallationPushAddress", migrations[4], StringComparison.Ordinal);
        Assert.Equal(
            new[] { "__EFMigrationsHistory", "allowed_admin", "audit_event", "installation", "instance_license_check", "owner" },
            tables.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(tables, t => t == "app_user");
    }

    /// <summary>db-design 7.2: the Control Plane model does not drift either.</summary>
    [Fact]
    public async Task TheControlPlaneModel_HasNoPendingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var scope = host.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ClassroomAgent.ControlPlane.Persistence.ControlPlaneDbContext>();

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
