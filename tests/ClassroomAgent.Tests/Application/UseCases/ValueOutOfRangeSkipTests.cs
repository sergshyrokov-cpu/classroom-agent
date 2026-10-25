using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-057 spec FR-002, FR-004, AC-001 … AC-003, AC-005, AC-009: a course whose coursework or submissions read reports a
/// value beyond the range the program holds is skipped before its transaction — nothing of it is written, it is not
/// counted, the run goes on and completes; a later failure of the same run is reported as itself; and the next run
/// reads the course again. The use case is the unit and the Classroom port is substituted (TC-1, TC-4); that the
/// adapter raises this failure for such a value is proven in <c>GoogleClassroomReaderValueRangeTests</c>.
/// </summary>
public sealed class ValueOutOfRangeSkipTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>The two reads of a course that carry points (VR-001): its coursework and its submissions.</summary>
    public static TheoryData<string> PointReads => new("coursework", "submissions");

    private static GoogleReadFailedException OutOfRange() => new(GoogleReadFailureKind.ValueOutOfRange, null);

    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    /// <summary>Course <paramref name="n"/> with a student of its own, one graded item and the student's submission.</summary>
    private static SyncWorld.ClassroomReader WithCourse(
        SyncWorld.ClassroomReader reader,
        int n,
        string name,
        string state = CourseTestData.States.Active,
        DateTimeOffset? updateTime = null,
        decimal maxPoints = 10m)
    {
        var id = CourseTestData.CourseId(n);
        return reader
            .WithCourse(id, name, state, students: [Person(n)], updateTime: updateTime)
            .WithCourseWork(id, CourseWorkTestData.ItemId(n), CourseWorkResource.CourseWork, CourseWorkTestData.Title(n), T, maxPoints: maxPoints)
            .WithSubmission(id, CourseWorkTestData.ItemId(n), CourseTestData.UserId(n), CourseWorkTestData.SubmissionId(n), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: 7m);
    }

    private static void FailRead(SyncWorld.ClassroomReader reader, string read, int course) =>
        _ = read switch
        {
            "coursework" => reader.WithCourseWorkFailure(CourseTestData.CourseId(course), OutOfRange()),
            "submissions" => reader.WithSubmissionsFailure(CourseTestData.CourseId(course), OutOfRange()),
            _ => throw new ArgumentOutOfRangeException(nameof(read), read, null),
        };

    /// <summary>
    /// AC-001, AC-002, FR-002.1, FR-002.3, FR-002.4: three courses, the second reporting an out-of-range value — courses 1
    /// and 3 are stored, nothing of course 2 is written (not even the person only it listed), the run completes with no
    /// error, only the two stored courses are counted, and course 3 was read after the skip.
    /// </summary>
    [Theory]
    [MemberData(nameof(PointReads))]
    public async Task ACourseWithAValueOutOfRange_IsSkipped_AndTheRunCompletes(string read)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        for (var n = 1; n <= 3; n++)
        {
            WithCourse(world.Classroom, n, CourseTestData.CourseName(n));
        }

        FailRead(world.Classroom, read, 2);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.False(outcome.Failed);
        var state = world.States.Stored!;
        Assert.Equal(SyncRunStatus.Completed, state.Status);
        Assert.Null(state.LastError);
        Assert.Equal(2, state.ProcessedCount);
        Assert.Contains(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.Contains(CourseTestData.CourseId(3), world.Courses.Stored.Keys);
        Assert.DoesNotContain(CourseTestData.CourseId(2), world.Courses.Stored.Keys);
        Assert.DoesNotContain(CourseTestData.UserId(2), world.Participants.Stored.Keys);
        Assert.DoesNotContain(world.CourseWork.Stored.Values, w => w.GoogleId == CourseWorkTestData.ItemId(2));
        Assert.DoesNotContain(world.Submissions.Stored.Values, s => s.GoogleId == CourseWorkTestData.SubmissionId(2));
        Assert.Contains(CourseTestData.CourseId(3), world.Classroom.RostersRead);
    }

    /// <summary>
    /// AC-003, FR-002.1: a course stored by an earlier run that now reports an out-of-range value keeps its name, its
    /// people, its items with their maximum and its submissions with their grades exactly, while the other course of
    /// the same run is updated (the control that the run was effective).
    /// </summary>
    [Theory]
    [MemberData(nameof(PointReads))]
    public async Task AStoredCourse_WithAValueOutOfRange_IsLeftAsItWas(string read)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        WithCourse(world.Classroom, 1, CourseTestData.CourseName(1));
        WithCourse(world.Classroom, 2, CourseTestData.CourseName(2));
        Assert.False((await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct)).Failed);
        var stored = world.Courses.Stored[CourseTestData.CourseId(2)];
        var item = world.CourseWork.Stored.Values.Single(w => w.GoogleId == CourseWorkTestData.ItemId(2));
        var submission = world.Submissions.Stored.Values.Single(s => s.GoogleId == CourseWorkTestData.SubmissionId(2));
        var before = (stored.Name, stored.CompleteReadUpdateTime, item.Title, item.MaxPoints, submission.AssignedGrade,
            Memberships: world.Memberships.OfCourse(stored.Id).Count);

        world.Time.Advance(SyncTestData.DefaultInterval);
        var second = new SyncWorld.ClassroomReader();
        WithCourse(second, 1, "Course 1 renamed in the second run");
        WithCourse(second, 2, "Course 2 renamed in the second run", maxPoints: 20m);
        FailRead(second, read, 2);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.False(outcome.Failed);
        Assert.Equal(SyncRunStatus.Completed, world.States.Stored!.Status);
        Assert.Equal(1, world.States.Stored.ProcessedCount);
        var after = world.Courses.Stored[CourseTestData.CourseId(2)];
        var itemAfter = world.CourseWork.Stored.Values.Single(w => w.GoogleId == CourseWorkTestData.ItemId(2));
        var submissionAfter = world.Submissions.Stored.Values.Single(s => s.GoogleId == CourseWorkTestData.SubmissionId(2));
        Assert.Equal(
            before,
            (after.Name, after.CompleteReadUpdateTime, itemAfter.Title, itemAfter.MaxPoints, submissionAfter.AssignedGrade,
                Memberships: world.Memberships.OfCourse(after.Id).Count));
        Assert.All(world.Memberships.OfCourse(after.Id), m => Assert.True(m.OnRoster));
        Assert.Equal(10m, itemAfter.MaxPoints);
        Assert.Equal("Course 1 renamed in the second run", world.Courses.Stored[CourseTestData.CourseId(1)].Name);
    }

    /// <summary>
    /// AC-009, FR-002.2: a run that skips a course for an out-of-range value and is then stopped by another failure
    /// fails with <b>that</b> failure's diagnosis — the skip itself never turns the run into <c>Unexpected</c>.
    /// </summary>
    [Fact]
    public async Task ASkipFollowedByAnotherFailure_FailsWithThatFailure_NotAsUnexpected()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        WithCourse(world.Classroom, 1, CourseTestData.CourseName(1));
        WithCourse(world.Classroom, 2, CourseTestData.CourseName(2));
        FailRead(world.Classroom, "coursework", 1);
        world.Classroom.WithRosterFailure(
            CourseTestData.CourseId(2),
            new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.TechnicalAccountCannotRead));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        var state = world.States.Stored!;
        Assert.Equal(SyncRunStatus.Failed, state.Status);
        Assert.Equal(nameof(SyncDiagnosis.TechnicalAccountCannotRead), state.LastError);
        Assert.Equal(0, state.ProcessedCount);
        Assert.Contains(CourseTestData.CourseId(2), world.Classroom.RostersRead);
    }

    /// <summary>
    /// AC-005, FR-004: an archived course skipped for an out-of-range value on its first appearance is read in full by
    /// the next, not weekly-full run that lists it with the <b>same</b> updateTime and in-range values, and is stored.
    /// The control — a second archived course stored completely at the same updateTime — is not read by that run, which
    /// proves the run applied the incremental rule rather than reading everything.
    /// </summary>
    [Fact]
    public async Task AnArchivedCourseSkippedForAValue_IsReadAgainByTheNextRun_AtTheSameUpdateTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var first = new SyncWorld.ClassroomReader();
        WithCourse(first, 1, CourseTestData.CourseName(1), CourseTestData.States.Archived, T);
        WithCourse(first, 2, CourseTestData.CourseName(2), CourseTestData.States.Archived, T);
        FailRead(first, "coursework", 1);
        var firstOutcome = await world.RunUsing(first).ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.False(firstOutcome.Failed);
        Assert.DoesNotContain(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.Equal(T, world.Courses.Stored[CourseTestData.CourseId(2)].CompleteReadUpdateTime);

        world.Time.Advance(TimeSpan.FromHours(1));
        var second = new SyncWorld.ClassroomReader();
        WithCourse(second, 1, CourseTestData.CourseName(1), CourseTestData.States.Archived, T);
        WithCourse(second, 2, CourseTestData.CourseName(2), CourseTestData.States.Archived, T);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.False(outcome.Failed);
        Assert.Contains(CourseTestData.CourseId(1), second.RostersRead);
        Assert.Contains(CourseTestData.CourseId(1), second.CourseWorkRead);
        Assert.Contains(CourseTestData.CourseId(1), second.SubmissionsRead);
        Assert.Contains(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.Equal(T, world.Courses.Stored[CourseTestData.CourseId(1)].CompleteReadUpdateTime);
        Assert.Contains(world.CourseWork.Stored.Values, w => w.GoogleId == CourseWorkTestData.ItemId(1) && w.MaxPoints == 10m);
        Assert.DoesNotContain(CourseTestData.CourseId(2), second.RostersRead);
        Assert.Equal(1, outcome.CoursesSkippedUnchanged);
    }
}
