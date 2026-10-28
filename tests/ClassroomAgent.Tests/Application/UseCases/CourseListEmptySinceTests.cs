using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-062 FR-008.1, FR-008.3, AC-008, AC-016: the run use case with every port in memory (<see cref="SyncWorld"/>,
/// TC-1) stores the instant since which course lists have come back empty in a row — the start of the first empty run
/// of a streak — keeps it through later empty runs, clears it with a non-empty list, and an empty run still counts as a
/// successful one (no staleness). Course data is synthetic (TC-4).
/// </summary>
public sealed class CourseListEmptySinceTests
{
    private static readonly TimeSpan Later = TimeSpan.FromHours(6);

    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    private static SyncWorld.ClassroomReader Listing(params int[] courses)
    {
        var reader = new SyncWorld.ClassroomReader();
        foreach (var n in courses)
        {
            reader.WithCourse(
                CourseTestData.CourseId(n), CourseTestData.CourseName(n), CourseTestData.States.Active, students: [Person(n)],
                creationTime: InstallationTestHost.DefaultStart, updateTime: InstallationTestHost.DefaultStart);
        }

        return reader;
    }

    private static async Task<(DateTimeOffset At, SynchronizationRunOutcome Outcome)> RunAsync(
        SyncWorld world, SyncWorld.ClassroomReader reader, int ordinal, CancellationToken ct)
    {
        var at = world.Time.GetUtcNow();
        var outcome = await world.RunUsing(reader).ExecuteAsync(SyncWorld.RunId(ordinal), ct);
        world.Time.Advance(Later);
        return (at, outcome);
    }

    /// <summary>
    /// AC-008, FR-008.1, FR-008.3, I-11: stored courses, then three empty runs from E1 — the date is E1 throughout, each
    /// run completes and its finish is the last success (so X2 cannot be raised while such runs keep coming).
    /// </summary>
    [Fact]
    public async Task ThreeEmptyRuns_KeepTheFirstRunsStart_AndEachCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);

        var (e1, first) = await RunAsync(world, new(), 2, ct);
        Assert.Equal(e1, world.States.Stored!.CourseListEmptySince);
        await RunAsync(world, new(), 3, ct);
        var (e3, third) = await RunAsync(world, new(), 4, ct);

        Assert.False(first.Failed);
        Assert.False(third.Failed);
        Assert.Equal(e1, world.States.Stored.CourseListEmptySince);
        Assert.True(world.States.Stored.LastCourseListEmpty);
        Assert.Equal(SyncRunStatus.Completed, world.States.Stored.Status);
        Assert.True(world.States.Stored.LastSuccessfulRunAt >= e3);
    }

    /// <summary>AC-008, AC-016, FR-008.1: a run reading a non-empty list clears flag and date together.</summary>
    [Fact]
    public async Task ANonEmptyList_ClearsTheDateAndTheFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1), 1, ct);
        await RunAsync(world, new(), 2, ct);
        Assert.NotNull(world.States.Stored!.CourseListEmptySince);

        await RunAsync(world, Listing(1), 3, ct);

        Assert.Null(world.States.Stored.CourseListEmptySince);
        Assert.False(world.States.Stored.LastCourseListEmpty);
    }

    /// <summary>FR-008.1, db-design §2: an empty list while no course is stored is not a problem — no date is set.</summary>
    [Fact]
    public async Task AnEmptyListWithNoCourseStored_SetsNoDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();

        var (_, outcome) = await RunAsync(world, new(), 1, ct);

        Assert.False(outcome.Failed);
        Assert.Null(world.States.Stored!.CourseListEmptySince);
        Assert.False(world.States.Stored.LastCourseListEmpty);
    }
}
