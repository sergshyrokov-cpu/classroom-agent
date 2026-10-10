using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-017 AC-006, AC-013 (spec FR-007, FR-006, VR-002, I-6; db-design §4): the "Last synchronization" query maps the
/// single <c>sync_state</c> row to a <see cref="LastSynchronizationView"/> — never run, running, completed, failed
/// with a diagnosis — and shows anything that is not a declared name as <c>Unexpected</c>. The repository is the
/// in-memory one of <see cref="SyncWorld"/> (TC-1); the same mapping over PostgreSQL is
/// <c>SyncDiagnosisPersistenceTests</c>.
/// </summary>
public sealed class GetLastSynchronizationQueryTests
{
    private static readonly DateTimeOffset Start = InstallationTestHost.DefaultStart;

    public static TheoryData<SyncDiagnosis> AllDiagnoses => new(Enum.GetValues<SyncDiagnosis>());

    private static GetLastSynchronizationQuery QueryOver(SyncWorld world) =>
        new(world.States, new SchoolTimeZone(JournalTestData.Kyiv));

    /// <summary>AC-006: before any run the row is absent and the view says so, with nothing to show.</summary>
    [Fact]
    public async Task WithNoRow_TheView_IsNeverRun()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        Assert.Null(world.States.Stored);

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Equal(LastSynchronizationStatus.NeverRun, view.Status);
        Assert.Null(view.StartedAt);
        Assert.Null(view.FinishedAt);
        Assert.Null(view.LastSuccessfulRunAt);
        Assert.Null(view.Diagnosis);
    }

    /// <summary>AC-006: a run in progress shows when it started and no end; no diagnosis.</summary>
    [Fact]
    public async Task ARunInProgress_TheView_IsRunning_WithItsStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.States.Add(SyncState.BeginFirstRun(SyncWorld.RunId(1), Start));

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Equal(LastSynchronizationStatus.Running, view.Status);
        Assert.Equal(Start, view.StartedAt);
        Assert.Null(view.FinishedAt);
        Assert.Null(view.LastSuccessfulRunAt);
        Assert.Null(view.Diagnosis);
    }

    /// <summary>AC-006: a completed run shows its end and the instant of the last success, which is the same.</summary>
    [Fact]
    public async Task ACompletedRun_TheView_IsCompleted_WithItsEndAndTheLastSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        world.States.Add(state);
        var end = Start + TimeSpan.FromSeconds(42);
        state.CompleteRun(end, 3, end);

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Equal(LastSynchronizationStatus.Completed, view.Status);
        Assert.Equal(Start, view.StartedAt);
        Assert.Equal(end, view.FinishedAt);
        Assert.Equal(end, view.LastSuccessfulRunAt);
        Assert.Null(view.Diagnosis);
    }

    /// <summary>AC-006: a first run that failed shows its diagnosis, its end and no last success.</summary>
    [Theory]
    [MemberData(nameof(AllDiagnoses))]
    public async Task AFailedRun_TheView_IsFailed_WithThatDiagnosis(SyncDiagnosis diagnosis)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        world.States.Add(state);
        var end = Start + TimeSpan.FromSeconds(7);
        state.FailRun(end, 1, diagnosis, SyncStep.Classroom);

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Equal(LastSynchronizationStatus.Failed, view.Status);
        Assert.Equal(diagnosis, view.Diagnosis);
        Assert.Equal(Start, view.StartedAt);
        Assert.Equal(end, view.FinishedAt);
        Assert.Null(view.LastSuccessfulRunAt);
    }

    /// <summary>AC-006, spec FR-007: a failure after an earlier success still shows when the last success was.</summary>
    [Fact]
    public async Task AFailedRun_AfterASuccess_KeepsTheLastSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        world.States.Add(state);
        var success = Start + TimeSpan.FromSeconds(5);
        state.CompleteRun(success, 2, success);
        var secondStart = Start + TimeSpan.FromHours(1);
        state.BeginRun(SyncWorld.RunId(2), secondStart);
        var failure = secondStart + TimeSpan.FromSeconds(3);
        state.FailRun(failure, 0, SyncDiagnosis.GoogleUnavailable, SyncStep.Classroom);

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Equal(LastSynchronizationStatus.Failed, view.Status);
        Assert.Equal(SyncDiagnosis.GoogleUnavailable, view.Diagnosis);
        Assert.Equal(secondStart, view.StartedAt);
        Assert.Equal(failure, view.FinishedAt);
        Assert.Equal(success, view.LastSuccessfulRunAt);
    }

    /// <summary>
    /// AC-013, FR-006, VR-002, I-6: a value an earlier version stored, or anything that is not exactly a declared
    /// name — numeric text, another casing, padding — is shown as <c>Unexpected</c>. The control (the exact name) maps
    /// to itself, inside the same test, so the legacy outcome is not a default for every value.
    /// </summary>
    [Theory]
    [InlineData("RunFailed:HttpRequestException")]
    [InlineData("3")]
    [InlineData("0")]
    [InlineData("scopenotauthorized")]
    [InlineData("SCOPENOTAUTHORIZED")]
    [InlineData("ScopeNotAuthorized ")]
    [InlineData("Transient:the synthetic port refused this run")]
    public async Task AValueThatIsNotADeclaredName_IsShownAsUnexpected(string stored)
    {
        var ct = TestContext.Current.CancellationToken;
        var control = new SyncWorld();
        control.States.Add(FailedRowHolding("ScopeNotAuthorized"));
        var controlView = await QueryOver(control).ExecuteAsync(ct);
        Assert.Equal(SyncDiagnosis.ScopeNotAuthorized, controlView.Diagnosis);

        var world = new SyncWorld();
        world.States.Add(FailedRowHolding(stored));

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Equal(LastSynchronizationStatus.Failed, view.Status);
        Assert.Equal(SyncDiagnosis.Unexpected, view.Diagnosis);
    }

    /// <summary>
    /// A failed row holding an arbitrary <c>last_error</c>, as an earlier version could have left it. The entity no
    /// longer has a way to write free text (FailRun takes a <see cref="SyncDiagnosis"/>), so the private setter is
    /// set the way EF Core materialises it.
    /// </summary>
    private static SyncState FailedRowHolding(string lastError)
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        state.FailRun(Start + TimeSpan.FromSeconds(1), 0, SyncDiagnosis.Unexpected, SyncStep.Classroom);
        typeof(SyncState).GetProperty(nameof(SyncState.LastError))!.SetValue(state, lastError);
        return state;
    }
}
