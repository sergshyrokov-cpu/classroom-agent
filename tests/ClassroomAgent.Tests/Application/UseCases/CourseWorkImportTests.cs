using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-015 AC-001, AC-003, AC-005: a run imports each course's <c>courseWork</c> and <c>courseWorkMaterial</c>
/// items, stores the FR-008 item date as the adapter already resolved it, and a second run updates the same row
/// rather than duplicating it (spec FR-003, FR-005, FR-008, FR-010, FR-013).
/// </summary>
/// <remarks>
/// <b>Not implemented in this class: <c>DraftAndDeletedWork_AreNotImported</c> and its control
/// <c>PublishedWork_IsImported</c> (AC-001, OD-004).</b> The ac-test-matrix maps both to Level U against
/// <see cref="ClassroomAgent.Application.Ports.IClassroomReader"/>, but neither
/// <see cref="ClassroomAgent.Application.Models.CourseWorkSnapshot"/> nor
/// <see cref="CourseWorkDetails"/> carries a Classroom <c>state</c> field — unlike
/// <see cref="ClassroomAgent.Application.Models.CourseSnapshot.State"/> for a course or
/// <see cref="ClassroomAgent.Application.Models.SubmissionSnapshot.State"/> for a submission, both of which do cross
/// the port as a string precisely so the use case can classify them. The entity model (§6) and db-design (§3.6)
/// place the <c>PUBLISHED</c> filter in the adapter, below the port, which means the fixture this Story's other
/// tests use (<see cref="SyncWorld.ClassroomReader.WithCourseWork"/>) has no way to represent a draft or deleted
/// item at all, so no Application-layer test can exercise this filter. Flagged as a gap between the ac-test-matrix
/// and the already-approved entity model rather than invented around, per AGENTS.md's Open Decisions policy — the
/// two either belong to an Infrastructure-adapter suite (not part of this Story's committed test classes) or the
/// port model needs a <c>State</c> member added at DB_DESIGN/API_DESIGN, which is not this batch's decision to make.
/// </remarks>
public sealed class CourseWorkImportTests
{
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
            world.Retention,
            world.Meet,
            world.MeetSessions);

    /// <summary>AC-001, FR-003: both Classroom resources of a course are imported into the one table (OD-008).</summary>
    [Fact]
    public async Task BothClassroomResources_AreImported()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                now)
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(2),
                CourseWorkResource.CourseWorkMaterial,
                CourseWorkTestData.Title(2),
                now);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(2, world.CourseWork.Added.Count);
        Assert.Single(world.CourseWork.Added, c => c.Resource == CourseWorkResource.CourseWork);
        Assert.Single(world.CourseWork.Added, c => c.Resource == CourseWorkResource.CourseWorkMaterial);
    }

    /// <summary>
    /// AC-001, AC-009, VR-005: several items of each resource are all stored, not only the first one a naive,
    /// single-page-shaped implementation would keep.
    /// </summary>
    [Fact]
    public async Task CourseWorkAndMaterials_ArePagedToTheEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));
        for (var i = 1; i <= 3; i++)
        {
            world.Classroom.WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(i),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(i),
                now);
        }

        for (var i = 4; i <= 5; i++)
        {
            world.Classroom.WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(i),
                CourseWorkResource.CourseWorkMaterial,
                CourseWorkTestData.Title(i),
                now);
        }

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(5, world.CourseWork.Added.Count);
        Assert.Equal(
            new[] { 1, 2, 3, 4, 5 }.Select(CourseWorkTestData.ItemId),
            world.CourseWork.Added.Select(c => c.GoogleId));
    }

    /// <summary>
    /// AC-001, FR-008: the adapter has already resolved the cascade to one instant before it crosses the port
    /// (entity model §6), so the use case's job is to store <see cref="CourseWorkDetails.ItemDate"/> exactly as
    /// given — not to re-derive it from <see cref="CourseWorkDetails.DueAt"/>, which is also present here and
    /// deliberately different, so a wrongful re-derivation inside the pipeline would be caught.
    /// </summary>
    [Fact]
    public async Task ItemDate_UsesScheduledTimeWhenPresent()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var scheduledTime = world.Time.GetUtcNow() + TimeSpan.FromDays(1);
        var dueDate = world.Time.GetUtcNow() + TimeSpan.FromDays(7);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                itemDate: scheduledTime,
                dueAt: dueDate);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var item = Assert.Single(world.CourseWork.Added);
        Assert.Equal(scheduledTime, item.ItemDate);
        Assert.Equal(dueDate, item.DueAt);
    }

    /// <summary>AC-001, FR-008: with no scheduled time, the item date is the due date the adapter already fell back to.</summary>
    [Fact]
    public async Task ItemDate_FallsBackToDueDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var dueDate = world.Time.GetUtcNow() + TimeSpan.FromDays(7);
        var updateTime = world.Time.GetUtcNow() - TimeSpan.FromDays(2);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                itemDate: dueDate,
                dueAt: dueDate,
                updateTime: updateTime);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var item = Assert.Single(world.CourseWork.Added);
        Assert.Equal(dueDate, item.ItemDate);
    }

    /// <summary>AC-001, FR-008: with no scheduled time and no due date, the item date is Google's update time.</summary>
    [Fact]
    public async Task ItemDate_FallsBackToUpdateTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var updateTime = world.Time.GetUtcNow() - TimeSpan.FromDays(2);
        var creationTime = world.Time.GetUtcNow() - TimeSpan.FromDays(30);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                itemDate: updateTime,
                creationTime: creationTime,
                updateTime: updateTime);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var item = Assert.Single(world.CourseWork.Added);
        Assert.Equal(updateTime, item.ItemDate);
        Assert.Null(item.DueAt);
    }

    /// <summary>
    /// AC-001, FR-008: with none of the other three sources, the item date is Google's creation time — the cascade's
    /// last resort, which db-design §3.3 notes Classroom returns for every item.
    /// </summary>
    [Fact]
    public async Task ItemDate_FallsBackToCreationTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var creationTime = world.Time.GetUtcNow() - TimeSpan.FromDays(30);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                itemDate: creationTime,
                creationTime: creationTime);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var item = Assert.Single(world.CourseWork.Added);
        Assert.Equal(creationTime, item.ItemDate);
        Assert.Null(item.UpdateTime);
    }

    /// <summary>AC-001, AC-005, PC-11: Google's creation and update times are kept as given, in UTC.</summary>
    [Fact]
    public async Task GoogleCreationAndUpdateTimes_AreStoredAsGiven()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var created = world.Time.GetUtcNow() - TimeSpan.FromDays(60);
        var updated = world.Time.GetUtcNow() - TimeSpan.FromDays(1);
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                itemDate: updated,
                creationTime: created,
                updateTime: updated);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var item = Assert.Single(world.CourseWork.Added);
        Assert.Equal(created, item.CreationTime);
        Assert.Equal(updated, item.UpdateTime);
    }

    /// <summary>AC-003, FR-010: a second run with nothing changed updates the same row and adds no duplicate.</summary>
    [Fact]
    public async Task SecondRun_UpdatesTheSameRowRatherThanAddingOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                now);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var firstId = world.CourseWork.Stored[(world.Courses.Stored[CourseTestData.CourseId(1)].Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))].Id;

        world.Time.Advance(SyncTestData.DefaultInterval);
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Single(world.CourseWork.Added);
        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        Assert.Equal(firstId, world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))].Id);
    }

    /// <summary>
    /// AC-003, §3.3: a piece of work that gained maximum points in Google updates the same row — the kind is
    /// derived, so nothing is duplicated and no history is kept (BR-052, PC-3).
    /// </summary>
    /// <remarks>
    /// <see cref="SyncWorld.ClassroomReader"/> can only accumulate seeded items, never replace one, so a genuine
    /// second run with changed data needs a fresh reader driving the same repositories — the technique
    /// <see cref="SecondRun"/> exists for.
    /// </remarks>
    [Fact]
    public async Task WorkThatGainedMaximumPoints_UpdatesTheSameRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                now);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var firstId = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))].Id;

        var secondReader = new SyncWorld.ClassroomReader();
        secondReader
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                now,
                maxPoints: 100m);
        world.Time.Advance(SyncTestData.DefaultInterval);
        await SecondRun(world, secondReader).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Single(world.CourseWork.Added);
        var item = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(1))];
        Assert.Equal(firstId, item.Id);
        Assert.Equal(100m, item.MaxPoints);
        Assert.Equal(CourseWorkKind.GradedWork, item.Kind);
    }

    /// <summary>
    /// AC-005, FR-011, I-4 precedent: an item Classroom stops returning keeps its row untouched, exactly as US-014
    /// leaves a vanished course — nothing in this Story deletes a row (entity model §1.2).
    /// </summary>
    [Fact]
    public async Task AnItemGoogleNoLongerReturns_KeepsItsRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                now)
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(2),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(2),
                now);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var kept = world.CourseWork.Stored[(course.Id, CourseWorkResource.CourseWork, CourseWorkTestData.ItemId(2))];
        var keptUpdatedAt = kept.UpdatedAt;

        var secondReader = new SyncWorld.ClassroomReader();
        secondReader
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                now);
        world.Time.Advance(SyncTestData.DefaultInterval);
        await SecondRun(world, secondReader).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Equal(CourseWorkTestData.Title(2), kept.Title);
        Assert.Equal(keptUpdatedAt, kept.UpdatedAt);
        Assert.Equal(2, world.CourseWork.Stored.Count);
    }
}
