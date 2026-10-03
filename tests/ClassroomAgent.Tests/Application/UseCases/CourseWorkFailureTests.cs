using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-015 AC-007: a Classroom failure part-way through a course's coursework or submissions leaves only complete
/// courses behind, records the failure the way US-013 already does, and the next run finishes the job without
/// duplicating what already committed (spec FR-013, FR-015; BR-041, SC-10).
/// </summary>
public sealed class CourseWorkFailureTests
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

    private static void SeedBothCourses(SyncWorld.ClassroomReader reader, DateTimeOffset now, bool secondCourseSubmissionsFail)
    {
        reader
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New)
            .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), students: [Person(2)])
            .WithCourseWork(CourseTestData.CourseId(2), CourseWorkTestData.ItemId(2), CourseWorkResource.CourseWork, CourseWorkTestData.Title(2), now);

        if (secondCourseSubmissionsFail)
        {
            reader.WithSubmissionsFailure(CourseTestData.CourseId(2), new InvalidOperationException("the synthetic reader stopped answering"));
        }
        else
        {
            reader.WithSubmission(CourseTestData.CourseId(2), CourseWorkTestData.ItemId(2), CourseTestData.UserId(2), CourseWorkTestData.SubmissionId(2), CourseWorkTestData.RawStates.New);
        }
    }

    /// <summary>
    /// AC-007, FR-013: the second course's submissions read fails inside its own transaction, so neither its
    /// coursework nor its submissions land — no course is left with items but no submissions.
    /// </summary>
    [Fact]
    public async Task FailureAfterSomeCourses_LeavesOnlyCompleteCourses()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        SeedBothCourses(world.Classroom, now, secondCourseSubmissionsFail: true);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        var course1 = world.Courses.Stored[CourseTestData.CourseId(1)];
        Assert.Contains(world.CourseWork.Added, c => c.CourseId == course1.Id);
        Assert.Contains(
            world.Submissions.Added,
            s => world.CourseWork.Stored.Values.Any(c => c.Id == s.CourseWorkId && c.CourseId == course1.Id));

        if (world.Courses.Stored.TryGetValue(CourseTestData.CourseId(2), out var course2))
        {
            Assert.DoesNotContain(world.CourseWork.Added, c => c.CourseId == course2.Id);
        }
    }

    /// <summary>
    /// AC-007, FR-015, SC-10; US-017 spec FR-006: a failure that is not a classified Google failure is stored as
    /// the closed-list code <c>Unexpected</c> — no exception type name, no message, no personal data.
    /// </summary>
    [Fact]
    public async Task FailedRun_IsRecordedInSyncState()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        const string secret = "a.student@school.example";
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWorkFailure(CourseTestData.CourseId(1), new InvalidOperationException(secret));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        var error = world.States.Stored!.LastError;
        Assert.NotNull(error);
        Assert.Equal("Unexpected", error);
        Assert.DoesNotContain(secret, error, StringComparison.Ordinal);
    }

    /// <summary>AC-007, BR-041: the next run completes the course a prior run failed on, without duplicating what already committed.</summary>
    [Fact]
    public async Task NextRun_CompletesWithoutDuplicating()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        SeedBothCourses(world.Classroom, now, secondCourseSubmissionsFail: true);
        var firstOutcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.True(firstOutcome.Failed);

        var secondReader = new SyncWorld.ClassroomReader();
        world.Time.Advance(SyncTestData.DefaultInterval);
        SeedBothCourses(secondReader, world.Time.GetUtcNow(), secondCourseSubmissionsFail: false);
        var secondOutcome = await SecondRun(world, secondReader).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.False(secondOutcome.Failed);
        Assert.Equal(2, world.Courses.Added.Count);
        var course1 = world.Courses.Stored[CourseTestData.CourseId(1)];
        var course2 = world.Courses.Stored[CourseTestData.CourseId(2)];
        Assert.Single(world.CourseWork.Added, c => c.CourseId == course1.Id);
        Assert.Single(world.CourseWork.Added, c => c.CourseId == course2.Id);
        Assert.Single(world.Submissions.Added, s => world.CourseWork.Stored[(course1.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))].Id == s.CourseWorkId);
        Assert.Single(world.Submissions.Added, s => world.CourseWork.Stored[(course2.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(2))].Id == s.CourseWorkId);
    }
}
