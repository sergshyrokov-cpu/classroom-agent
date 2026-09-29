using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-015 AC-002, AC-003, AC-005, VR-004: a run reads a course's submissions once with the OD-002 wildcard,
/// attributes each one by its own <c>courseWorkId</c>, stores the facts PC-13 allows exactly as Google gave them,
/// and stores an unrecognised state with its marker while the run completes (spec FR-004, FR-006, FR-007, FR-009,
/// FR-010; VR-003, VR-004, VR-007).
/// </summary>
public sealed class SubmissionImportTests
{
    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    private static RunSynchronizationUseCase SecondRun(SyncWorld world, SyncWorld.ClassroomReader reader) =>
        new(
            world.States,
            world.ConnectionQuery,
            world.ReadOnly,
            world.Work,
            world.Time,
            reader,
            world.Courses,
            world.Participants,
            world.Memberships,
            world.CourseWork,
            world.Submissions,
            world.Retention);

    /// <summary>
    /// AC-002, FR-004, OD-002: submissions are read once for the whole course, not once per coursework item —
    /// the observable proxy for the <c>courseWorkId = "-"</c> wildcard, which is an adapter-only detail invisible
    /// at the port (entity model §6).
    /// </summary>
    [Fact]
    public async Task Submissions_AreReadOncePerCourseWithTheWildcard()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now)
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(2), CourseWorkResource.CourseWork, CourseWorkTestData.Title(2), now)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(2), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(2), CourseWorkTestData.RawStates.New);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal([CourseTestData.CourseId(1)], world.Classroom.SubmissionsRead);
    }

    /// <summary>AC-002, FR-004: each submission is stored under the coursework item its own <c>courseWorkId</c> names.</summary>
    [Fact]
    public async Task EachSubmission_IsAttributedByItsOwnCourseWorkId()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1), Person(2)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now)
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(2), CourseWorkResource.CourseWork, CourseWorkTestData.Title(2), now)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(2), CourseTestData.UserId(2), CourseWorkTestData.SubmissionId(2), CourseWorkTestData.RawStates.New);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var item1 = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))];
        var item2 = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(2))];
        var submission1 = world.Submissions.Stored[(item1.Id, CourseWorkTestData.SubmissionId(1))];
        var submission2 = world.Submissions.Stored[(item2.Id, CourseWorkTestData.SubmissionId(2))];
        Assert.Equal(item1.Id, submission1.CourseWorkId);
        Assert.Equal(item2.Id, submission2.CourseWorkId);
    }

    /// <summary>AC-002, VR-003, VR-007, I-9: grades and the <c>late</c> flag are stored exactly as Google gave them.</summary>
    [Fact]
    public async Task GradesAndLateFlag_AreStoredExactlyAsGoogleGaveThem()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now, maxPoints: 100m)
            .WithSubmission(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseTestData.UserId(1),
                CourseWorkTestData.SubmissionId(1),
                CourseWorkTestData.RawStates.TurnedIn,
                assignedGrade: 87.5m,
                draftGrade: 90m,
                late: true);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var item = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))];
        var submission = world.Submissions.Stored[(item.Id, CourseWorkTestData.SubmissionId(1))];
        Assert.Equal(87.5m, submission.AssignedGrade);
        Assert.Equal(90m, submission.DraftGrade);
        Assert.True(submission.Late);
    }

    /// <summary>
    /// AC-002, FR-009, BR-058: the adapter has already reduced the history to the latest <c>TURNED_IN</c> instant
    /// before it crosses the port (entity model §6), so the use case's job is to store
    /// <see cref="SubmissionSnapshot.TurnedInAt"/> exactly as given.
    /// </summary>
    [Fact]
    public async Task LastTurnIn_IsTheLatestTransition()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        var latestTurnIn = now - TimeSpan.FromDays(1);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now)
            .WithSubmission(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseTestData.UserId(1),
                CourseWorkTestData.SubmissionId(1),
                CourseWorkTestData.RawStates.ReclaimedByStudent,
                turnedInAt: latestTurnIn);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var item = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))];
        var submission = world.Submissions.Stored[(item.Id, CourseWorkTestData.SubmissionId(1))];
        Assert.Equal(latestTurnIn, submission.TurnedInAt);
    }

    /// <summary>AC-002, FR-009: a submission whose history carries no turn-in has no date — never invented from <c>updateTime</c> (OD-009).</summary>
    [Fact]
    public async Task SubmissionWithoutTurnInHistory_HasNoDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now)
            .WithSubmission(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseTestData.UserId(1),
                CourseWorkTestData.SubmissionId(1),
                CourseWorkTestData.RawStates.New,
                updateTime: now);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var item = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))];
        var submission = world.Submissions.Stored[(item.Id, CourseWorkTestData.SubmissionId(1))];
        Assert.Null(submission.TurnedInAt);
    }

    /// <summary>
    /// AC-002, I-6: a course with only a material and one real coursework item ends up with every submission
    /// attributed to the graded item, never to the material — proving no submission is queried or stored against a
    /// material, which §3 says has none.
    /// </summary>
    [Fact]
    public async Task Materials_AreNeverQueriedForSubmissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now)
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(2), CourseWorkResource.CourseWorkMaterial, CourseWorkTestData.Title(2), now)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var material = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWorkMaterial, CourseWorkTestData.ItemId(2))];
        Assert.Single(world.Submissions.Added);
        Assert.DoesNotContain(world.Submissions.Added, s => s.CourseWorkId == material.Id);
    }

    /// <summary>AC-002, AC-009, VR-005: several submissions of a course are all stored, not only the first page's worth.</summary>
    [Fact]
    public async Task Submissions_ArePagedToTheEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1), Person(2), Person(3)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now);
        for (var i = 1; i <= 3; i++)
        {
            world.Classroom.WithSubmission(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseTestData.UserId(i),
                CourseWorkTestData.SubmissionId(i),
                CourseWorkTestData.RawStates.New);
        }

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(3, world.Submissions.Added.Count);
        Assert.Equal(
            new[] { 1, 2, 3 }.Select(CourseWorkTestData.SubmissionId),
            world.Submissions.Added.Select(s => s.GoogleId));
    }

    /// <summary>
    /// VR-004, OD-005: a state outside the six recognised values is stored with the <c>Unrecognised</c> marker and
    /// the raw string Google sent, and the run still completes.
    /// </summary>
    [Fact]
    public async Task UnrecognisedState_IsStoredWithTheRawStringAndTheRunCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now)
            .WithSubmission(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseTestData.UserId(1),
                CourseWorkTestData.SubmissionId(1),
                CourseWorkTestData.UnrecognisedState);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.False(outcome.Failed);
        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var item = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))];
        var submission = world.Submissions.Stored[(item.Id, CourseWorkTestData.SubmissionId(1))];
        Assert.Equal(SubmissionState.Unrecognised, submission.State);
        Assert.Equal(CourseWorkTestData.UnrecognisedState, submission.RawState);
    }

    /// <summary>VR-004: every one of the six recognised values is stored with no raw string kept alongside it.</summary>
    [Fact]
    public async Task RecognisedStates_AreStoredWithoutARawString()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom.WithCourse(
            CourseTestData.CourseId(1),
            CourseTestData.CourseName(1),
            students: CourseWorkTestData.RawStates.AllRecognised.Select((_, i) => Person(i + 1)));
        world.Classroom.WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now);
        for (var i = 0; i < CourseWorkTestData.RawStates.AllRecognised.Length; i++)
        {
            world.Classroom.WithSubmission(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseTestData.UserId(i + 1),
                CourseWorkTestData.SubmissionId(i + 1),
                CourseWorkTestData.RawStates.AllRecognised[i]);
        }

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(CourseWorkTestData.RawStates.AllRecognised.Length, world.Submissions.Added.Count);
        Assert.All(world.Submissions.Added, s =>
        {
            Assert.NotEqual(SubmissionState.Unrecognised, s.State);
            Assert.Null(s.RawState);
        });
    }

    /// <summary>AC-003, FR-010: a grade that changed in Google updates the same submission row in place.</summary>
    [Fact]
    public async Task SecondRun_UpdatesTheGradeInPlace()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now, maxPoints: 100m)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: 70m);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var item = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))];
        var firstId = world.Submissions.Stored[(item.Id, CourseWorkTestData.SubmissionId(1))].Id;

        var secondReader = new SyncWorld.ClassroomReader();
        secondReader
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now, maxPoints: 100m)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: 85m);
        world.Time.Advance(SyncTestData.DefaultInterval);
        await SecondRun(world, secondReader).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Single(world.Submissions.Added);
        var submission = world.Submissions.Stored[(item.Id, CourseWorkTestData.SubmissionId(1))];
        Assert.Equal(firstId, submission.Id);
        Assert.Equal(85m, submission.AssignedGrade);
    }

    /// <summary>AC-005, OD-007: Google's <c>updateTime</c> is stored, which PC-11 needs for a course's last activity.</summary>
    [Fact]
    public async Task GoogleUpdateTimeIsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        var updateTime = now - TimeSpan.FromHours(3);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now)
            .WithSubmission(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseTestData.UserId(1),
                CourseWorkTestData.SubmissionId(1),
                CourseWorkTestData.RawStates.New,
                updateTime: updateTime);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var item = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))];
        var submission = world.Submissions.Stored[(item.Id, CourseWorkTestData.SubmissionId(1))];
        Assert.Equal(updateTime, submission.UpdateTime);
    }
}
