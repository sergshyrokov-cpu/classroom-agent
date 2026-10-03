using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-017 AC-002, AC-003, AC-004, AC-005, AC-010 (spec FR-001, FR-004, FR-005, FR-006, FR-012, VR-002, VR-003, I-5):
/// what the run does with a final failure the Google adapter reports. A configuration, a final transient or an
/// unexpected failure stops the run with a closed-list diagnosis and the number of courses committed before the stop;
/// a course that is gone, or whose name is blank, is skipped and the run completes with the others. The use case is
/// the unit and the Classroom port is substituted (TC-1, TC-4); the retry itself is the adapter's and is proven in
/// <c>GoogleClassroomReaderRetryTests</c>.
/// </summary>
public sealed class SynchronizationFailureHandlingTests
{
    private const string Marker = "SECRET-DETAIL-9c1e";

    public static TheoryData<SyncDiagnosis> ConfigurationDiagnoses => new(
        SyncDiagnosis.ScopeNotAuthorized,
        SyncDiagnosis.TechnicalAccountUnknown,
        SyncDiagnosis.TechnicalAccountCannotRead,
        SyncDiagnosis.ApiNotEnabled,
        SyncDiagnosis.KeyUnavailable,
        SyncDiagnosis.KeyRejected);

    public static TheoryData<string> PerCourseReads => new("roster", "coursework", "submissions");

    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    private static GoogleReadFailedException Configuration(SyncDiagnosis diagnosis) =>
        new(GoogleReadFailureKind.Configuration, diagnosis);

    private static GoogleReadFailedException CourseGone() => new(GoogleReadFailureKind.CourseGone, null);

    /// <summary>Courses 1..count, each with a student of its own, a coursework item and a submission.</summary>
    private static SyncWorld.ClassroomReader SeedCourses(SyncWorld.ClassroomReader reader, DateTimeOffset now, int count)
    {
        for (var n = 1; n <= count; n++)
        {
            reader
                .WithCourse(CourseTestData.CourseId(n), CourseTestData.CourseName(n), students: [Person(n)])
                .WithCourseWork(CourseTestData.CourseId(n), CourseWorkTestData.ItemId(n), CourseWorkResource.CourseWork, CourseWorkTestData.Title(n), now)
                .WithSubmission(CourseTestData.CourseId(n), CourseWorkTestData.ItemId(n), CourseTestData.UserId(n), CourseWorkTestData.SubmissionId(n), CourseWorkTestData.RawStates.New);
        }

        return reader;
    }

    private static void FailRead(SyncWorld.ClassroomReader reader, string read, int course, Exception failure)
    {
        var id = CourseTestData.CourseId(course);
        switch (read)
        {
            case "roster":
                reader.WithRosterFailure(id, failure);
                break;
            case "coursework":
                reader.WithCourseWorkFailure(id, failure);
                break;
            case "submissions":
                reader.WithSubmissionsFailure(id, failure);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(read), read, null);
        }
    }

    /// <summary>
    /// AC-003, FR-005, FR-006: a configuration failure of the course listing stops the run at once with the outcome's
    /// own name as the diagnosis, and no course is read — the quota is not wasted (§6). The control world, without the
    /// failure, reads all three seeded courses, so "none read" is not a result of an empty reader.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConfigurationDiagnoses))]
    public async Task AConfigurationFailureOfTheListing_StopsTheRun_WithTheDiagnosisName(SyncDiagnosis diagnosis)
    {
        var ct = TestContext.Current.CancellationToken;
        var control = new SyncWorld();
        SeedCourses(control.Classroom, control.Time.GetUtcNow(), 3);
        await control.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Equal(3, control.Classroom.RostersRead.Count);

        var world = new SyncWorld();
        SeedCourses(world.Classroom, world.Time.GetUtcNow(), 3);
        world.Classroom.FailOnListing = Configuration(diagnosis);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        var state = world.States.Stored!;
        Assert.Equal(SyncRunStatus.Failed, state.Status);
        Assert.Equal(diagnosis.ToString(), state.LastError);
        Assert.Equal(diagnosis.ToString(), outcome.Error);
        Assert.Equal(1, world.Classroom.CourseReads);
        Assert.Empty(world.Classroom.RostersRead);
        Assert.Empty(world.Classroom.CourseWorkRead);
        Assert.Empty(world.Classroom.SubmissionsRead);
        Assert.Empty(world.Courses.Added);
    }

    /// <summary>
    /// AC-003, FR-005, I-5: a configuration failure on the second of three courses leaves the first committed, imports
    /// neither the second nor the third, never reads the third, and records the code and one course processed. The
    /// control world imports all three.
    /// </summary>
    [Fact]
    public async Task AConfigurationFailureOnTheSecondRoster_KeepsTheFirstCourse_AndStops()
    {
        var ct = TestContext.Current.CancellationToken;
        var control = new SyncWorld();
        SeedCourses(control.Classroom, control.Time.GetUtcNow(), 3);
        await control.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Equal(3, control.Courses.Stored.Count);

        var world = new SyncWorld();
        SeedCourses(world.Classroom, world.Time.GetUtcNow(), 3);
        world.Classroom.WithRosterFailure(CourseTestData.CourseId(2), Configuration(SyncDiagnosis.ScopeNotAuthorized));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        Assert.Contains(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.DoesNotContain(CourseTestData.CourseId(2), world.Courses.Stored.Keys);
        Assert.DoesNotContain(CourseTestData.CourseId(3), world.Courses.Stored.Keys);
        Assert.Equal([CourseTestData.CourseId(1), CourseTestData.CourseId(2)], world.Classroom.RostersRead);
        var state = world.States.Stored!;
        Assert.Equal(SyncRunStatus.Failed, state.Status);
        Assert.Equal("ScopeNotAuthorized", state.LastError);
        Assert.Equal(1, state.ProcessedCount);
        Assert.NotNull(state.FinishedAt);
    }

    /// <summary>
    /// AC-002, FR-005, FR-006: a transient failure that is final (the adapter already spent its attempts) fails the
    /// run as <c>GoogleUnavailable</c>; the courses committed before it stay and are counted.
    /// </summary>
    [Fact]
    public async Task AFinalTransientFailure_FailsTheRun_AsGoogleUnavailable_AndKeepsEarlierCourses()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        SeedCourses(world.Classroom, world.Time.GetUtcNow(), 3);
        world.Classroom.WithRosterFailure(
            CourseTestData.CourseId(2),
            new GoogleReadFailedException(GoogleReadFailureKind.Transient, SyncDiagnosis.GoogleUnavailable));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        var state = world.States.Stored!;
        Assert.Equal(SyncRunStatus.Failed, state.Status);
        Assert.Equal("GoogleUnavailable", state.LastError);
        Assert.Equal(1, state.ProcessedCount);
        Assert.Contains(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.DoesNotContain(CourseTestData.CourseId(2), world.Courses.Stored.Keys);
        Assert.DoesNotContain(CourseTestData.CourseId(3), world.Courses.Stored.Keys);
    }

    /// <summary>
    /// AC-002, FR-005: a final transient failure part-way through the course listing (a later page) keeps the course
    /// already committed and records the count.
    /// </summary>
    [Fact]
    public async Task AFinalTransientFailureOfTheListing_AfterOneCourse_KeepsThatCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        SeedCourses(world.Classroom, world.Time.GetUtcNow(), 1);
        world.Classroom.FailAfterCourses =
            new GoogleReadFailedException(GoogleReadFailureKind.Transient, SyncDiagnosis.GoogleUnavailable);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        Assert.Contains(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        var state = world.States.Stored!;
        Assert.Equal("GoogleUnavailable", state.LastError);
        Assert.Equal(1, state.ProcessedCount);
    }

    /// <summary>
    /// AC-004, FR-005: a course that is gone is skipped whichever of its three reads answers 404; the courses around
    /// it are imported, the run completes with no error, and nothing of the gone course is written — not even the
    /// person only it listed. The control (the same world without the failure) imports course 2.
    /// </summary>
    [Theory]
    [MemberData(nameof(PerCourseReads))]
    public async Task ACourseGone_IsSkipped_AndTheRunCompletes(string read)
    {
        var ct = TestContext.Current.CancellationToken;
        var control = new SyncWorld();
        SeedCourses(control.Classroom, control.Time.GetUtcNow(), 3);
        await control.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Contains(CourseTestData.CourseId(2), control.Courses.Stored.Keys);

        var world = new SyncWorld();
        SeedCourses(world.Classroom, world.Time.GetUtcNow(), 3);
        FailRead(world.Classroom, read, 2, CourseGone());

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
        Assert.Contains(CourseTestData.CourseId(3), world.Classroom.RostersRead);
    }

    /// <summary>
    /// AC-004, FR-005: a course already in the database that is gone in a later run keeps exactly what it held — its
    /// name, its people (none marked off the roster), its items and its submissions — while the other course of the
    /// same run is updated (the control that the run was effective).
    /// </summary>
    [Theory]
    [MemberData(nameof(PerCourseReads))]
    public async Task AStoredCourse_ThatIsGone_IsLeftAsItWas(string read)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        SeedCourses(world.Classroom, world.Time.GetUtcNow(), 3);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var stored = world.Courses.Stored[CourseTestData.CourseId(2)];
        var storedName = stored.Name;
        var membershipsBefore = world.Memberships.OfCourse(stored.Id).Count;
        var itemsBefore = world.CourseWork.Stored.Values.Count(c => c.CourseId == stored.Id);
        var submissionsBefore = world.Submissions.Stored.Count;
        Assert.Equal(1, membershipsBefore);
        Assert.Equal(1, itemsBefore);

        world.Time.Advance(SyncTestData.DefaultInterval);
        var second = new SyncWorld.ClassroomReader()
            .WithCourse(CourseTestData.CourseId(1), "Course 1 renamed in the second run", students: [Person(1)])
            .WithCourse(CourseTestData.CourseId(2), "Renamed in the second run", students: [Person(2)])
            .WithCourse(CourseTestData.CourseId(3), CourseTestData.CourseName(3), students: [Person(3)]);
        FailRead(second, read, 2, CourseGone());

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.False(outcome.Failed);
        Assert.Equal(SyncRunStatus.Completed, world.States.Stored!.Status);
        Assert.Null(world.States.Stored.LastError);
        Assert.Equal(storedName, world.Courses.Stored[CourseTestData.CourseId(2)].Name);
        Assert.NotEqual("Renamed in the second run", world.Courses.Stored[CourseTestData.CourseId(2)].Name);
        Assert.All(world.Memberships.OfCourse(stored.Id), m => Assert.True(m.OnRoster));
        Assert.Equal(membershipsBefore, world.Memberships.OfCourse(stored.Id).Count);
        Assert.Equal(itemsBefore, world.CourseWork.Stored.Values.Count(c => c.CourseId == stored.Id));
        Assert.Equal(submissionsBefore, world.Submissions.Stored.Count);
        Assert.Contains(CourseTestData.CourseId(3), second.RostersRead);
        Assert.Equal("Course 1 renamed in the second run", world.Courses.Stored[CourseTestData.CourseId(1)].Name);
    }

    /// <summary>
    /// AC-005, FR-006, SC-10: a failure that is not a classified Google failure is stored as <c>Unexpected</c> — no
    /// exception type name and no message — whether it comes from the listing or from a course read.
    /// </summary>
    [Theory]
    [InlineData("listing")]
    [InlineData("roster")]
    public async Task AnUnexpectedException_IsStoredAsUnexpected_WithNoDetail(string where)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        SeedCourses(world.Classroom, world.Time.GetUtcNow(), 2);
        var failure = new InvalidOperationException(Marker);
        if (where == "listing")
        {
            world.Classroom.FailOnListing = failure;
        }
        else
        {
            world.Classroom.WithRosterFailure(CourseTestData.CourseId(1), failure);
        }

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        var error = world.States.Stored!.LastError;
        Assert.Equal("Unexpected", error);
        Assert.DoesNotContain(Marker, error, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", error, StringComparison.Ordinal);
        Assert.Equal("Unexpected", outcome.Error);
        Assert.Equal(SyncRunStatus.Failed, world.States.Stored.Status);
    }

    /// <summary>AC-005, FR-001: the adapter's own <c>Unexpected</c> class is stored as <c>Unexpected</c> too.</summary>
    [Fact]
    public async Task AnUnexpectedClassFromTheAdapter_IsStoredAsUnexpected()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        SeedCourses(world.Classroom, world.Time.GetUtcNow(), 2);
        world.Classroom.WithRosterFailure(
            CourseTestData.CourseId(1),
            new GoogleReadFailedException(GoogleReadFailureKind.Unexpected, SyncDiagnosis.Unexpected));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.True(outcome.Failed);
        Assert.Equal("Unexpected", world.States.Stored!.LastError);
        Assert.Equal(SyncRunStatus.Failed, world.States.Stored.Status);
    }

    /// <summary>
    /// AC-010, FR-012, VR-003: a course whose name is empty or whitespace is skipped <b>before</b> its reads and its
    /// transaction; the valid courses around it are imported and the run completes. The control (a valid name for
    /// the same course) imports it.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankCourseName_IsSkipped_AndTheRunCompletes(string blank)
    {
        var ct = TestContext.Current.CancellationToken;
        var control = new SyncWorld();
        SeedCourses(control.Classroom, control.Time.GetUtcNow(), 3);
        await control.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Contains(CourseTestData.CourseId(2), control.Courses.Stored.Keys);

        var world = new SyncWorld();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourse(CourseTestData.CourseId(2), blank, students: [Person(2)])
            .WithCourse(CourseTestData.CourseId(3), CourseTestData.CourseName(3), students: [Person(3)]);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.False(outcome.Failed);
        var state = world.States.Stored!;
        Assert.Equal(SyncRunStatus.Completed, state.Status);
        Assert.Null(state.LastError);
        Assert.Equal(2, state.ProcessedCount);
        Assert.Contains(CourseTestData.CourseId(1), world.Courses.Stored.Keys);
        Assert.Contains(CourseTestData.CourseId(3), world.Courses.Stored.Keys);
        Assert.DoesNotContain(CourseTestData.CourseId(2), world.Courses.Stored.Keys);
        Assert.DoesNotContain(CourseTestData.CourseId(2), world.Classroom.RostersRead);
        Assert.DoesNotContain(CourseTestData.CourseId(2), world.Classroom.CourseWorkRead);
        Assert.DoesNotContain(CourseTestData.CourseId(2), world.Classroom.SubmissionsRead);
    }

    /// <summary>
    /// AC-010, FR-012: a stored course that now arrives with a blank name keeps the name it has, and the run completes;
    /// another course of the same run is updated (the control that the run was effective).
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AStoredCourse_ArrivingBlank_KeepsItsName(string blank)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), "Algebra", students: [Person(1)])
            .WithCourse(CourseTestData.CourseId(2), "Geometry", students: [Person(2)]);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Equal("Algebra", world.Courses.Stored[CourseTestData.CourseId(1)].Name);

        world.Time.Advance(SyncTestData.DefaultInterval);
        var second = new SyncWorld.ClassroomReader()
            .WithCourse(CourseTestData.CourseId(1), blank, students: [Person(1)])
            .WithCourse(CourseTestData.CourseId(2), "Geometry II", students: [Person(2)]);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.False(outcome.Failed);
        Assert.Equal(SyncRunStatus.Completed, world.States.Stored!.Status);
        Assert.Equal("Algebra", world.Courses.Stored[CourseTestData.CourseId(1)].Name);
        Assert.Equal("Geometry II", world.Courses.Stored[CourseTestData.CourseId(2)].Name);
    }

    /// <summary>
    /// FR-006: a completed run after a failed one clears the diagnosis and writes the instant of the last success. The
    /// precondition (the first run stored the code) keeps the clearing from being proved by an empty column.
    /// </summary>
    [Fact]
    public async Task ACompletedRun_AfterAFailedOne_ClearsTheDiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        SeedCourses(world.Classroom, world.Time.GetUtcNow(), 2);
        world.Classroom.FailOnListing = Configuration(SyncDiagnosis.ScopeNotAuthorized);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Equal("ScopeNotAuthorized", world.States.Stored!.LastError);
        Assert.Null(world.States.Stored.LastSuccessfulRunAt);

        world.Classroom.ClearFailures();
        world.Time.Advance(SyncTestData.DefaultInterval);
        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.False(outcome.Failed);
        var state = world.States.Stored;
        Assert.Equal(SyncRunStatus.Completed, state.Status);
        Assert.Null(state.LastError);
        Assert.Equal(2, state.ProcessedCount);
        Assert.Equal(state.FinishedAt, state.LastSuccessfulRunAt);
    }
}
