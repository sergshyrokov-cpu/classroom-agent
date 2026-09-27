using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-013 AC-003 and AC-006: the rules of the entity itself (entity model §1.2) — a run is begun once, finished
/// once, its counter is never negative, its end never precedes its start, and a long error message is shortened
/// rather than rejected (db-design §3.3). These are the invariants the check constraints cannot express.
/// </summary>
public sealed class SyncStateInvariantTests
{
    private static readonly DateTimeOffset Start = InstallationTestHost.DefaultStart;

    /// <summary>The first row starts as <c>Running</c>, with nothing finished and nothing failed.</summary>
    [Fact]
    public void AFirstRun_StartsAsRunning()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        Assert.Equal(SyncRunStatus.Running, state.Status);
        Assert.True(state.IsRunning);
        Assert.Equal(Start, state.StartedAt);
        Assert.Null(state.FinishedAt);
        Assert.Null(state.LastError);
        Assert.Null(state.LastSuccessfulRunAt);
        Assert.Equal(0, state.ProcessedCount);
    }

    /// <summary>A new run resets the counter and clears the previous error, keeping the last success.</summary>
    [Fact]
    public void ANewRun_ClearsTheErrorAndTheCounter_AndKeepsTheLastSuccess()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        state.CompleteRun(Start + TimeSpan.FromSeconds(3), 7);
        var lastSuccess = state.LastSuccessfulRunAt;

        state.BeginRun(SyncWorld.RunId(2), Start + TimeSpan.FromHours(1));

        Assert.Equal(SyncRunStatus.Running, state.Status);
        Assert.Equal(0, state.ProcessedCount);
        Assert.Null(state.LastError);
        Assert.Null(state.FinishedAt);
        Assert.Equal(lastSuccess, state.LastSuccessfulRunAt);
    }

    /// <summary>A terminal row cannot be completed again — a run is finished once.</summary>
    [Fact]
    public void CompletingATerminalRow_IsRejected()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        state.CompleteRun(Start + TimeSpan.FromSeconds(3), 0);

        Assert.ThrowsAny<ArgumentException>(() => state.CompleteRun(Start + TimeSpan.FromSeconds(4), 0));
    }

    /// <summary>A terminal row cannot be failed again either.</summary>
    [Fact]
    public void FailingATerminalRow_IsRejected()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        state.FailRun(Start + TimeSpan.FromSeconds(3), 0, SyncTestData.SyntheticError);

        Assert.ThrowsAny<ArgumentException>(
            () => state.FailRun(Start + TimeSpan.FromSeconds(4), 0, SyncTestData.SyntheticError));
    }

    /// <summary>The counter is never negative (spec VR-002).</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ANegativeCounter_IsRejected(int processedCount)
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        Assert.ThrowsAny<ArgumentException>(() => state.CompleteRun(Start + TimeSpan.FromSeconds(1), processedCount));
    }

    /// <summary>The end of a run never precedes its start (spec VR-002).</summary>
    [Fact]
    public void AnEndBeforeTheStart_IsRejected()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        Assert.ThrowsAny<ArgumentException>(() => state.CompleteRun(Start - TimeSpan.FromSeconds(1), 0));
    }

    /// <summary>A failure without a message is not a diagnosis (spec FR-008, VR-002).</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AFailureWithoutAMessage_IsRejected(string error)
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        Assert.ThrowsAny<ArgumentException>(() => state.FailRun(Start + TimeSpan.FromSeconds(1), 0, error));
    }

    /// <summary>
    /// db-design §3.3: a long message is <b>truncated</b>, not refused — a failing run must not fail again at
    /// the commit because its diagnosis was verbose.
    /// </summary>
    [Fact]
    public void ALongMessage_IsTruncatedToTheStoredLength()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        state.FailRun(Start + TimeSpan.FromSeconds(1), 0, new string('x', SyncState.MaxErrorLength + 100));

        Assert.Equal(SyncState.MaxErrorLength, state.LastError!.Length);
        Assert.Equal(512, SyncState.MaxErrorLength);
    }

    /// <summary>A completed run carries no error; a failed one carries no success (db-design §3.2).</summary>
    [Fact]
    public void TheTerminalFields_NeverDisagreeWithTheStatus()
    {
        var completed = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        completed.CompleteRun(Start + TimeSpan.FromSeconds(1), 3);
        var failed = SyncState.BeginFirstRun(SyncWorld.RunId(2), Start);
        failed.FailRun(Start + TimeSpan.FromSeconds(1), 1, SyncTestData.SyntheticError);

        Assert.Null(completed.LastError);
        Assert.NotNull(completed.FinishedAt);
        Assert.NotNull(failed.LastError);
        Assert.NotNull(failed.FinishedAt);
        Assert.Null(failed.LastSuccessfulRunAt);
    }
}
