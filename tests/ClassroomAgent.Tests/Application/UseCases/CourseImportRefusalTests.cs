using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-014 AC-006 and AC-008: in read-only mode, and with no saved connection, the Classroom port is never reached
/// and nothing is written. Proved in the Application layer with the port substituted (spec FR-015; SC-5, AD-6,
/// TC-4, TC-5).
/// </summary>
public sealed class CourseImportRefusalTests
{
    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    /// <summary>
    /// AC-006: in read-only mode no call is made through the Classroom port at all — not the course list, not a
    /// roster, and therefore not the token request the adapter would make first (SC-5).
    /// </summary>
    /// <remarks>
    /// The allowed case is asserted in the same test on purpose. "The port was not called" is true of a run that
    /// refused <b>and</b> of a pipeline that has no step at all, so without the control the test would pass
    /// vacuously before the import exists and prove nothing (the red-phase rule).
    /// </remarks>
    [Fact]
    public async Task InReadOnlyMode_TheClassroomPort_IsNeverCalled()
    {
        var ct = TestContext.Current.CancellationToken;
        var refused = new SyncWorld(readOnly: true);
        refused.Classroom.WithCourse(
            CourseTestData.CourseId(1),
            CourseTestData.CourseName(1),
            students: [Person(1)]);
        var allowed = new SyncWorld();
        allowed.Classroom.WithCourse(
            CourseTestData.CourseId(1),
            CourseTestData.CourseName(1),
            students: [Person(1)]);

        var outcome = await refused.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        await allowed.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.NotNull(outcome.ReadOnlyReason);
        Assert.Equal(0, refused.Classroom.CourseReads);
        Assert.Empty(refused.Classroom.RostersRead);

        // The control: the same seeding does reach Google when the installation is not read-only.
        Assert.Equal(1, allowed.Classroom.CourseReads);
        Assert.Equal([CourseTestData.CourseId(1)], allowed.Classroom.RostersRead);
    }

    /// <summary>
    /// AC-006: in read-only mode nothing is written — no course, no participant, no membership, and no commit. A
    /// synchronization write is not on BR-026's closed list of service writes.
    /// </summary>
    /// <remarks>
    /// Paired with the allowed case for the same reason as the test above: "nothing was written" is also true of a
    /// pipeline with no step.
    /// </remarks>
    [Fact]
    public async Task InReadOnlyMode_NothingIsWritten()
    {
        var ct = TestContext.Current.CancellationToken;
        var refused = new SyncWorld(readOnly: true);
        refused.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]);
        var allowed = new SyncWorld();
        allowed.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]);

        await refused.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        await allowed.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Empty(refused.Courses.Added);
        Assert.Empty(refused.Participants.Added);
        Assert.Empty(refused.Memberships.Added);
        Assert.Equal(0, refused.Work.Commits);
        Assert.Equal(0, refused.Work.Transactions);
        Assert.Null(refused.States.Stored);

        // The control: the same seeding does write when the installation is not read-only.
        Assert.NotEmpty(allowed.Courses.Added);
        Assert.NotEmpty(allowed.Participants.Added);
        Assert.NotEmpty(allowed.Memberships.Added);
    }

    /// <summary>
    /// AC-006, FR-015: the guard is the first thing the run consults, before the port or any repository is touched
    /// — the order US-013 fixed and this Story must not disturb.
    /// </summary>
    /// <remarks>
    /// The control here is the allowed run's impersonation record: it proves the port is reachable at all, so the
    /// zero above is a refusal and not an absent pipeline.
    /// </remarks>
    [Fact]
    public async Task TheGuard_IsConsultedBeforeTheClassroomPort()
    {
        var ct = TestContext.Current.CancellationToken;
        var refused = new SyncWorld(readOnly: true);
        refused.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));
        var allowed = new SyncWorld();
        allowed.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));

        await refused.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        await allowed.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal([RunSynchronizationUseCase.Operation], refused.ReadOnly.Operations);
        Assert.Equal(0, refused.Classroom.CourseReads);
        Assert.Equal(0, refused.States.Reads);

        // The control: the allowed run reaches Google as the technical account of the connection (BR-015).
        Assert.Equal([AccessCheckTestData.TechnicalAccount], allowed.Classroom.ImpersonatedAs.Distinct());
    }

    /// <summary>
    /// AC-006: the school resumes importing by itself once it leaves read-only mode — the refusal is a state, not a
    /// latch.
    /// </summary>
    [Fact]
    public async Task LeavingReadOnlyMode_LetsTheNextRunImport()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(readOnly: true);
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        world.ReadOnly.IsReadOnly = false;
        world.Time.Advance(SyncTestData.DefaultInterval);
        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Null(outcome.ReadOnlyReason);
        Assert.Single(world.Courses.Added);
        Assert.Equal(1, outcome.ProcessedCount);
    }

    /// <summary>
    /// AC-006, US-013 AC-005: with no saved connection there is nothing to synchronize — no Classroom call and no
    /// write, the same shape US-011 gave its own missing-connection case.
    /// </summary>
    /// <remarks>Paired with the allowed case, as the read-only tests above are, and for the same reason.</remarks>
    [Fact]
    public async Task WithNoSavedConnection_TheClassroomPortIsNeverCalledAndNothingIsWritten()
    {
        var ct = TestContext.Current.CancellationToken;
        var refused = new SyncWorld(connection: SeededConnection.None);
        refused.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]);
        var allowed = new SyncWorld();
        allowed.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]);

        var outcome = await refused.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        await allowed.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.NotNull(outcome.ConnectionState);
        Assert.Equal(0, refused.Classroom.CourseReads);
        Assert.Empty(refused.Courses.Added);
        Assert.Empty(refused.Participants.Added);
        Assert.Empty(refused.Memberships.Added);
        Assert.Equal(0, refused.Work.Commits);

        // The control: with a connection saved, the same seeding is imported.
        Assert.Equal(1, allowed.Classroom.CourseReads);
        Assert.NotEmpty(allowed.Courses.Added);
    }

    /// <summary>
    /// AC-006, FR-001, US-013 spec FR-006: the run remains a protected write path, so <c>PermittedServiceWrites</c>
    /// does not grow. Asserted here as the operation name the guard is asked about, which is what makes the path
    /// protected rather than permitted.
    /// </summary>
    [Fact]
    public async Task TheRun_StaysAProtectedWritePath()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal([SyncTestData.Operation], world.ReadOnly.Operations);

        // The guard ran first and the import still happened: a protected write path, not a permitted service write.
        Assert.NotEmpty(world.Courses.Added);
    }
}
