using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-013 AC-003 and AC-006: the rules of the entity itself (entity model §1.2) — a run is begun once, finished
/// once, its counter is never negative, its end never precedes its start. US-017 spec FR-006 replaces the free-text
/// error with a closed diagnosis list: a failure stores exactly a <see cref="SyncDiagnosis"/> name and an undeclared
/// value is refused. These are the invariants the check constraints cannot express.
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
        state.CompleteRun(Start + TimeSpan.FromSeconds(3), 7, Start + TimeSpan.FromSeconds(3));
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
        state.CompleteRun(Start + TimeSpan.FromSeconds(3), 0, Start + TimeSpan.FromSeconds(3));

        Assert.ThrowsAny<ArgumentException>(() => state.CompleteRun(Start + TimeSpan.FromSeconds(4), 0, Start + TimeSpan.FromSeconds(4)));
    }

    /// <summary>A terminal row cannot be failed again either.</summary>
    [Fact]
    public void FailingATerminalRow_IsRejected()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        state.FailRun(Start + TimeSpan.FromSeconds(3), 0, SyncDiagnosis.Unexpected, SyncStep.Classroom);

        Assert.ThrowsAny<ArgumentException>(
            () => state.FailRun(Start + TimeSpan.FromSeconds(4), 0, SyncDiagnosis.Unexpected, SyncStep.Classroom));
    }

    /// <summary>The counter is never negative (spec VR-002).</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ANegativeCounter_IsRejected(int processedCount)
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        Assert.ThrowsAny<ArgumentException>(() => state.CompleteRun(Start + TimeSpan.FromSeconds(1), processedCount, Start + TimeSpan.FromSeconds(1)));
    }

    /// <summary>The end of a run never precedes its start (spec VR-002).</summary>
    [Fact]
    public void AnEndBeforeTheStart_IsRejected()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        Assert.ThrowsAny<ArgumentException>(() => state.CompleteRun(Start - TimeSpan.FromSeconds(1), 0, Start - TimeSpan.FromSeconds(1)));
    }

    /// <summary>The eight diagnoses of US-017 spec FR-006, for the theories below.</summary>
    public static TheoryData<SyncDiagnosis> AllDiagnoses => new(Enum.GetValues<SyncDiagnosis>());

    /// <summary>
    /// US-017 spec FR-006, VR-002: the closed list is exactly the eight names, so a theory over every value covers
    /// the whole list. A declaration guard — it holds before the behaviour exists.
    /// </summary>
    [Fact]
    public void TheClosedList_IsExactlyTheEightNames()
    {
        Assert.Equal(
            [
                "ScopeNotAuthorized",
                "TechnicalAccountUnknown",
                "TechnicalAccountCannotRead",
                "ApiNotEnabled",
                "KeyUnavailable",
                "KeyRejected",
                "GoogleUnavailable",
                "Unexpected",
            ],
            Enum.GetNames<SyncDiagnosis>());
    }

    /// <summary>
    /// US-017 spec FR-006, db-design §2: a failed run stores exactly the diagnosis name — never an exception type
    /// name, never text.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllDiagnoses))]
    public void AFailure_StoresExactlyTheDiagnosisName(SyncDiagnosis diagnosis)
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        state.FailRun(Start + TimeSpan.FromSeconds(1), 2, diagnosis, SyncStep.Classroom);

        Assert.Equal(SyncRunStatus.Failed, state.Status);
        Assert.Equal(diagnosis.ToString(), state.LastError);
        Assert.Equal(2, state.ProcessedCount);
        Assert.Equal(Start + TimeSpan.FromSeconds(1), state.FinishedAt);
    }

    /// <summary>
    /// US-017 entity model §2: a value outside the declared list is refused and the row is left exactly as it was,
    /// still running — so the run can still be failed with a real diagnosis.
    /// </summary>
    [Fact]
    public void AnUndeclaredDiagnosis_IsRefused_AndTheStateIsUnchanged()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => state.FailRun(Start + TimeSpan.FromSeconds(1), 5, (SyncDiagnosis)99, SyncStep.Classroom));

        Assert.Equal(SyncRunStatus.Running, state.Status);
        Assert.Null(state.FinishedAt);
        Assert.Null(state.LastError);
        Assert.Equal(0, state.ProcessedCount);

        state.FailRun(Start + TimeSpan.FromSeconds(2), 0, SyncDiagnosis.Unexpected, SyncStep.Classroom);
        Assert.Equal("Unexpected", state.LastError);
    }

    /// <summary>A completed run carries no error; a failed one carries no success (db-design §3.2).</summary>
    [Fact]
    public void TheTerminalFields_NeverDisagreeWithTheStatus()
    {
        var completed = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        completed.CompleteRun(Start + TimeSpan.FromSeconds(1), 3, Start + TimeSpan.FromSeconds(1));
        var failed = SyncState.BeginFirstRun(SyncWorld.RunId(2), Start);
        failed.FailRun(Start + TimeSpan.FromSeconds(1), 1, SyncDiagnosis.GoogleUnavailable, SyncStep.Classroom);

        Assert.Null(completed.LastError);
        Assert.NotNull(completed.FinishedAt);
        Assert.NotNull(failed.LastError);
        Assert.NotNull(failed.FinishedAt);
        Assert.Null(failed.LastSuccessfulRunAt);
    }
}
