using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-015 AC-003, AC-010: the §5 age rule for a course not yet in the database — its last activity is the latest
/// of the course's own update time, its coursework's creation or update, and its submissions' update, and the run
/// counter still counts courses, never items (spec FR-011, FR-014; I-1, I-3, I-4, I-5, I-10).
/// </summary>
public sealed class CourseAgeRuleTests
{
    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    /// <summary>
    /// AC-003, FR-014, OD-005: the counter reports courses processed, not the coursework items or submissions
    /// inside them — the meaning US-014 already fixed and this Story must not change.
    /// </summary>
    [Fact]
    public async Task ProcessedCount_CountsCoursesNotItems()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1), Person(2)]);
        for (var i = 1; i <= 3; i++)
        {
            world.Classroom.WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(i), CourseWorkResource.CourseWork, CourseWorkTestData.Title(i), now);
        }

        world.Classroom
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(2), CourseTestData.UserId(2), CourseWorkTestData.SubmissionId(2), CourseWorkTestData.RawStates.New);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(1, outcome.ProcessedCount);
        Assert.Equal(1, world.States.Stored!.ProcessedCount);

        // Without these two the test passes before the import exists: "the counter is 1" is equally true of a run
        // that imported three items under one course and of a pipeline that imported nothing at all. Asserting the
        // items and submissions really arrived is what makes the count mean "courses, not items" (the red-phase rule).
        Assert.Equal(3, world.CourseWork.Added.Count);
        Assert.Equal(2, world.Submissions.Added.Count);
    }

    /// <summary>
    /// AC-010, FR-011: a course not yet in the database whose last activity is older than N years leaves nothing
    /// behind at all — no course, participant, membership, coursework or submission (§5 v36, v55).
    /// </summary>
    [Fact]
    public async Task UnknownCourseOlderThanN_ImportsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(retentionYears: 5);
        var now = world.Time.GetUtcNow();
        var old = now.AddYears(-5) - TimeSpan.FromDays(1);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)], updateTime: old)
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), old, creationTime: old, updateTime: old)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New, updateTime: old);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Empty(world.Courses.Added);
        Assert.Empty(world.Participants.Added);
        Assert.Empty(world.Memberships.Stored);
        Assert.Empty(world.CourseWork.Added);
        Assert.Empty(world.Submissions.Added);
    }

    /// <summary>
    /// AC-010, **control**: the same shape of course, with its last activity within N years, is imported in full —
    /// the positive case that keeps <see cref="UnknownCourseOlderThanN_ImportsNothing"/> from being vacuous.
    /// </summary>
    [Fact]
    public async Task UnknownCourseWithinN_IsImportedInFull()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(retentionYears: 5);
        var now = world.Time.GetUtcNow();
        var recent = now.AddYears(-5) + TimeSpan.FromDays(1);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)], updateTime: recent)
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), recent, creationTime: recent, updateTime: recent)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New, updateTime: recent);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(1, outcome.ProcessedCount);
        Assert.NotEmpty(world.Courses.Added);
        Assert.NotEmpty(world.Memberships.Stored);
        Assert.NotEmpty(world.CourseWork.Added);
        Assert.NotEmpty(world.Submissions.Added);
    }

    /// <summary>
    /// AC-010, FR-011, PC-11: a course whose own dates are old is imported anyway because a fresh submission is the
    /// latest of the four sources that define last activity.
    /// </summary>
    [Fact]
    public async Task LastActivity_TakesTheLatestOfCourseWorkAndSubmissionDates()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(retentionYears: 5);
        var now = world.Time.GetUtcNow();
        var old = now.AddYears(-5) - TimeSpan.FromDays(30);
        var recentSubmission = now - TimeSpan.FromDays(1);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)], updateTime: old)
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), old, creationTime: old, updateTime: old)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New, updateTime: recentSubmission);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(1, outcome.ProcessedCount);
        Assert.NotEmpty(world.Courses.Added);
        Assert.NotEmpty(world.Submissions.Added);
    }

    /// <summary>AC-010, I-3: exactly N years before the run's instant is not "older than N" and is imported.</summary>
    [Fact]
    public async Task CourseExactlyAtTheBoundary_IsImported()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(retentionYears: 5);
        var now = world.Time.GetUtcNow();
        var boundary = now.AddYears(-5);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)], updateTime: boundary)
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), boundary, creationTime: boundary, updateTime: boundary)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New, updateTime: boundary);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(1, outcome.ProcessedCount);
        Assert.NotEmpty(world.Courses.Added);

        // "The course was imported" alone is true of US-014's unconditional import too, so the boundary case would
        // pass before the rule exists. Asserting the whole course arrived — item and submission included — is what
        // makes this a test of the boundary rather than of US-014 (the red-phase rule).
        Assert.NotEmpty(world.CourseWork.Added);
        Assert.NotEmpty(world.Submissions.Added);
    }

    /// <summary>AC-010, I-4, §5 v55: a course already in the database is never age-checked, however old it becomes.</summary>
    [Fact]
    public async Task AlreadyImportedCourse_IsUpdatedHoweverOld()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(retentionYears: 5);
        var now = world.Time.GetUtcNow();
        var veryOld = now.AddYears(-50);
        world.Courses.Add(Course.Import(
            CourseTestData.CourseId(1),
            CourseState.Active,
            new CourseDetails(CourseTestData.CourseName(1), null, null, null, null, null, veryOld, veryOld, null, null, null, null)));

        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(2), students: [Person(1)], updateTime: veryOld)
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), veryOld, creationTime: veryOld, updateTime: veryOld)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New, updateTime: veryOld);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(1, outcome.ProcessedCount);
        Assert.Equal(CourseTestData.CourseName(2), world.Courses.Stored[CourseTestData.CourseId(1)].Name);
        Assert.NotEmpty(world.CourseWork.Added);
        Assert.NotEmpty(world.Submissions.Added);
    }

    /// <summary>AC-010, I-5: a course the age rule skips is not counted, alongside one that is.</summary>
    [Fact]
    public async Task SkippedCourse_IsNotCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(retentionYears: 5);
        var now = world.Time.GetUtcNow();
        var old = now.AddYears(-5) - TimeSpan.FromDays(1);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), updateTime: old)
            .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), updateTime: now);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(1, outcome.ProcessedCount);
        Assert.DoesNotContain(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.Contains(CourseTestData.CourseId(2), world.Courses.Stored.Keys);
    }
}
