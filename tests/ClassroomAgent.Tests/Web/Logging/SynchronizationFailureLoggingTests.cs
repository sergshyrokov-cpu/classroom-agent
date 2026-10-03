using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-017 AC-003, AC-004, AC-005, AC-009, AC-010, spec FR-005, FR-010, FR-011, FR-012, OD-010: what a run writes to
/// the log when a Google read fails. The Classroom port is substituted and scripted (TC-4); the host, the background
/// service and PostgreSQL are real, and every assertion waits for the log line itself (the known stub-wait race).
/// </summary>
public sealed class SynchronizationFailureLoggingTests(PostgreSqlFixture database)
{
    private const string CourseGoneEvent = "SyncCourseGone";

    private const string CourseNameBlankEvent = "SyncCourseNameBlank";

    private const string CourseSkippedEvent = "SyncCourseSkipped";

    private const string SubmissionStateEvent = "SyncSubmissionStateUnrecognised";

    /// <summary>I-4, FR-011: the longest a Google-supplied value may be in a log line.</summary>
    private const int LogBound = 64;

    private static string Repeating(string pattern, int length) =>
        string.Concat(Enumerable.Repeat(pattern, (length / pattern.Length) + 1))[..length];

    /// <summary>True when any property of the event holds exactly that string, whatever the property is named.</summary>
    /// <summary>
    /// A line of the synchronization run. The host's startup access self-check writes its own Error line in this
    /// keyless test host (US-011), which says nothing about the run's level (corrected at IMPLEMENTATION by Owner
    /// decision).
    /// </summary>
    private static bool IsSynchronizationEvent(LogEvent line) =>
        line.EventName?.StartsWith("Sync", StringComparison.Ordinal) == true;

    private static bool HasPropertyValue(LogEvent line, string value) =>
        line.Json.EnumerateObject().Any(p => p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == value);

    private static RosterEntry Student(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"student{ordinal}"), CourseTestData.Name(ordinal));

    /// <summary>
    /// AC-003, FR-010: a configuration failure stops the run and writes one <b>Error</b> line, carrying the run id and
    /// the diagnosis code.
    /// </summary>
    [Fact]
    public async Task AConfigurationFailure_IsLoggedAtError_WithTheRunIdAndTheCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncFailureHostExtensions.StartFailingAsync(
            database,
            ct,
            failures: reader => reader.CourseListingFailure =
                new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.ScopeNotAuthorized));

        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunFailed, ct);
        var row = await host.WaitForFinishedRunAsync(ct);

        var line = Assert.Single(events, e => e.EventName == SyncTestData.LogEvents.RunFailed);
        Assert.Equal("Error", line.Level);
        Assert.Equal(row.RunId.ToString(), line.Property("RunId"));
        Assert.True(HasPropertyValue(line, "ScopeNotAuthorized"), "The line must carry the diagnosis code.");
        Assert.DoesNotContain("RunFailed:", line.Line, StringComparison.Ordinal);
        Assert.Equal("ScopeNotAuthorized", row.LastError);
        Assert.Single(events, e => e.Level == "Error" && IsSynchronizationEvent(e));
    }

    /// <summary>
    /// FR-010, OD-010: a final transient failure is the Google side's trouble, not the school's — the same event, at
    /// <b>Warning</b>.
    /// </summary>
    [Fact]
    public async Task AFinalTransientFailure_IsLoggedAtWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncFailureHostExtensions.StartFailingAsync(
            database,
            ct,
            failures: reader => reader.CourseListingFailure =
                new GoogleReadFailedException(GoogleReadFailureKind.Transient, SyncDiagnosis.GoogleUnavailable));

        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunFailed, ct);
        var row = await host.WaitForFinishedRunAsync(ct);

        var line = Assert.Single(events, e => e.EventName == SyncTestData.LogEvents.RunFailed);
        Assert.Equal("Warning", line.Level);
        Assert.Equal(row.RunId.ToString(), line.Property("RunId"));
        Assert.True(HasPropertyValue(line, "GoogleUnavailable"), "The line must carry the diagnosis code.");
        Assert.DoesNotContain(events, e => e.Level == "Error" && IsSynchronizationEvent(e));
        Assert.Equal("GoogleUnavailable", row.LastError);
    }

    /// <summary>
    /// AC-005, FR-010, SC-10: an unforeseen failure is logged at <b>Error</b> with the code "Unexpected"; the
    /// exception's message appears in no line, no property and no stored value.
    /// </summary>
    [Fact]
    public async Task AnUnexpectedFailure_IsLoggedAtError_AndLeaksNoExceptionMessage()
    {
        const string marker = "SECRET-DETAIL-4b2d";
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncFailureHostExtensions.StartFailingAsync(
            database,
            ct,
            failures: reader => reader.CourseListingFailure = new InvalidOperationException(marker));

        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunFailed, ct);
        var row = await host.WaitForFinishedRunAsync(ct);
        var all = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));

        var line = Assert.Single(events, e => e.EventName == SyncTestData.LogEvents.RunFailed);
        Assert.Equal("Error", line.Level);
        Assert.Equal(row.RunId.ToString(), line.Property("RunId"));
        Assert.True(HasPropertyValue(line, "Unexpected"), "The line must carry the diagnosis code.");
        Assert.Equal("Unexpected", row.LastError);
        Assert.DoesNotContain(marker, all, StringComparison.Ordinal);
        Assert.DoesNotContain(marker, row.LastError ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-004, FR-005, FR-010: a course that is gone when its roster is read is skipped with a <b>Warning</b> carrying
    /// the run id and the course's Google id; nothing of it is stored, and the run goes on to complete with the other.
    /// </summary>
    [Fact]
    public async Task ACourseGone_IsLoggedAtWarning_AndTheRunCompletesWithTheOtherCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncFailureHostExtensions.StartFailingAsync(
            database,
            ct,
            classroom: reader => reader
                .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Student(1)])
                .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), students: [Student(2)]),
            failures: reader => reader.WithRosterFailure(
                CourseTestData.CourseId(1),
                new GoogleReadFailedException(GoogleReadFailureKind.CourseGone, null)));

        // The stored row first: a run that failed instead of completing is reported at once, not as a log timeout.
        var row = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, row.Status);
        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var line = Assert.Single(events, e => e.EventName == CourseGoneEvent);
        Assert.Equal("Warning", line.Level);
        Assert.Equal(row.RunId.ToString(), line.Property("RunId"));
        Assert.True(HasPropertyValue(line, CourseTestData.CourseId(1)), "The line must carry the course's Google id.");
        Assert.DoesNotContain(events, e => e.EventName == SyncTestData.LogEvents.RunFailed);
        Assert.Equal(SyncTestData.Status.Completed, row.Status);
        Assert.Equal([CourseTestData.CourseId(2)], await host.QueryAsync("SELECT google_id FROM course", r => r.GetString(0), ct));
    }

    /// <summary>
    /// AC-010, FR-012: a course whose name is blank is skipped before its reads, with a <b>Warning</b> carrying the run
    /// id and the course's Google id; the run completes with the other course.
    /// </summary>
    [Fact]
    public async Task ABlankCourseName_IsLoggedAtWarning_AndTheRunCompletesWithTheOtherCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader
                .WithCourse(CourseTestData.CourseId(1), "   ")
                .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2)));

        // The stored row first: a run that failed instead of completing is reported at once, not as a log timeout.
        var row = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, row.Status);
        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var line = Assert.Single(events, e => e.EventName == CourseNameBlankEvent);
        Assert.Equal("Warning", line.Level);
        Assert.Equal(row.RunId.ToString(), line.Property("RunId"));
        Assert.True(HasPropertyValue(line, CourseTestData.CourseId(1)), "The line must carry the course's Google id.");
        Assert.DoesNotContain(events, e => e.EventName == SyncTestData.LogEvents.RunFailed);
        Assert.Equal(SyncTestData.Status.Completed, row.Status);
        Assert.Equal([CourseTestData.CourseId(2)], await host.QueryAsync("SELECT google_id FROM course", r => r.GetString(0), ct));
    }

    /// <summary>
    /// AC-009, FR-011, VR-004: a course id and a state of 200 characters reach the log as their first 64 characters,
    /// and the whole value appears nowhere.
    /// </summary>
    [Fact]
    public async Task ALongCourseIdAndState_AreTruncatedToTheBound()
    {
        var ct = TestContext.Current.CancellationToken;
        var longId = Repeating("COURSEID0123456789", 200);
        var longState = Repeating("UNKNOWN_STATE_", 200);
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader.WithCourse(longId, CourseTestData.CourseName(1), longState));

        await host.WaitForLogEventAsync(CourseSkippedEvent, ct);
        var events = await HostLogs.ReadEventsAsync(host.LogDirectory, ct);
        var all = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));

        var line = Assert.Single(events, e => e.EventName == CourseSkippedEvent);
        var id = line.Property("CourseGoogleId")!;
        var state = line.Property("CourseState")!;
        Assert.True(id.Length <= LogBound, $"The course id was logged with {id.Length} characters.");
        Assert.True(state.Length <= LogBound, $"The course state was logged with {state.Length} characters.");
        Assert.Equal(longId[..LogBound], id);
        Assert.Equal(longState[..LogBound], state);
        Assert.DoesNotContain(longId, all, StringComparison.Ordinal);
        Assert.DoesNotContain(longState, all, StringComparison.Ordinal);
    }

    /// <summary>The control for the bound: a short value is logged exactly as Google sent it.</summary>
    [Fact]
    public async Task AShortCourseIdAndState_AreLoggedUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader.WithCourse("ID01234567", CourseTestData.CourseName(1), "STATE_ABCD"));

        await host.WaitForLogEventAsync(CourseSkippedEvent, ct);
        var events = await HostLogs.ReadEventsAsync(host.LogDirectory, ct);

        var line = Assert.Single(events, e => e.EventName == CourseSkippedEvent);
        Assert.Equal("ID01234567", line.Property("CourseGoogleId"));
        Assert.Equal("STATE_ABCD", line.Property("CourseState"));
    }

    private static FakeClassroomReader SubmissionWorld(FakeClassroomReader reader, string submissionId, string rawState) =>
        reader
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Student(1)])
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                itemDate: InstallationTestHost.DefaultStart,
                maxPoints: 100m)
            .WithSubmission(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseTestData.UserId(1),
                submissionId,
                rawState);

    /// <summary>
    /// AC-009, FR-011, VR-004: a submission id and a raw state of 200 characters reach the log as their first 64
    /// characters.
    /// </summary>
    [Fact]
    public async Task ALongSubmissionIdAndRawState_AreTruncatedToTheBound()
    {
        var ct = TestContext.Current.CancellationToken;
        var longId = Repeating("SUBMISSIONID0123456789", 200);
        var longState = Repeating("ODD_RAW_STATE_", 200);
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => SubmissionWorld(reader, longId, longState));

        await host.WaitForLogEventAsync(SubmissionStateEvent, ct);
        var events = await HostLogs.ReadEventsAsync(host.LogDirectory, ct);
        var all = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));

        var line = Assert.Single(events, e => e.EventName == SubmissionStateEvent);
        var id = line.Property("SubmissionGoogleId")!;
        var state = line.Property("RawState")!;
        Assert.True(id.Length <= LogBound, $"The submission id was logged with {id.Length} characters.");
        Assert.True(state.Length <= LogBound, $"The raw state was logged with {state.Length} characters.");
        Assert.Equal(longId[..LogBound], id);
        Assert.Equal(longState[..LogBound], state);
        Assert.DoesNotContain(longId, all, StringComparison.Ordinal);
        Assert.DoesNotContain(longState, all, StringComparison.Ordinal);
    }

    /// <summary>The control for the bound: a short submission id and state are logged unchanged.</summary>
    [Fact]
    public async Task AShortSubmissionIdAndRawState_AreLoggedUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => SubmissionWorld(reader, "SUB0123456", "ODD_STATE1"));

        await host.WaitForLogEventAsync(SubmissionStateEvent, ct);
        var events = await HostLogs.ReadEventsAsync(host.LogDirectory, ct);

        var line = Assert.Single(events, e => e.EventName == SubmissionStateEvent);
        Assert.Equal("SUB0123456", line.Property("SubmissionGoogleId"));
        Assert.Equal("ODD_STATE1", line.Property("RawState"));
    }

    /// <summary>
    /// AC-003, FR-005, OD-004: after a failed run the next one is due one normal interval after the failure — no
    /// shorter and no longer. The failure is real (a scripted configuration failure), not a hand-edited row; US-013's
    /// <c>AfterAFailedRun_TheIntervalIsUnchanged</c> proves the same for a row edited by hand and stays valid.
    /// </summary>
    [Fact]
    public async Task AfterAConfigurationFailure_TheNextRunIsDueAfterTheNormalInterval()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncFailureHostExtensions.StartFailingAsync(
            database,
            ct,
            failures: reader => reader.CourseListingFailure =
                new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.KeyRejected),
            intervalMinutes: 30);
        var failed = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Failed, failed.Status);

        await host.Time.WaitForTimerAtAsync(failed.FinishedAt!.Value + TimeSpan.FromMinutes(30), ct);

        Assert.Contains(failed.FinishedAt!.Value + TimeSpan.FromMinutes(30), host.Time.PendingDueTimes);
    }
}
