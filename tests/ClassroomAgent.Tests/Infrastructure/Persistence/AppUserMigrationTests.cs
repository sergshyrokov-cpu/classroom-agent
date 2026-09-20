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

        // US-009 adds workspace_connection in its own migration, as its db-design §7.1 fixes.
        Assert.Equal(
            new[] { "__EFMigrationsHistory", "app_user", "audit_event", "legitimacy_state", "workspace_connection" },
            tables.Order(StringComparer.Ordinal));
        Assert.Equal(3, migrations.Count);
        Assert.EndsWith("_InitialLegitimacyState", migrations[0], StringComparison.Ordinal);
        Assert.EndsWith("_InitialAppUserAndAuditEvent", migrations[1], StringComparison.Ordinal);
        Assert.EndsWith("_AddWorkspaceConnection", migrations[2], StringComparison.Ordinal);
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
