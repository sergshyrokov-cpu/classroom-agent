using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-015 AC-006: in read-only mode neither new port member is reached and no <c>course_work</c> or
/// <c>submission</c> row is written — the existing guard in <see cref="RunSynchronizationUseCase"/> runs first, as
/// it already did for US-014 (spec FR-001, FR-016; SC-5, AD-6, BR-025, BR-026).
/// </summary>
public sealed class CourseWorkRefusalTests
{
    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    private static void Seed(SyncWorld.ClassroomReader reader)
    {
        var now = InstallationTestHost.DefaultStart;
        reader
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), now)
            .WithSubmission(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.New);
    }

    /// <summary>
    /// AC-006: in read-only mode <see cref="ClassroomAgent.Application.Ports.IClassroomReader.ReadCourseWorkAsync"/>
    /// is never called.
    /// </summary>
    /// <remarks>
    /// The allowed case is asserted in the same test on purpose, the <c>CourseImportRefusalTests</c> pattern: "the
    /// port was not called" is also true of a pipeline with no coursework step at all, so without the control this
    /// test would have passed vacuously before the step existed.
    /// </remarks>
    [Fact]
    public async Task ReadOnlyMode_DoesNotReadCourseWork()
    {
        var ct = TestContext.Current.CancellationToken;
        var refused = new SyncWorld(readOnly: true);
        Seed(refused.Classroom);
        var allowed = new SyncWorld();
        Seed(allowed.Classroom);

        await refused.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        await allowed.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Empty(refused.Classroom.CourseWorkRead);

        // The control: the same seeding does reach the coursework port when the installation is not read-only.
        Assert.Equal([CourseTestData.CourseId(1)], allowed.Classroom.CourseWorkRead);
    }

    /// <summary>
    /// AC-006: in read-only mode <see cref="ClassroomAgent.Application.Ports.IClassroomReader.ReadSubmissionsAsync"/>
    /// is never called.
    /// </summary>
    /// <remarks>Paired with the allowed case, for the same reason as <see cref="ReadOnlyMode_DoesNotReadCourseWork"/>.</remarks>
    [Fact]
    public async Task ReadOnlyMode_DoesNotReadSubmissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var refused = new SyncWorld(readOnly: true);
        Seed(refused.Classroom);
        var allowed = new SyncWorld();
        Seed(allowed.Classroom);

        await refused.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        await allowed.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Empty(refused.Classroom.SubmissionsRead);

        // The control: the same seeding does reach the submissions port when the installation is not read-only.
        Assert.Equal([CourseTestData.CourseId(1)], allowed.Classroom.SubmissionsRead);
    }

    /// <summary>AC-006: in read-only mode no <c>course_work</c> or <c>submission</c> row is written.</summary>
    /// <remarks>Paired with the allowed case, for the same reason as the tests above.</remarks>
    [Fact]
    public async Task ReadOnlyMode_WritesNoCourseWorkOrSubmissionRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var refused = new SyncWorld(readOnly: true);
        Seed(refused.Classroom);
        var allowed = new SyncWorld();
        Seed(allowed.Classroom);

        await refused.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        await allowed.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Empty(refused.CourseWork.Added);
        Assert.Empty(refused.Submissions.Added);

        // The control: the same seeding does write both rows when the installation is not read-only.
        Assert.NotEmpty(allowed.CourseWork.Added);
        Assert.NotEmpty(allowed.Submissions.Added);
    }

    /// <summary>
    /// AC-006, the standalone control the matrix names in its own right: a run that is not read-only reads both
    /// new port members and writes both new rows, correctly attributed.
    /// </summary>
    [Fact]
    public async Task AllowedRun_ReadsBothAndWrites()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        Seed(world.Classroom);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal([CourseTestData.CourseId(1)], world.Classroom.CourseWorkRead);
        Assert.Equal([CourseTestData.CourseId(1)], world.Classroom.SubmissionsRead);
        var item = Assert.Single(world.CourseWork.Added);
        var submission = Assert.Single(world.Submissions.Added);
        Assert.Equal(item.Id, submission.CourseWorkId);
    }

    /// <summary>
    /// AC-006, FR-001, US-013 spec FR-006: the coursework and submission writes stay inside the existing protected
    /// write path, so <c>PermittedServiceWrites</c> does not grow a new entry for them.
    /// </summary>
    [Fact]
    public void PermittedServiceWrites_DoesNotGrow() =>
        Assert.DoesNotContain(typeof(RunSynchronizationUseCase), PermittedServiceWrites.Declarations.Keys);
}
