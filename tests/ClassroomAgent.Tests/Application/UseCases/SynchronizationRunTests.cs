using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-013 AC-003 and AC-006: a run is recorded in <c>SyncState</c> and nowhere else — the status of the run in
/// progress, then its outcome; a completed run writes the instant of the last success, a failed one leaves that
/// instant alone and keeps a diagnosable message (spec FR-001, FR-002, FR-006, FR-008, I-1, I-2).
/// </summary>
public sealed class SynchronizationRunTests
{
    /// <summary>AC-003, I-1: a run with an empty pipeline completes with the counter at zero — that is success.</summary>
    [Fact]
    public async Task AFirstRun_CreatesTheRow_AndCompletesWithAZeroCounter()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var runId = SyncWorld.RunId(1);

        var outcome = await world.Run.ExecuteAsync(runId, ct);

        Assert.Equal(runId, outcome.RunId);
        Assert.Equal(0, outcome.ProcessedCount);
        Assert.False(outcome.Failed);
        var row = Assert.Single(world.States.Added);
        Assert.Equal(SyncRunStatus.Completed, row.Status);
        Assert.Equal(runId, row.RunId);
        Assert.Equal(0, row.ProcessedCount);
        Assert.Null(row.LastError);
    }

    /// <summary>AC-003: a completed run is the only writer of the instant of the last success (spec FR-008).</summary>
    [Fact]
    public async Task ACompletedRun_RecordsTheInstantOfTheLastSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var row = Assert.Single(world.States.Added);
        Assert.Equal(row.FinishedAt, row.LastSuccessfulRunAt);
        Assert.NotNull(row.LastSuccessfulRunAt);
    }

    /// <summary>AC-003: the row is one row, updated in place, however many runs happen (OD-004).</summary>
    [Fact]
    public async Task ASecondRun_UpdatesTheSameRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        world.Time.Advance(SyncTestData.DefaultInterval);
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Single(world.States.Added);
        Assert.Equal(SyncWorld.RunId(2), world.States.Stored!.RunId);
    }

    /// <summary>
    /// AC-003, FR-006: the run in progress is visible as <c>Running</c> before the outcome is written — two
    /// commits per run, the first one carrying the running status.
    /// </summary>
    [Fact]
    public async Task ARun_IsCommittedAsRunning_BeforeItsOutcome()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(2, world.Work.Commits);
        Assert.Equal(
            new SyncRunStatus?[] { SyncRunStatus.Running, SyncRunStatus.Completed },
            world.Work.CommittedStatuses);
    }

    /// <summary>
    /// AC-006: a failed run keeps the instant of the last success — the Owner sees both that the school last
    /// succeeded and that it has been failing since (spec FR-008).
    /// </summary>
    [Fact]
    public async Task AFailedRun_LeavesTheInstantOfTheLastSuccessUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var lastSuccess = world.States.Stored!.LastSuccessfulRunAt;
        world.Time.Advance(SyncTestData.DefaultInterval);

        world.States.Stored.BeginRun(SyncWorld.RunId(2), world.Time.GetUtcNow());
        world.States.Stored.FailRun(world.Time.GetUtcNow(), 0, SyncDiagnosis.GoogleUnavailable);

        Assert.Equal(SyncRunStatus.Failed, world.States.Stored.Status);
        Assert.Equal(lastSuccess, world.States.Stored.LastSuccessfulRunAt);
        Assert.Equal("GoogleUnavailable", world.States.Stored.LastError);
    }

    /// <summary>
    /// AC-006, US-017 spec FR-006: the stored value is a diagnosis code from the closed list, never a payload (spec
    /// S-06, SC-10).
    /// </summary>
    [Fact]
    public async Task AFailedRun_StoresACodeOfTheClosedList()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        world.States.Stored!.BeginRun(SyncWorld.RunId(2), world.Time.GetUtcNow());
        world.States.Stored.FailRun(world.Time.GetUtcNow(), 0, SyncDiagnosis.KeyRejected);

        Assert.NotNull(world.States.Stored.LastError);
        Assert.Contains(world.States.Stored.LastError, Enum.GetNames<SyncDiagnosis>());
        Assert.True(world.States.Stored.LastError!.Length <= 512);
    }

    /// <summary>AC-003, FR-005: the guard is consulted before the row is ever read.</summary>
    [Fact]
    public async Task TheGuard_IsConsultedBeforeAnythingIsRead()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal([RunSynchronizationUseCase.Operation], world.ReadOnly.Operations);
        Assert.Equal(SyncTestData.Operation, RunSynchronizationUseCase.Operation);
    }
}
