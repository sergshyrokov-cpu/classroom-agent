using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using static ClassroomAgent.Tests.TestInfrastructure.MeetLinkingTestData;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-053 AC-001 … AC-005, AC-007 (spec FR-002 … FR-007): a run that stops on a failure is recorded as failed, through
/// the real host, its background service and PostgreSQL (TC-2), the Google ports substituted (TC-4). The database is
/// made to reject a write by a CHECK constraint this test adds to its own database (spec I-5, §7: no seam in src);
/// the non-database failure of AC-007 is synthetic Google data the domain refuses inside the course's transaction.
/// </summary>
public sealed class FailedRunRecordedTests(PostgreSqlFixture database)
{
    private const string RejectCourseConstraint = "us053_test_reject_course";

    private const string RejectConferenceConstraint = "us053_test_reject_conference";

    private static readonly DateTimeOffset LeftAt = InstallationTestHost.DefaultStart - TimeSpan.FromDays(1);

    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    /// <summary>Two courses, one student each, one item each; the second is the one a test makes fail.</summary>
    private static void TwoCourses(FakeClassroomReader reader) =>
        reader
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithCourseWork(CourseTestData.CourseId(1), CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), InstallationTestHost.DefaultStart)
            .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), students: [Person(2)])
            .WithCourseWork(CourseTestData.CourseId(2), CourseWorkTestData.ItemId(2), CourseWorkResource.CourseWork, CourseWorkTestData.Title(2), InstallationTestHost.DefaultStart);

    /// <summary>Spec I-5: a constraint the second course's data violates, in this test's database only.</summary>
    private static Task RejectSecondCourseAsync(InstallationTestHost host, CancellationToken ct) =>
        host.ExecuteAsync(
            $"ALTER TABLE course ADD CONSTRAINT {RejectCourseConstraint} CHECK (google_id <> '{CourseTestData.CourseId(2)}')",
            ct);

    /// <summary>AC-005: the cause is gone — the data no longer violates a constraint.</summary>
    private static Task AllowSecondCourseAsync(InstallationTestHost host, CancellationToken ct) =>
        host.ExecuteAsync($"ALTER TABLE course DROP CONSTRAINT {RejectCourseConstraint}", ct);

    /// <summary>Spec I-5: a constraint the second Meet page's conference violates, in this test's database only.</summary>
    private static Task RejectSecondConferenceAsync(InstallationTestHost host, CancellationToken ct) =>
        host.ExecuteAsync(
            $"ALTER TABLE meet_session ADD CONSTRAINT {RejectConferenceConstraint} CHECK (conference_id <> '{MeetTestData.ConferenceId(2)}')",
            ct);

    private static Task<long> CoursesWithAsync(InstallationTestHost host, string googleId, CancellationToken ct) =>
        host.CountAsync("course", ct, "google_id = @id", ("id", googleId));

    private static Task<long> ParticipantsWithAsync(InstallationTestHost host, string googleUserId, CancellationToken ct) =>
        host.CountAsync("classroom_participant", ct, "google_user_id = @id", ("id", googleUserId));

    private static Task<long> CourseWorkWithAsync(InstallationTestHost host, string googleId, CancellationToken ct) =>
        host.CountAsync("course_work", ct, "google_id = @id", ("id", googleId));

    private static Task<long> SessionsOfAsync(InstallationTestHost host, int conference, CancellationToken ct) =>
        host.CountAsync("meet_session", ct, "conference_id = @id", ("id", MeetTestData.ConferenceId(conference)));

    private static Task<string?> FailedStepAsync(InstallationTestHost host, CancellationToken ct) =>
        host.ScalarAsync<string>("SELECT failed_step FROM sync_state", ct);

    private static Task<object?> MeetLoadedUpToAsync(InstallationTestHost host, CancellationToken ct) =>
        host.ScalarAsync<object>("SELECT meet_loaded_up_to FROM sync_state", ct);

    /// <summary>
    /// Waits for the first run to finish. A failure save that throws leaves the row <c>running</c> and the host writes
    /// its "escaped" line (event SyncRunException) — the defect this Story removes — so that line ends the wait at once
    /// with a clear failure instead of a timeout.
    /// </summary>
    private static async Task<SyncHostExtensions.SyncStateRow> WaitForRecordedRunAsync(InstallationTestHost host, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + ManualTimeProvider.RealTimeLimit;
        while (DateTime.UtcNow < deadline)
        {
            if (await host.SyncStatesAsync(ct) is [{ } row] && row.Status != SyncTestData.Status.Running)
            {
                return row;
            }

            if ((await host.ReadLogFilesWhileRunningAsync(ct)).Any(file => file.Contains("SyncRunException", StringComparison.Ordinal)))
            {
                Assert.Fail("The run's failure save threw: the host wrote SyncRunException and sync_state stays 'running'.");
            }

            await Task.Delay(25, ct);
        }

        Assert.Fail($"No finished synchronization run appeared within {ManualTimeProvider.RealTimeLimit}.");
        throw new InvalidOperationException("unreachable");
    }

    private static async Task<LastSynchronizationView> LastSynchronizationAsync(InstallationTestHost host, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        return await new GetLastSynchronizationQuery(
                scope.ServiceProvider.GetRequiredService<ISyncStateRepository>(),
                scope.ServiceProvider.GetRequiredService<SchoolTimeZone>())
            .ExecuteAsync(ct);
    }

    /// <summary>
    /// AC-001, FR-002 … FR-004: the second course's write is rejected by the database; the run is recorded as failed at
    /// the Classroom step with the first course counted and stored, nothing of the second stored, and no success moved.
    /// </summary>
    [Fact]
    public async Task ARejectedCourseWrite_IsRecordedAsAFailedRunAtTheClassroomStep()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, classroom: TwoCourses, seed: RejectSecondCourseAsync);

        var run = await WaitForRecordedRunAsync(host, ct);

        Assert.Equal(SyncTestData.Status.Failed, run.Status);
        Assert.Equal(nameof(SyncDiagnosis.Unexpected), run.LastError);
        Assert.Equal("classroom", await FailedStepAsync(host, ct));
        Assert.Equal(1, run.ProcessedCount);
        Assert.NotNull(run.FinishedAt);
        Assert.Null(run.LastSuccessfulRunAt);
        Assert.Null(await MeetLoadedUpToAsync(host, ct));
        Assert.Equal(1, await CoursesWithAsync(host, CourseTestData.CourseId(1), ct));
        Assert.Equal(1, await ParticipantsWithAsync(host, CourseTestData.UserId(1), ct));
        Assert.Equal(0, await CoursesWithAsync(host, CourseTestData.CourseId(2), ct));
        Assert.Equal(0, await ParticipantsWithAsync(host, CourseTestData.UserId(2), ct));
        Assert.Equal(0, await CourseWorkWithAsync(host, CourseWorkTestData.ItemId(2), ct));
    }

    /// <summary>
    /// AC-001, FR-005: the use case returns its failed outcome, so the host writes its one SyncRunFailed line (diagnosis
    /// code and exception type only) and never the "exception escaped the run" line.
    /// </summary>
    [Fact]
    public async Task ARejectedCourseWrite_LogsTheFailureLine_AndNotTheEscapedLine()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, classroom: TwoCourses, seed: RejectSecondCourseAsync);

        await WaitForRecordedRunAsync(host, ct);
        await host.WaitForLogEventAsync("SyncRunFailed", ct);
        var events = await host.ReadLogEventsAsync(ct);

        var failed = Assert.Single(events, e => e.EventName == "SyncRunFailed");
        Assert.Equal("Error", failed.Level);
        Assert.Equal(nameof(SyncDiagnosis.Unexpected), failed.Property("Diagnosis"));
        Assert.Equal(nameof(SyncStep.Classroom), failed.Property("Step"));
        Assert.DoesNotContain(CourseTestData.CourseId(2), failed.Line, StringComparison.Ordinal);
        Assert.DoesNotContain(RejectCourseConstraint, failed.Line, StringComparison.Ordinal);
        Assert.DoesNotContain(events, e => e.EventName == "SyncRunException");
    }

    /// <summary>
    /// AC-002, FR-003, FR-004: the Classroom step completes and the second Meet page's write is rejected; the run is
    /// recorded as failed at the Meet step, the courses and the first page stay, nothing of the second page is stored.
    /// </summary>
    [Fact]
    public async Task ARejectedMeetPageWrite_IsRecordedAsAFailedRunAtTheMeetStep()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]),
            seed: async (h, token) =>
            {
                h.Meet
                    .WithPage(MeetTestData.Event(1, 1, LeftAt, 600, MeetTestData.Teacher(1), MeetTestData.Student(1)))
                    .WithPage(MeetTestData.Event(2, 2, LeftAt, 600, MeetTestData.Teacher(1), MeetTestData.Student(2)));
                await RejectSecondConferenceAsync(h, token);
            });

        var run = await WaitForRecordedRunAsync(host, ct);

        Assert.Equal(SyncTestData.Status.Failed, run.Status);
        Assert.Equal(nameof(SyncDiagnosis.Unexpected), run.LastError);
        Assert.Equal("meet", await FailedStepAsync(host, ct));
        Assert.Equal(1, run.ProcessedCount);
        Assert.Null(await MeetLoadedUpToAsync(host, ct));
        Assert.Equal(1, await CoursesWithAsync(host, CourseTestData.CourseId(1), ct));
        Assert.Equal(1, await SessionsOfAsync(host, 1, ct));
        Assert.Equal(0, await SessionsOfAsync(host, 2, ct));
        Assert.Equal(
            0,
            await host.CountAsync(
                "meet_participation",
                ct,
                "endpoint_id = @id",
                ("id", MeetTestData.EndpointId(2))));
    }

    /// <summary>
    /// AC-003, FR-004, FR-008: the linking step's write is rejected; the run is recorded as failed at the Linking step and
    /// what the Classroom and Meet steps committed stays. May pass before the change (US-032 already detaches a failed
    /// link) — kept as the regression test the Story asks for.
    /// </summary>
    [Fact]
    public async Task ARejectedLinkingWrite_IsRecordedAsAFailedRunAtTheLinkingStep_AndKeepsWhatEarlierStepsCommitted()
    {
        var ct = TestContext.Current.CancellationToken;
        var classroomCourse = CourseTestData.CourseId(7);
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader.WithCourse(classroomCourse, CourseTestData.CourseName(7), students: [Person(7)]),
            seed: async (h, token) =>
            {
                await SeedClearRostersAsync(h, token);
                await FailLinkInsertsAsync(h, token);
                h.Meet.WithPage(FirstMeetingEvents());
            });

        var run = await WaitForRecordedRunAsync(host, ct);

        Assert.Equal(SyncTestData.Status.Failed, run.Status);
        Assert.Equal(nameof(SyncDiagnosis.Unexpected), run.LastError);
        Assert.Equal("linking", await FailedStepAsync(host, ct));
        Assert.Equal(1, await CoursesWithAsync(host, classroomCourse, ct));
        Assert.Equal(1, await ParticipantsWithAsync(host, CourseTestData.UserId(7), ct));
        Assert.Equal(1L, await host.CountAsync("meet_session", ct));
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct));
        Assert.Equal(0L, await host.CountAsync("audit_event", ct, $"action = '{AutoLinkedAction}'"));
    }

    /// <summary>
    /// AC-004, FR-006, spec I-6: from the state a real failed run left, the connection page's "Last synchronization" query
    /// returns the failed status, the "unexpected error" diagnosis and the step — not "in progress".
    /// </summary>
    [Fact]
    public async Task AfterARejectedCourseWrite_TheLastSynchronizationBlockShowsTheFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, classroom: TwoCourses, seed: RejectSecondCourseAsync);
        await WaitForRecordedRunAsync(host, ct);

        var view = await LastSynchronizationAsync(host, ct);

        Assert.Equal(LastSynchronizationStatus.Failed, view.Status);
        Assert.Equal(SyncDiagnosis.Unexpected, view.Diagnosis);
        Assert.Equal(SyncStep.Classroom, view.FailedStep);
        Assert.NotNull(view.FinishedAt);
        Assert.Null(view.LastSuccessfulRunAt);
    }

    /// <summary>
    /// AC-005, FR-007: once the data no longer violates the constraint, the next scheduled run completes and clears the
    /// diagnosis and the step; the course the failed run could not store is stored now.
    /// </summary>
    [Fact]
    public async Task AfterARejectedCourseWrite_TheNextRunCompletes_WhenTheCauseIsGone()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            intervalMinutes: 1,
            classroom: TwoCourses,
            seed: RejectSecondCourseAsync);
        var first = await WaitForRecordedRunAsync(host, ct);
        Assert.Equal(SyncTestData.Status.Failed, first.Status);

        await AllowSecondCourseAsync(host, ct);
        await host.WaitForSecondRunAsync(first, ct);

        var second = Assert.Single(await host.SyncStatesAsync(ct));
        Assert.NotEqual(first.RunId, second.RunId);
        Assert.Equal(SyncTestData.Status.Completed, second.Status);
        Assert.Null(second.LastError);
        Assert.Null(await FailedStepAsync(host, ct));
        Assert.NotNull(second.LastSuccessfulRunAt);
        Assert.Equal(2, second.ProcessedCount);
        Assert.Equal(1, await CoursesWithAsync(host, CourseTestData.CourseId(2), ct));
    }

    /// <summary>
    /// AC-007, FR-002, FR-003, I-3: an exception that is not a database rejection, thrown inside the second course's
    /// transaction after the course and one of its people were staged and before any save — a roster entry Google
    /// returned without a user id, which the domain refuses. The failure save commits nothing of that course.
    /// </summary>
    [Fact]
    public async Task ANonDatabaseFailureBeforeTheCourseSaved_StoresNothingOfThatCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var nameless = new RosterEntry(" ", CourseTestData.Email("nameless"), CourseTestData.Name(9));
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader
                .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
                .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), students: [Person(2), nameless]));

        var run = await WaitForRecordedRunAsync(host, ct);

        Assert.Equal(SyncTestData.Status.Failed, run.Status);
        Assert.Equal(nameof(SyncDiagnosis.Unexpected), run.LastError);
        Assert.Equal("classroom", await FailedStepAsync(host, ct));
        Assert.Equal(1, run.ProcessedCount);
        Assert.Equal(1, await CoursesWithAsync(host, CourseTestData.CourseId(1), ct));
        Assert.Equal(0, await CoursesWithAsync(host, CourseTestData.CourseId(2), ct));
        Assert.Equal(0, await ParticipantsWithAsync(host, CourseTestData.UserId(2), ct));
        Assert.Equal(1L, await host.CountAsync("course", ct));
        Assert.Equal(1L, await host.CountAsync("classroom_participant", ct));
    }

    /// <summary>
    /// AC-007, FR-002, FR-003, I-3: the same kind of failure after the course's first save inside its transaction — an item
    /// Google returned without a title, refused by the domain while an earlier item of the course is staged. The rolled-back
    /// course's staged item is not written by the failure save, and the run is recorded as failed.
    /// </summary>
    [Fact]
    public async Task ANonDatabaseFailureAfterTheCourseSavedInItsTransaction_StoresNothingOfThatCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader
                .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
                .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), students: [Person(2)])
                .WithCourseWork(CourseTestData.CourseId(2), CourseWorkTestData.ItemId(2), CourseWorkResource.CourseWork, CourseWorkTestData.Title(2), InstallationTestHost.DefaultStart)
                .WithCourseWork(CourseTestData.CourseId(2), CourseWorkTestData.ItemId(3), CourseWorkResource.CourseWork, " ", InstallationTestHost.DefaultStart));

        var run = await WaitForRecordedRunAsync(host, ct);

        Assert.Equal(SyncTestData.Status.Failed, run.Status);
        Assert.Equal(nameof(SyncDiagnosis.Unexpected), run.LastError);
        Assert.Equal("classroom", await FailedStepAsync(host, ct));
        Assert.Equal(1, run.ProcessedCount);
        Assert.Equal(0, await CoursesWithAsync(host, CourseTestData.CourseId(2), ct));
        Assert.Equal(0, await ParticipantsWithAsync(host, CourseTestData.UserId(2), ct));
        Assert.Equal(0L, await host.CountAsync("course_work", ct));
    }
}
