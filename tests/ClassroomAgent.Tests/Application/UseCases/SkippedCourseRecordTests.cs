using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-058 spec FR-005 … FR-007, AC-007 … AC-011, AC-014, AC-015: a course the run skips for out-of-range values or an
/// unknown course state is recorded with its Google id, its list name, the reason and the instant the streak began;
/// a repeated skip keeps the instant; the record goes when the course is read in full, is aged out, or has left a
/// course list the run went through to its end; no other skip creates a record and no other outcome removes one. The
/// use case is the unit and the Classroom port is substituted (TC-1, TC-4); the table itself is proven in
/// <c>SyncSkippedCoursePersistenceTests</c>.
/// </summary>
public sealed class SkippedCourseRecordTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Later = TimeSpan.FromHours(1);

    /// <summary>A course state the program does not know (US-014 VR-002).</summary>
    private const string UnknownState = "COURSE_STATE_FROM_THE_FUTURE";

    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    private static GoogleReadFailedException OutOfRange() => new(GoogleReadFailureKind.ValueOutOfRange, null);

    /// <summary>Course <paramref name="n"/> with one student, one graded item and the student's submission.</summary>
    private static SyncWorld.ClassroomReader WithCourse(
        SyncWorld.ClassroomReader reader,
        int n,
        string? name = null,
        string state = CourseTestData.States.Active,
        DateTimeOffset? activity = null)
    {
        var id = CourseTestData.CourseId(n);
        return reader
            .WithCourse(id, name ?? CourseTestData.CourseName(n), state, students: [Person(n)], creationTime: activity, updateTime: activity)
            .WithCourseWork(id, CourseWorkTestData.ItemId(n), CourseWorkResource.CourseWork, CourseWorkTestData.Title(n), activity ?? T, maxPoints: 10m, creationTime: activity, updateTime: activity)
            .WithSubmission(id, CourseWorkTestData.ItemId(n), CourseTestData.UserId(n), CourseWorkTestData.SubmissionId(n), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: 7m, updateTime: activity);
    }

    private static SyncWorld.ClassroomReader OutOfRangeCourse(SyncWorld.ClassroomReader reader, int n, string? name = null, DateTimeOffset? activity = null) =>
        WithCourse(reader, n, name, activity: activity).WithCourseWorkFailure(CourseTestData.CourseId(n), OutOfRange());

    private static SyncWorld.ClassroomReader UnknownStateCourse(SyncWorld.ClassroomReader reader, int n, string? name = null) =>
        WithCourse(reader, n, name, UnknownState);

    private static async Task<(DateTimeOffset At, SynchronizationRunOutcome Outcome)> RunAsync(
        SyncWorld world,
        SyncWorld.ClassroomReader reader,
        int ordinal,
        CancellationToken ct)
    {
        var at = world.Time.GetUtcNow();
        return (at, await world.RunUsing(reader).ExecuteAsync(SyncWorld.RunId(ordinal), ct));
    }

    private static SyncSkippedCourse Record(SyncWorld world, int course) =>
        world.SkippedCourses.Stored[CourseTestData.CourseId(course)];

    /// <summary>
    /// AC-007, FR-005.1 … FR-005.3, VR-001 … VR-004: a never-loaded course skipped for an out-of-range value and a course
    /// whose state the program does not know are each recorded with the Google id, the name from the course list, the
    /// reason and the run's instant — and nothing else of them is stored.
    /// </summary>
    [Fact]
    public async Task ACourseSkippedForEitherReason_IsRecorded()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var reader = UnknownStateCourse(OutOfRangeCourse(WithCourse(new(), 3), 1, "Out of range course"), 2, "Unknown state course");

        var (at, outcome) = await RunAsync(world, reader, 1, ct);

        Assert.False(outcome.Failed);
        Assert.Equal(2, world.SkippedCourses.Stored.Count);
        var outOfRange = Record(world, 1);
        Assert.Equal(CourseTestData.CourseId(1), outOfRange.CourseGoogleId);
        Assert.Equal("Out of range course", outOfRange.CourseName);
        Assert.Equal(CourseSkipReason.ValueOutOfRange, outOfRange.Reason);
        Assert.Equal(at, outOfRange.SkippedSince);
        var unknown = Record(world, 2);
        Assert.Equal(CourseTestData.CourseId(2), unknown.CourseGoogleId);
        Assert.Equal("Unknown state course", unknown.CourseName);
        Assert.Equal(CourseSkipReason.UnknownCourseState, unknown.Reason);
        Assert.Equal(at, unknown.SkippedSince);
        Assert.DoesNotContain(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.DoesNotContain(CourseTestData.CourseId(2), world.Courses.Stored.Keys);
        Assert.Contains(CourseTestData.CourseId(3), world.Courses.Stored.Keys);
    }

    /// <summary>
    /// VR-002, §1.1: an unknown-state course is checked before the blank-name check, so it can arrive without a name — it
    /// is recorded with no name, never refused; a list name is trimmed.
    /// </summary>
    [Fact]
    public async Task AnUnknownStateCourse_WithABlankName_IsRecordedWithNoName()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var reader = UnknownStateCourse(UnknownStateCourse(new(), 1, "   "), 2, "  Padded name  ");

        var (_, outcome) = await RunAsync(world, reader, 1, ct);

        Assert.False(outcome.Failed);
        Assert.Null(Record(world, 1).CourseName);
        Assert.Equal("Padded name", Record(world, 2).CourseName);
    }

    /// <summary>
    /// FR-005.4, BR-043: recording a skip of a stored course writes nothing else of it — its name stays as stored.
    /// </summary>
    [Fact]
    public async Task RecordingAStoredCourse_LeavesTheCourseAsItWas()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, WithCourse(new(), 1), 1, ct);
        world.Time.Advance(Later);

        var (_, outcome) = await RunAsync(world, OutOfRangeCourse(new(), 1, "Renamed while out of range"), 2, ct);

        Assert.False(outcome.Failed);
        Assert.Equal("Renamed while out of range", Record(world, 1).CourseName);
        Assert.Equal(CourseTestData.CourseName(1), world.Courses.Stored[CourseTestData.CourseId(1)].Name);
    }

    /// <summary>
    /// AC-008, FR-006, I-3: a recorded course skipped again — here for the other reason, under a changed name — keeps the
    /// earlier instant, takes the new name and the new reason, and stays one record.
    /// </summary>
    [Fact]
    public async Task ARepeatedSkip_KeepsTheFirstInstant_AndTakesTheNewNameAndReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var (first, _) = await RunAsync(world, UnknownStateCourse(new(), 1, "First name"), 1, ct);
        world.Time.Advance(Later);

        var (_, outcome) = await RunAsync(world, OutOfRangeCourse(new(), 1, "Second name"), 2, ct);

        Assert.False(outcome.Failed);
        var record = Assert.Single(world.SkippedCourses.Stored.Values);
        Assert.Equal(first, record.SkippedSince);
        Assert.Equal("Second name", record.CourseName);
        Assert.Equal(CourseSkipReason.ValueOutOfRange, record.Reason);
    }

    /// <summary>FR-006: the same skip for the same reason in a later run keeps the first instant too.</summary>
    [Fact]
    public async Task TheSameSkipAgain_KeepsTheFirstInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var (first, _) = await RunAsync(world, OutOfRangeCourse(new(), 1), 1, ct);
        world.Time.Advance(Later);

        await RunAsync(world, OutOfRangeCourse(new(), 1), 2, ct);

        Assert.Equal(first, Assert.Single(world.SkippedCourses.Stored.Values).SkippedSince);
    }

    /// <summary>
    /// AC-009, FR-007.1: a recorded course whose value the teacher corrected is read in full by the next run, and its
    /// record is gone.
    /// </summary>
    [Fact]
    public async Task ARecordedCourseReadInFull_LosesItsRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, OutOfRangeCourse(new(), 1), 1, ct);
        Assert.Single(world.SkippedCourses.Stored.Values);
        world.Time.Advance(Later);

        var (_, outcome) = await RunAsync(world, WithCourse(new(), 1), 2, ct);

        Assert.False(outcome.Failed);
        Assert.Contains(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.Empty(world.SkippedCourses.Stored);
    }

    /// <summary>
    /// AC-010, FR-007.3: a recorded course no longer in Google's course list loses its record once the run has gone
    /// through the whole list; a recorded course still listed and skipped again keeps its own.
    /// </summary>
    [Fact]
    public async Task ARecordedCourseNoLongerListed_LosesItsRecord_WhenTheListIsGoneThrough()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, UnknownStateCourse(UnknownStateCourse(new(), 8), 9), 1, ct);
        Assert.Equal(2, world.SkippedCourses.Stored.Count);
        world.Time.Advance(Later);

        var (_, outcome) = await RunAsync(world, UnknownStateCourse(WithCourse(new(), 1), 8), 2, ct);

        Assert.False(outcome.Failed);
        Assert.Equal([CourseTestData.CourseId(8)], world.SkippedCourses.Stored.Keys);
    }

    /// <summary>
    /// AC-011, FR-005.5, I-1, I-2: a course that vanishes mid-run (<c>404</c>), a course with a blank name and a new course
    /// older than the retention period are skipped without a record.
    /// </summary>
    [Fact]
    public async Task OtherSkips_CreateNoRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var old = T.AddYears(-7);
        var reader = WithCourse(WithCourse(WithCourse(new(), 1), 2, "   "), 3, activity: old);
        reader.WithRosterFailure(CourseTestData.CourseId(1), new GoogleReadFailedException(GoogleReadFailureKind.CourseGone, null));

        var (_, outcome) = await RunAsync(world, reader, 1, ct);

        Assert.False(outcome.Failed);
        Assert.Single(outcome.CoursesSkippedByAge);
        Assert.Empty(world.Courses.Stored);
        Assert.Empty(world.SkippedCourses.Stored);
    }

    /// <summary>
    /// FR-007.4, I-1, I-2: a recorded course that vanishes mid-run (<c>404</c>) or comes with a blank name keeps its record
    /// and its instant — neither outcome means the problem is gone.
    /// </summary>
    [Fact]
    public async Task ARecordedCourse_GoneMidRunOrBlankNamed_KeepsItsRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var (first, _) = await RunAsync(world, OutOfRangeCourse(OutOfRangeCourse(new(), 1), 2), 1, ct);
        world.Time.Advance(Later);

        var reader = WithCourse(WithCourse(new(), 1), 2, "   ");
        reader.WithRosterFailure(CourseTestData.CourseId(1), new GoogleReadFailedException(GoogleReadFailureKind.CourseGone, null));
        var (_, outcome) = await RunAsync(world, reader, 2, ct);

        Assert.False(outcome.Failed);
        Assert.Equal(first, Record(world, 1).SkippedSince);
        Assert.Equal(first, Record(world, 2).SkippedSince);
        Assert.Equal(CourseSkipReason.ValueOutOfRange, Record(world, 1).Reason);
    }

    /// <summary>
    /// AC-014, FR-007.2, I-5: a recorded never-loaded course older than the retention period whose value was corrected is
    /// aged out by the next run — not loaded, and its record is gone.
    /// </summary>
    [Fact]
    public async Task ARecordedCourseAgedOut_IsNotLoaded_AndLosesItsRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var old = T.AddYears(-7);
        await RunAsync(world, OutOfRangeCourse(new(), 1, activity: old), 1, ct);
        Assert.Single(world.SkippedCourses.Stored.Values);
        world.Time.Advance(Later);

        var (_, outcome) = await RunAsync(world, WithCourse(new(), 1, activity: old), 2, ct);

        Assert.False(outcome.Failed);
        Assert.Equal([CourseTestData.CourseId(1)], outcome.CoursesSkippedByAge);
        Assert.DoesNotContain(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.Empty(world.SkippedCourses.Stored);
    }

    /// <summary>
    /// AC-015, FR-001.5, FR-007.3: a run stopped by a permission failure after committing course 1 — whose absent item is
    /// marked — removes no record of a course missing from the list, because it never reached the list's end.
    /// </summary>
    [Fact]
    public async Task AStoppedRun_KeepsTheMarksItCommitted_AndRemovesNoUnlistedRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var first = UnknownStateCourse(WithCourse(new(), 1), 9);
        first.WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(5), CourseWorkResource.CourseWork, CourseWorkTestData.Title(5), T);
        var (recorded, _) = await RunAsync(world, first, 1, ct);
        world.Time.Advance(Later);

        var second = WithCourse(WithCourse(new(), 1), 2);
        second.WithRosterFailure(
            CourseTestData.CourseId(2),
            new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.TechnicalAccountCannotRead));
        var (at, outcome) = await RunAsync(world, second, 2, ct);

        Assert.True(outcome.Failed);
        Assert.Equal(at, world.CourseWork.Stored.Values.Single(w => w.GoogleId == CourseWorkTestData.ItemId(5)).NotInClassroomSince);
        Assert.Equal(recorded, Record(world, 9).SkippedSince);
    }

    /// <summary>
    /// FR-007.3: a course list whose enumeration fails part-way stops the run — no record of a course missing from the
    /// part read is removed.
    /// </summary>
    [Fact]
    public async Task AListThatFailsPartWay_RemovesNoUnlistedRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var (recorded, _) = await RunAsync(world, UnknownStateCourse(new(), 9), 1, ct);
        world.Time.Advance(Later);

        var second = WithCourse(new(), 1);
        second.FailAfterCourses = new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.TechnicalAccountCannotRead);
        var (_, outcome) = await RunAsync(world, second, 2, ct);

        Assert.True(outcome.Failed);
        Assert.Contains(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.Equal(recorded, Record(world, 9).SkippedSince);
    }
}
