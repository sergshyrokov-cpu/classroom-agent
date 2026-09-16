using ClassroomAgent.ControlPlane.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.ControlPlane.Persistence;

/// <summary>db-design §1 and §6: the <c>InitialOwnerAndAudit</c> migration and model drift (PC-2).</summary>
public sealed class MigrationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task InitialMigration_CreatesOnlyOwnerAndAuditEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var tables = await host.QueryAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name",
            r => r.GetString(0),
            ct);
        var migrations = await host.QueryAsync(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\"",
            r => r.GetString(0),
            ct);
        var trigger = await host.ScalarAsync<string>(
            "SELECT tgname::text FROM pg_trigger WHERE tgname = 'trg_audit_event_immutable' AND tgrelid = 'audit_event'::regclass",
            ct);

        Assert.Equal(new[] { "__EFMigrationsHistory", "audit_event", "owner" }, tables.Order(StringComparer.Ordinal));
        var migration = Assert.Single(migrations);
        Assert.EndsWith("_InitialOwnerAndAudit", migration, StringComparison.Ordinal);
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
