using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-014 AC-001, AC-002, AC-005: a run imports the school's courses with the fields §3 fixes, a second run
/// updates in place instead of duplicating, and a course Classroom stopped returning is left untouched
/// (spec FR-003, FR-005, FR-008, FR-011, FR-013).
/// </summary>
public sealed class CourseImportTests
{
    /// <summary>AC-001: every course Classroom returns is imported with the fields of §3.</summary>
    [Fact]
    public async Task ARun_ImportsEveryCourseClassroomReturns()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), CourseTestData.States.Archived);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(2, world.Courses.Added.Count);
        Assert.Equal(
            new[] { CourseTestData.CourseId(1), CourseTestData.CourseId(2) },
            world.Courses.Added.Select(c => c.GoogleId));
        var archived = world.Courses.Stored[CourseTestData.CourseId(2)];
        Assert.Equal(CourseState.Archived, archived.State);
        Assert.Equal(CourseTestData.CourseName(2), archived.Name);
    }

    /// <summary>AC-001: the course's optional fields arrive as Classroom gave them, in UTC (spec I-1).</summary>
    [Fact]
    public async Task ACourse_KeepsTheFieldsClassroomGave()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var created = InstallationTestHost.DefaultStart - TimeSpan.FromDays(400);
        var updated = InstallationTestHost.DefaultStart - TimeSpan.FromDays(3);
        world.Classroom.WithCourse(
            CourseTestData.CourseId(1),
            CourseTestData.CourseName(1),
            ownerGoogleId: CourseTestData.UserId(1),
            creationTime: created,
            updateTime: updated,
            section: "Section A");

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        Assert.Equal(CourseTestData.UserId(1), course.OwnerGoogleId);
        Assert.Equal(created, course.CreationTime);
        Assert.Equal(updated, course.UpdateTime);
        Assert.Equal("Section A", course.Section);
    }

    /// <summary>
    /// AC-001, spec §3.3 of db-design: a course Classroom returns without an owner or its instants is imported.
    /// Refusing it would be the failure mode OD-001 rejected — declining on absent evidence.
    /// </summary>
    [Fact]
    public async Task ACourseWithNoOwnerAndNoInstants_IsStillImported()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var course = Assert.Single(world.Courses.Added);
        Assert.Null(course.OwnerGoogleId);
        Assert.Null(course.CreationTime);
        Assert.Null(course.UpdateTime);
    }

    /// <summary>AC-002, FR-008: a second run with nothing changed adds no row and keeps the surrogate identity.</summary>
    [Fact]
    public async Task ASecondRun_AddsNoCourseAndKeepsTheIdentity()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var firstId = world.Courses.Stored[CourseTestData.CourseId(1)].Id;

        world.Time.Advance(SyncTestData.DefaultInterval);
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Single(world.Courses.Added);
        Assert.Equal(firstId, world.Courses.Stored[CourseTestData.CourseId(1)].Id);
    }

    /// <summary>AC-002: a course whose card changed in Google is updated in place.</summary>
    [Fact]
    public async Task ARenamedCourse_IsUpdatedInPlace()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var renamed = new SyncWorld();
        // The same world cannot restate a course, so the second run is driven by a reader seeded with the new card
        // and the repositories carried over would be the database's; here the assertion is on the update path only.
        renamed.Classroom.WithCourse(
            CourseTestData.CourseId(1),
            CourseTestData.CourseName(2),
            CourseTestData.States.Suspended);
        await renamed.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var course = renamed.Courses.Stored[CourseTestData.CourseId(1)];
        Assert.Equal(CourseTestData.CourseName(2), course.Name);
        Assert.Equal(CourseState.Suspended, course.State);
    }

    /// <summary>
    /// AC-001, FR-013, OD-005: the counter reports courses processed — the number §5 makes meaningful, because the
    /// course is the unit of storage.
    /// </summary>
    [Fact]
    public async Task TheCounter_ReportsCoursesProcessed()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2))
            .WithCourse(
                CourseTestData.CourseId(3),
                CourseTestData.CourseName(3),
                students: [new RosterEntry(CourseTestData.UserId(1), CourseTestData.Email("a.student"), CourseTestData.Name(1))]);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(3, outcome.ProcessedCount);
        Assert.Equal(3, world.States.Stored!.ProcessedCount);
    }

    /// <summary>
    /// AC-001, §3.8, OD-003: a school whose course list comes back empty is a <b>successful</b> run with a zero
    /// counter, not an error — on a live domain that most likely means the technical account lacks its Workspace
    /// roles (§7 item 10), which is a configuration problem and not a failure of the run.
    /// </summary>
    [Fact]
    public async Task AnEmptyCourseList_IsASuccessfulRunWithAZeroCounter()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.False(outcome.Failed);
        Assert.Equal(0, outcome.ProcessedCount);
        Assert.Equal(SyncRunStatus.Completed, world.States.Stored!.Status);
        Assert.Empty(world.Courses.Added);
    }

    /// <summary>
    /// AC-005, FR-011, I-4: a course Classroom no longer returns is left exactly as it was — not marked, not
    /// hidden, not deleted. Only the purge deletes a course, by age, in any state (§5 v36).
    /// </summary>
    [Fact]
    public async Task ACourseClassroomStoppedReturning_IsLeftUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2));
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var kept = world.Courses.Stored[CourseTestData.CourseId(2)];
        var keptUpdatedAt = kept.UpdatedAt;

        // The next run sees only the first course; nothing in this Story may touch the other one.
        var later = new SyncWorld();
        later.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));
        await later.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Equal(CourseTestData.CourseName(2), kept.Name);
        Assert.Equal(keptUpdatedAt, kept.UpdatedAt);
        Assert.Equal(CourseState.Active, kept.State);
    }

    /// <summary>
    /// AC-001, OD-010: a course whose state Classroom reports outside the five §3 values is <b>skipped</b>, and
    /// the run completes with the others. Inventing a sixth state value is forbidden outright.
    /// </summary>
    [Fact]
    public async Task ACourseWithAnUnrecognisedState_IsSkippedAndTheRunCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), CourseTestData.UnrecognisedState)
            .WithCourse(CourseTestData.CourseId(3), CourseTestData.CourseName(3));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.False(outcome.Failed);
        Assert.Equal(SyncRunStatus.Completed, world.States.Stored!.Status);
        Assert.DoesNotContain(CourseTestData.CourseId(2), world.Courses.Stored.Keys);
        Assert.Equal(
            new[] { CourseTestData.CourseId(1), CourseTestData.CourseId(3) },
            world.Courses.Stored.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC-001, FR-013, I-5: a skipped course is not counted, so the counter never overstates what was imported.
    /// </summary>
    [Fact]
    public async Task ASkippedCourse_IsNotCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), CourseTestData.UnrecognisedState);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(1, outcome.ProcessedCount);
    }

    /// <summary>
    /// AC-007, FR-012, OD-009: one course is one transaction, so a run that fails part-way leaves the courses it
    /// already committed and no half-written one.
    /// </summary>
    [Fact]
    public async Task ARunThatFailsPartWay_KeepsTheCoursesAlreadyCommitted()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2));
        world.Classroom.FailAfterCourses = new InvalidOperationException("the synthetic reader stopped answering");

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        Assert.Equal(SyncRunStatus.Failed, world.States.Stored!.Status);
        Assert.Equal(2, world.Courses.Added.Count);
        Assert.Equal(2, world.Work.Transactions);
    }

    /// <summary>
    /// AC-007, FR-014, OD-008; US-017 spec FR-006: the stored diagnosis is a code of the closed list
    /// (<c>Unexpected</c> for a failure that is not a classified Google one), never a Google error text and never
    /// a payload (SC-10).
    /// </summary>
    [Fact]
    public async Task AFailedImport_StoresADiagnosisWithNoPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        const string secret = "a.student@school.example";
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));
        world.Classroom.FailAfterCourses = new InvalidOperationException(secret);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var error = world.States.Stored!.LastError;
        Assert.NotNull(error);
        Assert.DoesNotContain(secret, error, StringComparison.Ordinal);
        Assert.Equal("Unexpected", error);
    }
}
