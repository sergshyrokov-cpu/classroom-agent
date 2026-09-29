using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-015 AC-004, BR-051 v56: a submitter synchronization never saw on a roster gets a <c>student</c> membership
/// marked off the roster, both dates at the run's instant, and an existing membership is never rewritten by this
/// rule (spec FR-007; VR-006, I-1, I-7). OD-010 bounds what this class may claim: it proves the program's own rule
/// on synthetic data, never that Classroom ever returns such a submission.
/// </summary>
public sealed class SubmitterWithoutRosterTests
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

    /// <summary>AC-004, FR-007: a submitter never seen on the roster gets a <c>student</c> membership marked off it.</summary>
    [Fact]
    public async Task SubmitterNeverSeenOnRoster_GetsAnOffRosterStudentMembership()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), teachers: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(2), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var participant = Assert.Single(world.Participants.Added, p => p.GoogleUserId == CourseTestData.UserId(2));
        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var membership = Assert.Single(world.Memberships.Stored, m => m.ParticipantId == participant.Id && m.CourseId == course.Id);
        Assert.Equal(ClassroomRole.Student, membership.Role);
        Assert.False(membership.OnRoster);
    }

    /// <summary>AC-004, I-1, VR-006: both the first and last sighting are the run's own instant, not a Google timestamp.</summary>
    [Fact]
    public async Task BothSeenDates_AreTheRunInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var runInstant = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), teachers: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), runInstant)
            .WithSubmission(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseTestData.UserId(2),
                CourseWorkTestData.SubmissionId(1),
                CourseWorkTestData.RawStates.New,
                updateTime: runInstant - TimeSpan.FromDays(10));

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var membership = Assert.Single(world.Memberships.Stored, m => m.ParticipantId == world.Participants.Stored[CourseTestData.UserId(2)].Id);
        Assert.Equal(runInstant, membership.FirstSeenAt);
        Assert.Equal(runInstant, membership.LastSeenAt);
    }

    /// <summary>AC-004, OD-006: a participant created from a submitter's <c>userId</c> alone has no name or email.</summary>
    [Fact]
    public async Task ParticipantCreatedFromUserIdAlone_HasNoNameOrEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), teachers: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(2), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var participant = world.Participants.Stored[CourseTestData.UserId(2)];
        Assert.Null(participant.Email);
        Assert.Null(participant.FullName);
    }

    /// <summary>
    /// AC-004, FR-007: a person already known through the roster keeps the membership US-014's own logic gave them
    /// — the off-roster rule never rewrites it, even once the same person also submits work.
    /// </summary>
    [Fact]
    public async Task ExistingMembership_IsNotRewritten()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var firstRun = world.Time.GetUtcNow();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var membership = Assert.Single(world.Memberships.Stored);
        Assert.True(membership.OnRoster);
        Assert.Equal(firstRun, membership.FirstSeenAt);

        // The second run's roster leaves the person on it, but this time they also submit work — the off-roster
        // rule of FR-007 must find the existing membership rather than create a second one or move its dates.
        var secondReader = new SyncWorld.ClassroomReader();
        secondReader
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), world.Time.GetUtcNow())
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New);
        world.Time.Advance(SyncTestData.DefaultInterval);
        await SecondRun(world, secondReader).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Single(world.Submissions.Added);
        Assert.Single(world.Memberships.Stored);
        Assert.True(membership.OnRoster);
        Assert.Equal(firstRun, membership.FirstSeenAt);
    }
}
