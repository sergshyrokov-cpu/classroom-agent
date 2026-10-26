using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-058 spec FR-001 … FR-003, FR-008, AC-001 … AC-006, AC-013: in a full read of a course, a stored item Google no
/// longer returns is kept with its submissions and marked "not in Classroom" with the run's instant; the mark keeps its
/// first instant while the item stays absent and goes when it comes back; only a full read changes marks; and a full
/// read stores the run's instant as the course's last successful read. The use case is the unit and the Classroom port
/// is substituted (TC-1, TC-4); that a failure <b>inside</b> the course's transaction rolls marks back is proven against
/// PostgreSQL in <c>CourseWorkPresenceSyncTests</c>.
/// </summary>
public sealed class CourseWorkPresenceTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Between two runs of a test — more than nothing, less than the weekly full run.</summary>
    private static readonly TimeSpan Later = TimeSpan.FromHours(1);

    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    private static GoogleReadFailedException OutOfRange() => new(GoogleReadFailureKind.ValueOutOfRange, null);

    private static GoogleReadFailedException CannotRead() =>
        new(GoogleReadFailureKind.Configuration, SyncDiagnosis.TechnicalAccountCannotRead);

    /// <summary>
    /// Course <paramref name="course"/> with one student and the given items of its own, each graded (maximum 10) with the
    /// student's submission graded <c>item + 1</c> — identities are the item ordinals, so they are unique across courses.
    /// </summary>
    private static SyncWorld.ClassroomReader WithCourse(
        SyncWorld.ClassroomReader reader,
        int course,
        int[] items,
        string state = CourseTestData.States.Active,
        DateTimeOffset? updateTime = null,
        Func<int, string>? title = null)
    {
        var id = CourseTestData.CourseId(course);
        reader.WithCourse(id, CourseTestData.CourseName(course), state, students: [Person(course)], updateTime: updateTime);
        foreach (var item in items)
        {
            reader
                .WithCourseWork(id, CourseWorkTestData.ItemId(item), CourseWorkResource.CourseWork, (title ?? CourseWorkTestData.Title)(item), T, maxPoints: 10m)
                .WithSubmission(id, CourseWorkTestData.ItemId(item), CourseTestData.UserId(course), CourseWorkTestData.SubmissionId(item), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: item + 1);
        }

        return reader;
    }

    private static CourseWork Item(SyncWorld world, int item, CourseWorkResource resource = CourseWorkResource.CourseWork) =>
        world.CourseWork.Stored.Values.Single(w => w.GoogleId == CourseWorkTestData.ItemId(item) && w.Resource == resource);

    private static Submission SubmissionOf(SyncWorld world, int item) =>
        world.Submissions.Stored.Values.Single(s => s.GoogleId == CourseWorkTestData.SubmissionId(item));

    /// <summary>Runs <paramref name="reader"/> as the next run and asserts the run completed; returns its instant.</summary>
    private static async Task<DateTimeOffset> RunAsync(SyncWorld world, SyncWorld.ClassroomReader reader, int ordinal, CancellationToken ct)
    {
        var at = world.Time.GetUtcNow();
        var outcome = await world.RunUsing(reader).ExecuteAsync(SyncWorld.RunId(ordinal), ct);
        Assert.False(outcome.Failed);
        return at;
    }

    /// <summary>
    /// AC-001, FR-001.2, FR-001.3, VR-004: Google returns only the first of two graded items — the second stays stored,
    /// marked with the run's instant, its title, maximum, submission state and grade unchanged; the first has no mark.
    /// </summary>
    [Fact]
    public async Task AnItemGoogleNoLongerReturns_IsKeptWithItsGrade_AndMarkedWithTheRunInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, WithCourse(new(), 1, [1, 2]), 1, ct);
        world.Time.Advance(Later);

        var second = await RunAsync(world, WithCourse(new(), 1, [1]), 2, ct);

        var absent = Item(world, 2);
        Assert.Equal(second, absent.NotInClassroomSince);
        Assert.Equal(CourseWorkTestData.Title(2), absent.Title);
        Assert.Equal(10m, absent.MaxPoints);
        var submission = SubmissionOf(world, 2);
        Assert.Equal(absent.Id, submission.CourseWorkId);
        Assert.Equal(SubmissionState.TurnedIn, submission.State);
        Assert.Equal(3m, submission.AssignedGrade);
        Assert.Null(Item(world, 1).NotInClassroomSince);
        Assert.Equal(2, world.CourseWork.Stored.Count);
    }

    /// <summary>AC-002, FR-001.2: a material Google no longer returns is kept and marked with the run's instant.</summary>
    [Fact]
    public async Task AMaterialGoogleNoLongerReturns_IsKept_AndMarked()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var first = WithCourse(new(), 1, [1]);
        first.WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(5), CourseWorkResource.CourseWorkMaterial, CourseWorkTestData.Title(5), T);
        await RunAsync(world, first, 1, ct);
        world.Time.Advance(Later);

        var second = await RunAsync(world, WithCourse(new(), 1, [1]), 2, ct);

        var material = Item(world, 5, CourseWorkResource.CourseWorkMaterial);
        Assert.Equal(second, material.NotInClassroomSince);
        Assert.Equal(CourseWorkTestData.Title(5), material.Title);
        Assert.Null(Item(world, 1).NotInClassroomSince);
    }

    /// <summary>
    /// FR-001.2: the key is (resource, Google id) — a material that disappears while a coursework item with the same
    /// Google id stays is marked, and the coursework item is not.
    /// </summary>
    [Fact]
    public async Task TheMark_FollowsTheResourceAndTheGoogleId_Together()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var first = WithCourse(new(), 1, [1]);
        first.WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWorkMaterial, CourseWorkTestData.Title(5), T);
        await RunAsync(world, first, 1, ct);
        world.Time.Advance(Later);

        var second = await RunAsync(world, WithCourse(new(), 1, [1]), 2, ct);

        Assert.Equal(second, Item(world, 1, CourseWorkResource.CourseWorkMaterial).NotInClassroomSince);
        Assert.Null(Item(world, 1).NotInClassroomSince);
    }

    /// <summary>
    /// AC-003, FR-002, PC-10: an item marked by an earlier run and still absent keeps the earlier run's instant — the same
    /// Google answer read again gives the same mark.
    /// </summary>
    [Fact]
    public async Task AMarkedItemStillAbsent_KeepsTheFirstInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, WithCourse(new(), 1, [1, 2]), 1, ct);
        world.Time.Advance(Later);
        var marked = await RunAsync(world, WithCourse(new(), 1, [1]), 2, ct);
        world.Time.Advance(Later);

        await RunAsync(world, WithCourse(new(), 1, [1]), 3, ct);

        Assert.Equal(marked, Item(world, 2).NotInClassroomSince);
        Assert.NotEqual(world.Time.GetUtcNow(), marked);
    }

    /// <summary>
    /// AC-004, FR-003: a marked item Google returns again, with a changed title, loses the mark and carries the new title.
    /// </summary>
    [Fact]
    public async Task AMarkedItemThatComesBack_LosesTheMark_AndIsUpdated()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, WithCourse(new(), 1, [1, 2]), 1, ct);
        world.Time.Advance(Later);
        await RunAsync(world, WithCourse(new(), 1, [1]), 2, ct);
        Assert.NotNull(Item(world, 2).NotInClassroomSince);
        world.Time.Advance(Later);

        await RunAsync(world, WithCourse(new(), 1, [1, 2], title: i => i == 2 ? "Back with a new title" : CourseWorkTestData.Title(i)), 3, ct);

        var back = Item(world, 2);
        Assert.Null(back.NotInClassroomSince);
        Assert.Equal("Back with a new title", back.Title);
        Assert.Equal(2, world.CourseWork.Stored.Count);
    }

    /// <summary>
    /// AC-005, FR-001.1: only a full read changes marks. Course 1, archived, is skipped as unchanged; course 2 is skipped
    /// for an out-of-range value. Google would no longer return an item of either: neither gains a mark, and the item
    /// each already had marked keeps its mark and its instant. The control: course 1 was not read at all in that run.
    /// </summary>
    [Fact]
    public async Task ACourseNotReadInFull_GainsAndLosesNoMark()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var t1 = T;
        var t2 = T.AddDays(1);
        await RunAsync(world, WithCourse(WithCourse(new(), 1, [11, 12, 13], CourseTestData.States.Archived, t1), 2, [21, 22, 23]), 1, ct);
        world.Time.Advance(Later);

        // Run 2 reads both courses in full (course 1's updateTime moved) and marks 13 and 23.
        var marked = await RunAsync(world, WithCourse(WithCourse(new(), 1, [11, 12], CourseTestData.States.Archived, t2), 2, [21, 22]), 2, ct);
        Assert.Equal(marked, Item(world, 13).NotInClassroomSince);
        Assert.Equal(marked, Item(world, 23).NotInClassroomSince);
        world.Time.Advance(Later);

        var third = WithCourse(WithCourse(new(), 1, [11], CourseTestData.States.Archived, t2), 2, [21]);
        third.WithCourseWorkFailure(CourseTestData.CourseId(2), OutOfRange());
        await RunAsync(world, third, 3, ct);

        Assert.DoesNotContain(CourseTestData.CourseId(1), third.CourseWorkRead);
        Assert.Null(Item(world, 12).NotInClassroomSince);
        Assert.Null(Item(world, 22).NotInClassroomSince);
        Assert.Equal(marked, Item(world, 13).NotInClassroomSince);
        Assert.Equal(marked, Item(world, 23).NotInClassroomSince);
    }

    /// <summary>
    /// AC-005, FR-001.1: a course skipped because Classroom reports it gone (<c>404</c>) also changes no mark.
    /// </summary>
    [Fact]
    public async Task ACourseReportedGone_GainsAndLosesNoMark()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, WithCourse(new(), 1, [1, 2, 3]), 1, ct);
        world.Time.Advance(Later);
        var marked = await RunAsync(world, WithCourse(new(), 1, [1, 2]), 2, ct);
        world.Time.Advance(Later);

        var third = WithCourse(new(), 1, [1]);
        third.WithSubmissionsFailure(CourseTestData.CourseId(1), new GoogleReadFailedException(GoogleReadFailureKind.CourseGone, null));
        await RunAsync(world, third, 3, ct);

        Assert.Null(Item(world, 2).NotInClassroomSince);
        Assert.Equal(marked, Item(world, 3).NotInClassroomSince);
    }

    /// <summary>
    /// AC-006, FR-001.5, SPEC_REVIEW m-4: the course starts with a marked item; the run's submissions read then fails with a
    /// non-retriable failure after its items were read, so the run stops. The item Google returns again stays marked and
    /// the item Google no longer returns gains no mark.
    /// </summary>
    [Fact]
    public async Task ARunStoppedByTheCourseRead_ChangesNoMarkOfThatCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, WithCourse(new(), 1, [1, 2]), 1, ct);
        world.Time.Advance(Later);
        var marked = await RunAsync(world, WithCourse(new(), 1, [1]), 2, ct);
        Assert.Equal(marked, Item(world, 2).NotInClassroomSince);
        world.Time.Advance(Later);

        var third = WithCourse(new(), 1, [2]);
        third.WithSubmissionsFailure(CourseTestData.CourseId(1), CannotRead());
        var outcome = await world.RunUsing(third).ExecuteAsync(SyncWorld.RunId(3), ct);

        Assert.True(outcome.Failed);
        Assert.Contains(CourseTestData.CourseId(1), third.CourseWorkRead);
        Assert.Equal(marked, Item(world, 2).NotInClassroomSince);
        Assert.Null(Item(world, 1).NotInClassroomSince);
    }

    /// <summary>
    /// AC-013, FR-008.1: a course read in full carries the run's instant as its last successful read, while a course
    /// skipped for an out-of-range value in the same run keeps the instant of the run that last read it.
    /// </summary>
    [Fact]
    public async Task AFullRead_StoresTheRunInstant_AndASkippedCourseKeepsItsOwn()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var first = await RunAsync(world, WithCourse(WithCourse(new(), 1, [1]), 2, [2]), 1, ct);
        Assert.Equal(first, world.Courses.Stored[CourseTestData.CourseId(2)].LastCompleteReadAt);
        world.Time.Advance(Later);

        var reader = WithCourse(WithCourse(new(), 1, [1]), 2, [2]);
        reader.WithSubmissionsFailure(CourseTestData.CourseId(2), OutOfRange());
        var second = await RunAsync(world, reader, 2, ct);

        Assert.Equal(second, world.Courses.Stored[CourseTestData.CourseId(1)].LastCompleteReadAt);
        Assert.Equal(first, world.Courses.Stored[CourseTestData.CourseId(2)].LastCompleteReadAt);
    }

    /// <summary>
    /// FR-008.1: a course skipped as an unchanged archive keeps its last-read instant — it was not read.
    /// </summary>
    [Fact]
    public async Task AnUnchangedArchive_KeepsItsLastReadInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var first = await RunAsync(world, WithCourse(new(), 1, [1], CourseTestData.States.Archived, T), 1, ct);
        world.Time.Advance(Later);

        var reader = WithCourse(new(), 1, [1], CourseTestData.States.Archived, T);
        await RunAsync(world, reader, 2, ct);

        Assert.DoesNotContain(CourseTestData.CourseId(1), reader.CourseWorkRead);
        Assert.Equal(first, world.Courses.Stored[CourseTestData.CourseId(1)].LastCompleteReadAt);
    }
}
