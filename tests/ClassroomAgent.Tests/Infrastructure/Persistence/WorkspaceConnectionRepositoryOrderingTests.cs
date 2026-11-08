using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Infrastructure.Persistence.Repositories;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-077 AC-009, spec FR-008, against real PostgreSQL (TC-2): reading the singleton connection, for display and for
/// update, raises no <c>FirstWithoutOrderByAndFilterWarning</c> — the context here turns that warning into an exception —
/// returns the same single row, and keeps today's tracking.
/// </summary>
public sealed class WorkspaceConnectionRepositoryOrderingTests(PostgreSqlFixture database)
{
    private const string TechnicalAccount = AccessCheckTestData.TechnicalAccount;

    private static ClassroomAgentDbContext StrictContext(string connectionString) =>
        new((DbContextOptions<ClassroomAgentDbContext>)ClassroomAgentDbContextOptions
            .Configure(new DbContextOptionsBuilder<ClassroomAgentDbContext>(), connectionString)
            .ConfigureWarnings(w => w
                .Ignore(RelationalEventId.PendingModelChangesWarning)
                .Throw(CoreEventId.FirstWithoutOrderByAndFilterWarning))
            .Options);

    /// <summary>AC-009, FR-008: the read path raises no warning, returns the row and leaves it untracked.</summary>
    [Fact]
    public async Task ReadingForDisplay_RaisesNoWarning_AndReturnsTheRowUntracked()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertWorkspaceConnectionAsync(ct, impersonationUserEmail: TechnicalAccount);
        await using var db = StrictContext(host.ConnectionString);

        var connection = await new WorkspaceConnectionRepository(db).GetForReadAsync(ct);

        Assert.NotNull(connection);
        Assert.Equal(TechnicalAccount, connection.ImpersonationUserEmail);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    /// <summary>AC-009, FR-008: the update path raises no warning, returns the row and tracks it.</summary>
    [Fact]
    public async Task ReadingForUpdate_RaisesNoWarning_AndReturnsTheRowTracked()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertWorkspaceConnectionAsync(ct, impersonationUserEmail: TechnicalAccount);
        await using var db = StrictContext(host.ConnectionString);

        var connection = await new WorkspaceConnectionRepository(db).GetForUpdateAsync(ct);

        Assert.NotNull(connection);
        Assert.Equal(TechnicalAccount, connection.ImpersonationUserEmail);
        Assert.Single(db.ChangeTracker.Entries());
    }

    /// <summary>FR-008: with no row saved yet, both reads answer null, still without the warning.</summary>
    [Fact]
    public async Task WithNoRow_BothReadsAnswerNull_WithoutTheWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var db = StrictContext(host.ConnectionString);
        var repository = new WorkspaceConnectionRepository(db);

        Assert.Null(await repository.GetForReadAsync(ct));
        Assert.Null(await repository.GetForUpdateAsync(ct));
    }
}
