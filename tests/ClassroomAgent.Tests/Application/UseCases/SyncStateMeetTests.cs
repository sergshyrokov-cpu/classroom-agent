using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-031 entity model §4 (spec FR-007, FR-010, VR-002, I-5): the watermark is written by a completed run only, never
/// later than its own finish and never backwards; a failed run records its step and keeps the watermark; a new run
/// clears the step.
/// </summary>
public sealed class SyncStateMeetTests
{
    private static readonly DateTimeOffset Start = InstallationTestHost.DefaultStart;

    private static DateTimeOffset At(int seconds) => Start + TimeSpan.FromSeconds(seconds);

    private static SyncState CompletedAt(int seconds)
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        state.CompleteRun(At(seconds), 0, At(seconds));
        return state;
    }

    [Fact]
    public void ANewRow_HasNoWatermarkAndNoFailedStep()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        Assert.Null(state.MeetLoadedUpTo);
        Assert.Null(state.FailedStep);
    }

    [Fact]
    public void ACompletedRun_WritesTheWatermark()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        state.CompleteRun(At(10), 3, At(2));

        Assert.Equal(At(2), state.MeetLoadedUpTo);
        Assert.Equal(At(10), state.LastSuccessfulRunAt);
        Assert.Null(state.FailedStep);
        Assert.Equal(SyncRunStatus.Completed, state.Status);
    }

    /// <summary>VR-002: never later than the instant it is written.</summary>
    [Fact]
    public void AWatermarkLaterThanTheFinish_IsRefused()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        Assert.ThrowsAny<ArgumentException>(() => state.CompleteRun(At(10), 0, At(11)));
        Assert.Equal(SyncRunStatus.Running, state.Status);
    }

    /// <summary>VR-002: never moved backwards by a successful run.</summary>
    [Fact]
    public void AWatermarkEarlierThanTheStoredOne_IsRefused()
    {
        var state = CompletedAt(10);
        state.BeginRun(SyncWorld.RunId(2), At(20));

        Assert.ThrowsAny<ArgumentException>(() => state.CompleteRun(At(30), 0, At(9)));
        Assert.Equal(At(10), state.MeetLoadedUpTo);
    }

    /// <summary>FR-007, FR-010: a failed run records its step and leaves the watermark where it was.</summary>
    [Theory]
    [InlineData(SyncStep.Classroom)]
    [InlineData(SyncStep.Meet)]
    public void AFailedRun_RecordsItsStep_AndKeepsTheWatermark(SyncStep step)
    {
        var state = CompletedAt(10);
        state.BeginRun(SyncWorld.RunId(2), At(20));

        state.FailRun(At(30), 0, SyncDiagnosis.ScopeNotAuthorized, step);

        Assert.Equal(step, state.FailedStep);
        Assert.Equal(At(10), state.MeetLoadedUpTo);
        Assert.Equal("ScopeNotAuthorized", state.LastError);
        Assert.Equal(SyncRunStatus.Failed, state.Status);
    }

    /// <summary>I-5: a new run clears the step of the failed one before it; the watermark stays.</summary>
    [Fact]
    public void ANewRun_ClearsTheFailedStep_AndKeepsTheWatermark()
    {
        var state = CompletedAt(10);
        state.BeginRun(SyncWorld.RunId(2), At(20));
        state.FailRun(At(30), 0, SyncDiagnosis.GoogleUnavailable, SyncStep.Meet);

        state.BeginRun(SyncWorld.RunId(3), At(40));

        Assert.Null(state.FailedStep);
        Assert.Equal(At(10), state.MeetLoadedUpTo);
    }

    [Fact]
    public void AStepTheEnumDoesNotDeclare_IsRefused()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        Assert.ThrowsAny<ArgumentException>(
            () => state.FailRun(At(1), 0, SyncDiagnosis.Unexpected, (SyncStep)9));
        Assert.Equal(SyncRunStatus.Running, state.Status);
    }
}
