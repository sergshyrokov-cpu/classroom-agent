using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-013 AC-004 and AC-005: in read-only mode and without a usable connection no run happens, nothing is
/// written and nothing reaches Google. The refusal is decided in <c>Application</c> by the guard, which is
/// consulted first (spec FR-005, FR-007, FR-009; AD-6, SC-5, BR-026).
/// </summary>
public sealed class SynchronizationRefusalTests
{
    /// <summary>AC-004: read-only mode refuses the run and the outcome says why.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheRunIsSkipped_WithItsReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(readOnly: true);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.NotNull(outcome.ReadOnlyReason);
        Assert.Null(outcome.RunId);
        Assert.Null(outcome.ConnectionState);
    }

    /// <summary>
    /// AC-004: <b>nothing is written</b> — a synchronization write is not on the BR-026 closed list, so the row
    /// is neither created nor updated and no commit happens at all.
    /// </summary>
    [Fact]
    public async Task InReadOnlyMode_NothingIsWritten()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(readOnly: true);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Empty(world.States.Added);
        Assert.Null(world.States.Stored);
        Assert.Equal(0, world.Work.Commits);
        Assert.Equal(0, world.Work.Transactions);
    }

    /// <summary>AC-004: the guard is asked first, so nothing else is even read.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheGuardIsAskedFirst_AndNothingElseIsRead()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(readOnly: true);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Single(world.ReadOnly.Operations);
        Assert.Equal(0, world.States.Reads);
        Assert.Equal(0, world.Connections.Reads);
    }

    /// <summary>
    /// AC-004: leaving read-only mode resumes synchronization without a restart — the same use case instance
    /// refuses and then runs.
    /// </summary>
    [Fact]
    public async Task WhenReadOnlyModeEnds_TheNextRunHappens()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(readOnly: true);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        world.ReadOnly.IsReadOnly = false;
        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Equal(SyncWorld.RunId(2), outcome.RunId);
        Assert.Single(world.States.Added);
    }

    /// <summary>AC-005: no connection has ever been saved, so there is nothing to synchronize through.</summary>
    [Fact]
    public async Task WithoutAConnection_TheRunIsSkipped_AndNothingIsWritten()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(connection: SeededConnection.None);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(WorkspaceConnectionState.NotConfigured, outcome.ConnectionState);
        Assert.Null(outcome.RunId);
        Assert.Empty(world.States.Added);
        Assert.Equal(0, world.Work.Commits);
    }

    /// <summary>
    /// AC-005: a connection saved for a domain that is no longer the installation's is not usable either
    /// (US-009 spec FR-002 — BR-021 says this cannot legitimately happen, and it is never used when it does).
    /// </summary>
    [Fact]
    public async Task WithAConnectionForAnotherDomain_TheRunIsSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(connection: SeededConnection.ForAnotherDomain);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(WorkspaceConnectionState.DomainMismatch, outcome.ConnectionState);
        Assert.Empty(world.States.Added);
    }

    /// <summary>
    /// AC-004, AC-005: the two skips are told apart by the outcome — read-only carries a reason and no
    /// connection state, a missing connection the other way round (spec FR-005).
    /// </summary>
    [Fact]
    public async Task TheTwoSkips_AreDistinguishable()
    {
        var ct = TestContext.Current.CancellationToken;
        var readOnly = await new SyncWorld(readOnly: true).Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var unconfigured = await new SyncWorld(connection: SeededConnection.None).Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.NotNull(readOnly.ReadOnlyReason);
        Assert.Null(readOnly.ConnectionState);
        Assert.Null(unconfigured.ReadOnlyReason);
        Assert.NotNull(unconfigured.ConnectionState);
    }

    /// <summary>AC-004: the guard sees this use case's own operation name, not a borrowed one (US-007 VR-001).</summary>
    [Fact]
    public async Task TheRefusal_CarriesThisUseCasesOperationName()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(readOnly: true);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(SyncTestData.Operation, Assert.Single(world.ReadOnly.Operations));
    }
}
