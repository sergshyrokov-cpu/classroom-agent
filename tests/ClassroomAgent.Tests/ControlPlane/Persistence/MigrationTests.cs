using ClassroomAgent.ControlPlane.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.ControlPlane.Persistence;

/// <summary>
/// US-001 db-design §1, §6, US-002 db-design §7, US-003 db-design §7 and US-005 db-design §6.1: the Control Plane migrations in order, and model drift (PC-2).
/// </summary>
public sealed class MigrationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Migrations_CreateOwnerAuditEventInstallationAllowedAdminAndInstanceLicenseCheck_InOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var tables = await host.QueryAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name",
            r => r.GetString(0),
            ct);
        var migrations = await host.QueryAsync(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"",
            r => r.GetString(0),
            ct);
        var trigger = await host.ScalarAsync<string>(
            "SELECT tgname::text FROM pg_trigger WHERE tgname = 'trg_audit_event_immutable' AND tgrelid = 'audit_event'::regclass",
            ct);

        Assert.Equal(new[] { "__EFMigrationsHistory", "allowed_admin", "audit_event", "installation", "instance_license_check", "owner" }, tables.Order(StringComparer.Ordinal));
        Assert.Equal(4, migrations.Count);
        Assert.EndsWith("_InitialOwnerAndAudit", migrations[0], StringComparison.Ordinal);
        Assert.EndsWith("_AddInstallation", migrations[1], StringComparison.Ordinal);
        Assert.EndsWith("_AddAllowedAdmin", migrations[2], StringComparison.Ordinal);
        Assert.EndsWith("_AddInstanceLicenseCheck", migrations[3], StringComparison.Ordinal);
        Assert.Equal("trg_audit_event_immutable", trigger);
    }

    [Fact]
    public async Task Model_HasNoPendingChangesAgainstMigrations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var scope = host.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();

        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
