using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MeetTestData;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-031 AC-007, AC-008 (use-case side) (spec FR-001, FR-007, FR-009, FR-010, I-5, OD-006): what a final failure of
/// the Meet step does to the run. The retry itself is the adapter's (<c>GoogleMeetReportsReaderRetryTests</c>); here the
/// port reports a failure that is already final, exactly as <see cref="GoogleReadFailedException"/> carries it.
/// </summary>
public sealed class MeetStepFailureTests
{
    private const string Marker = "SECRET-DETAIL-31a7";

    public static TheoryData<SyncDiagnosis> ConfigurationDiagnoses => new(
        SyncDiagnosis.ScopeNotAuthorized,
        SyncDiagnosis.TechnicalAccountUnknown,
        SyncDiagnosis.TechnicalAccountCannotRead,
        SyncDiagnosis.ApiNotEnabled,
        SyncDiagnosis.KeyUnavailable,
        SyncDiagnosis.KeyRejected);

    private static DateTimeOffset Yesterday(SyncWorld world) => world.Time.GetUtcNow() - TimeSpan.FromDays(1);

    private static SyncWorld WorldWithOneCourse()
    {
        var world = new SyncWorld();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));
        return world;
    }

    /// <summary>
    /// AC-007, FR-010: a configuration failure of the Meet step stops the run at once — the port is called once — with the
    /// "Check access" code and the Meet step; the Classroom data of the run stays; the watermark does not move and the
    /// run is not a success.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConfigurationDiagnoses))]
    public async Task AConfigurationFailureOfTheMeetStep_FailsTheRun_NamingTheMeetStep(SyncDiagnosis diagnosis)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = WorldWithOneCourse();
        world.Meet.FailOnFirstPage = new GoogleReadFailedException(GoogleReadFailureKind.Configuration, diagnosis);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        Assert.Equal(SyncStep.Meet, outcome.FailedStep);
        var state = world.States.Stored!;
        Assert.Equal(SyncRunStatus.Failed, state.Status);
        Assert.Equal(diagnosis.ToString(), state.LastError);
        Assert.Equal(SyncStep.Meet, state.FailedStep);
        Assert.Null(state.MeetLoadedUpTo);
        Assert.Null(state.LastSuccessfulRunAt);
        Assert.Single(world.Meet.Calls);
        Assert.Contains(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.Equal(1, state.ProcessedCount);
    }

    /// <summary>FR-010: a final transient failure of the Meet step fails the run as <c>GoogleUnavailable</c>, step Meet.</summary>
    [Fact]
    public async Task AFinalTransientFailureOfTheMeetStep_IsGoogleUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = WorldWithOneCourse();
        world.Meet.FailOnFirstPage =
            new GoogleReadFailedException(GoogleReadFailureKind.Transient, SyncDiagnosis.GoogleUnavailable);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        Assert.Equal("GoogleUnavailable", world.States.Stored!.LastError);
        Assert.Equal(SyncStep.Meet, world.States.Stored.FailedStep);
    }

    /// <summary>FR-010, SC-10: an unexpected exception in the Meet step is stored as <c>Unexpected</c>, with no detail of it.</summary>
    [Fact]
    public async Task AnUnexpectedExceptionInTheMeetStep_IsStoredAsUnexpected_WithNoDetail()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = WorldWithOneCourse();
        world.Meet.FailOnFirstPage = new InvalidOperationException(Marker);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        var state = world.States.Stored!;
        Assert.Equal("Unexpected", state.LastError);
        Assert.DoesNotContain(Marker, state.LastError, StringComparison.Ordinal);
        Assert.Equal(SyncStep.Meet, state.FailedStep);
        Assert.Equal(nameof(InvalidOperationException), outcome.UnexpectedExceptionType);
    }

    /// <summary>FR-010, I-5: a run the Classroom step stops records the Classroom step, and the Meet port is not called.</summary>
    [Fact]
    public async Task AClassroomFailure_RecordsTheClassroomStep()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = WorldWithOneCourse();
        world.Classroom.FailOnListing =
            new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.ScopeNotAuthorized);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        Assert.Equal(SyncStep.Classroom, outcome.FailedStep);
        Assert.Equal(SyncStep.Classroom, world.States.Stored!.FailedStep);
        Assert.Empty(world.Meet.Calls);
    }

    /// <summary>
    /// FR-009: a failure on the second page keeps what the first page committed; nothing of the second page is stored and
    /// the watermark does not move.
    /// </summary>
    [Fact]
    public async Task AFailureOnTheSecondPage_KeepsTheFirstPage_AndLeavesTheWatermark()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var at = Yesterday(world);
        world.Meet
            .WithPage(Event(1, 1, at, 600, Teacher(1), Teacher(1)))
            .WithPage(Event(2, 2, at, 600, Teacher(2), Teacher(2)));
        world.Meet.FailAfterPages = 1;
        world.Meet.FailAfterPagesWith =
            new GoogleReadFailedException(GoogleReadFailureKind.Transient, SyncDiagnosis.GoogleUnavailable);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        Assert.Contains(ConferenceId(1), world.MeetSessions.Stored.Keys);
        Assert.DoesNotContain(ConferenceId(2), world.MeetSessions.Stored.Keys);
        Assert.Null(world.States.Stored!.MeetLoadedUpTo);
        Assert.Equal(SyncStep.Meet, world.States.Stored.FailedStep);
    }

    /// <summary>FR-010: a completed run after a Meet failure clears the failed step and moves the watermark.</summary>
    [Fact]
    public async Task ACompletedRun_AfterAMeetFailure_ClearsTheStep()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Meet.FailOnFirstPage =
            new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.ApiNotEnabled);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Equal(SyncStep.Meet, world.States.Stored!.FailedStep);

        world.Time.Advance(SyncTestData.DefaultInterval);
        world.Meet.Reset();
        var now = world.Time.GetUtcNow();
        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.False(outcome.Failed);
        Assert.Null(outcome.FailedStep);
        var state = world.States.Stored;
        Assert.Equal(SyncRunStatus.Completed, state.Status);
        Assert.Null(state.FailedStep);
        Assert.Null(state.LastError);
        Assert.Equal(now, state.MeetLoadedUpTo);
    }

    /// <summary>
    /// Spec §8, FR-007: a host stop during the Meet step is not a failed run — the cancellation propagates, the row stays
    /// running and the watermark is not written.
    /// </summary>
    [Fact]
    public async Task AHostStopDuringTheMeetStep_IsNotAFailure_AndWritesNoWatermark()
    {
        var world = new SyncWorld();
        var at = Yesterday(world);
        world.Meet
            .WithPage(Event(1, 1, at, 600, Teacher(1), Teacher(1)))
            .WithPage(Event(2, 2, at, 600, Teacher(2), Teacher(2)));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        world.Meet.AfterPage = _ => stop.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => world.Run.ExecuteAsync(SyncWorld.RunId(1), stop.Token));

        Assert.Single(world.Meet.Calls);
        var state = world.States.Stored!;
        Assert.Equal(SyncRunStatus.Running, state.Status);
        Assert.Null(state.MeetLoadedUpTo);
    }
}
