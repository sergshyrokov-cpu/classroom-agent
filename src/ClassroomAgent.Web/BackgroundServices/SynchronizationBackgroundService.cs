using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Web.Configuration;

namespace ClassroomAgent.Web.BackgroundServices;

/// <summary>
/// The synchronization schedule (US-013 spec FR-003, FR-004, FR-010, FR-011, FR-012; AD-5, NFR-001): a run
/// becomes due one configured interval after the completion of the previous one, one run happens at a time, and
/// the first run of a process waits for the first legitimacy determination. Nothing a run does stops the service
/// or the host.
/// </summary>
/// <remarks>
/// The interval is the same after a success and after a failure (OD-003); retrying the call that actually failed
/// is the Google adapter's business (US-017). A run skipped because of read-only mode or a missing connection writes nothing and says
/// so in one line (OD-005) — the closed list of BR-026 permits no synchronization write.
/// </remarks>
public sealed partial class SynchronizationBackgroundService(
    IServiceScopeFactory scopes,
    SyncRunCoordinator runs,
    SynchronizationServiceMemory memory,
    LegitimacyCheckMemory legitimacy,
    SyncScheduleSettings schedule,
    TimeProvider timeProvider,
    ILogger<SynchronizationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        memory.MarkRunning();
        try
        {
            // The first run waits for the first legitimacy determination: an installation whose check has not
            // answered yet is read-only by BR-025, so a run at the instant of start would always be skipped
            // (spec FR-011, OD-006). The wait is bounded, so a stalled check cannot stop synchronization for
            // the life of the process (spec I-5).
            await WaitForFirstLegitimacyDeterminationAsync(stoppingToken);

            while (true)
            {
                var changed = runs.Changed;
                if (runs.TryStartScheduledRun() || runs.TryStartRequestedRun())
                {
                    await RunOnceAsync(stoppingToken);
                }
                else if (runs.IsPurging || changed.IsCompleted)
                {
                    // US-037 spec FR-014, OD-002: a run that falls due while the retention purge holds the gate starts
                    // when the purge ends — it is not pushed back by a whole interval. The signal captured before the
                    // attempt also covers a purge that ended between the attempt and this check (security review N-2):
                    // the state changed, so the loop decides again instead of sleeping for an interval.
                    await changed.WaitAsync(stoppingToken);
                    continue;
                }

                var dueAt = timeProvider.GetUtcNow() + schedule.Interval;
                await WaitForNextRunAsync(dueAt, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown: no new run starts, and the stop is not a failed run (spec FR-010).
        }
        finally
        {
            memory.MarkStopped();
        }
    }

    /// <summary>
    /// Waits until the next run may start: the scheduled instant, or an out-of-schedule request US-019 will
    /// make. Returns with nothing started — the caller asks the coordinator, which decides atomically.
    /// </summary>
    private async Task WaitForNextRunAsync(DateTimeOffset dueAt, CancellationToken stoppingToken)
    {
        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();
            if (timeProvider.GetUtcNow() >= dueAt)
            {
                return;
            }

            var changed = runs.Changed;
            var remaining = dueAt - timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            // Cancelled on the way out so the timer leaves the clock: a stale one would fire a later advance.
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            try
            {
                await Task.WhenAny(changed, Task.Delay(remaining, timeProvider, wait.Token));
            }
            finally
            {
                await wait.CancelAsync();
            }

            if (runs.IsRequested)
            {
                return;
            }
        }
    }

    private async Task WaitForFirstLegitimacyDeterminationAsync(CancellationToken stoppingToken)
    {
        var giveUpAt = timeProvider.GetUtcNow() + schedule.Interval;
        while (legitimacy.LastOutcome is null && timeProvider.GetUtcNow() < giveUpAt)
        {
            stoppingToken.ThrowIfCancellationRequested();

            // The signal, not a poll: the clock is injected, and a poll would need it to move — which nothing
            // does while the installation is only waiting to learn whether it is legitimate (spec I-5).
            var changed = legitimacy.Changed;
            if (legitimacy.LastOutcome is not null)
            {
                return;
            }

            using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            try
            {
                await Task.WhenAny(changed, Task.Delay(giveUpAt - timeProvider.GetUtcNow(), timeProvider, wait.Token));
            }
            finally
            {
                await wait.CancelAsync();
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        var runId = Guid.NewGuid();
        try
        {
            // Every line of this run carries its identifier, so one run reads end to end (DC-10).
            using var scopeOfRun = logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId });
            SynchronizationRunOutcome outcome;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                LogRunStarted(logger, runId);
                outcome = await scope.ServiceProvider
                    .GetRequiredService<RunSynchronizationUseCase>()
                    .ExecuteAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Only the type: a message may carry remote detail (SC-10). A failing run never stops the
                // service (spec FR-008).
                LogRunException(logger, runId, exception.GetType().Name);
                return;
            }

            LogOutcome(runId, outcome);
        }
        finally
        {
            runs.RunCompleted();
        }
    }

    private void LogOutcome(Guid runId, SynchronizationRunOutcome outcome)
    {
        if (outcome.ReadOnlyReason is { } reason)
        {
            LogRunSkipped(logger, runId, "ReadOnly:" + reason);
        }
        else if (outcome.ConnectionState is { } state)
        {
            LogRunSkipped(logger, runId, "Connection:" + state);
        }
        else
        {
            LogSkippedCourses(runId, outcome);
            LogMeetSkipped(runId, outcome);

            if (outcome.Failed)
            {
                LogFailure(runId, outcome);
                return;
            }

            // US-015 spec VR-004, OD-005: one Warning line per submission whose state Classroom reported outside
            // the six values. The submission itself IS stored — skipping it would read as «не сдано» in a journal
            // — so this line is what makes the unfamiliar value discoverable. Two identifiers, nothing else (SC-10).
            foreach (var unrecognised in outcome.UnrecognisedSubmissions)
            {
                LogSubmissionStateUnrecognised(
                    logger,
                    runId,
                    GoogleLogValue.Bounded(unrecognised.GoogleId),
                    GoogleLogValue.Bounded(unrecognised.State));
            }

            // US-015 spec FR-011, I-5: SyncState has one counter and cannot carry a skipped count, so a course the
            // age rule left unimported is visible only here. Information, not Warning: it is the rule working.
            foreach (var googleId in outcome.CoursesSkippedByAge)
            {
                LogCourseSkippedByAge(logger, runId, GoogleLogValue.Bounded(googleId));
            }

            if (outcome.Meet is { } meet)
            {
                // US-031 spec FR-012: the window and the counts only — no email, meeting code, organizer,
                // participant, endpoint or conference id (SC-10, AC-012, I-6).
                LogMeetStepCompleted(
                    logger,
                    runId,
                    meet.WindowFrom,
                    meet.WindowTo,
                    meet.EventsRead,
                    meet.SessionsAdded,
                    meet.SessionsUpdated,
                    meet.ParticipationsAdded,
                    meet.ParticipationsUpdated,
                    meet.NotOfTheSchool,
                    meet.SkippedTotal);
            }

            LogRunCompleted(logger, runId, outcome.ProcessedCount ?? 0, outcome.MembershipsMarkedOffRoster);
        }
    }

    /// <summary>
    /// US-031 spec FR-012, VR-001: one Warning line per reason invalid Meet events were skipped for — the reason and the
    /// count, never a value of the event (AC-012). Written whether the run completed or stopped later.
    /// </summary>
    private void LogMeetSkipped(Guid runId, SynchronizationRunOutcome outcome)
    {
        if (outcome.Meet is not { } meet)
        {
            return;
        }

        foreach (var (reason, count) in meet.Skipped.OrderBy(pair => pair.Key))
        {
            LogMeetEventsSkipped(logger, runId, reason.ToString(), count);
        }
    }

    /// <summary>
    /// One Warning line per skipped course — not Information, so it does not sink into a run's ordinary lines —
    /// carrying the Google id and, for an unrecognised state, the state; never the course's name (SC-10). Reported
    /// whether the run completed or stopped later, because the skip happened either way. Every Google-supplied
    /// value is bounded (US-017 spec FR-011, I-4).
    /// </summary>
    private void LogSkippedCourses(Guid runId, SynchronizationRunOutcome outcome)
    {
        // US-014 spec FR-003, OD-010: an unrecognised state.
        foreach (var skipped in outcome.SkippedCourses)
        {
            LogCourseSkipped(
                logger,
                runId,
                GoogleLogValue.Bounded(skipped.GoogleId),
                GoogleLogValue.Bounded(skipped.State));
        }

        // US-017 spec FR-005, FR-010: Classroom reported the course gone.
        foreach (var googleId in outcome.CoursesGone)
        {
            LogCourseGone(logger, runId, GoogleLogValue.Bounded(googleId));
        }

        // US-017 spec FR-012, FR-010: the course's name is blank.
        foreach (var googleId in outcome.CoursesWithBlankName)
        {
            LogCourseNameBlank(logger, runId, GoogleLogValue.Bounded(googleId));
        }
    }

    /// <summary>
    /// US-017 spec FR-010, OD-010: a run stopped by a final transient failure is Google's trouble and is a Warning;
    /// a configuration or unexpected stop is an Error. The diagnosis code is a closed-list name; an unexpected
    /// failure also carries the exception <b>type</b> name — never a message (SC-10).
    /// </summary>
    private void LogFailure(Guid runId, SynchronizationRunOutcome outcome)
    {
        var diagnosis = outcome.Diagnosis ?? SyncDiagnosis.Unexpected;
        LogRunFailed(
            logger,
            diagnosis == SyncDiagnosis.GoogleUnavailable ? LogLevel.Warning : LogLevel.Error,
            runId,
            diagnosis.ToString(),
            outcome.FailedStep?.ToString() ?? "none",
            diagnosis == SyncDiagnosis.Unexpected ? outcome.UnexpectedExceptionType ?? "unknown" : "none");
    }

    [LoggerMessage(
        EventId = 5132,
        EventName = "SyncMeetStepCompleted",
        Level = LogLevel.Information,
        Message = "Synchronization run {RunId} Meet step finished for {WindowFrom} to {WindowTo}: {EventsRead} events read, "
            + "{SessionsAdded} meetings new, {SessionsUpdated} updated, {ParticipationsAdded} connections new, "
            + "{ParticipationsUpdated} updated, {NotOfTheSchool} not of the school, {Skipped} skipped")]
    private static partial void LogMeetStepCompleted(
        ILogger logger,
        Guid runId,
        DateTimeOffset windowFrom,
        DateTimeOffset windowTo,
        int eventsRead,
        int sessionsAdded,
        int sessionsUpdated,
        int participationsAdded,
        int participationsUpdated,
        int notOfTheSchool,
        int skipped);

    [LoggerMessage(
        EventId = 5133,
        EventName = "SyncMeetEventsSkipped",
        Level = LogLevel.Warning,
        Message = "Synchronization run {RunId} skipped {Count} invalid Meet events: {Reason}")]
    private static partial void LogMeetEventsSkipped(ILogger logger, Guid runId, string reason, int count);

    [LoggerMessage(
        EventId = 5121,
        EventName = "SyncRunStarted",
        Level = LogLevel.Information,
        Message = "Synchronization run {RunId} started")]
    private static partial void LogRunStarted(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 5122,
        EventName = "SyncRunCompleted",
        Level = LogLevel.Information,
        Message = "Synchronization run {RunId} completed, {ProcessedCount} processed, {MarkedOffRoster} off a roster")]
    private static partial void LogRunCompleted(
        ILogger logger,
        Guid runId,
        int processedCount,
        int markedOffRoster);

    [LoggerMessage(
        EventId = 5127,
        EventName = "SyncSubmissionStateUnrecognised",
        Level = LogLevel.Warning,
        Message = "Synchronization run {RunId} stored submission {SubmissionGoogleId} with an unrecognised state {RawState}")]
    private static partial void LogSubmissionStateUnrecognised(
        ILogger logger,
        Guid runId,
        string submissionGoogleId,
        string rawState);

    [LoggerMessage(
        EventId = 5128,
        EventName = "SyncCourseSkippedByAge",
        Level = LogLevel.Information,
        Message = "Synchronization run {RunId} did not import course {CourseGoogleId}: last activity older than the retention period")]
    private static partial void LogCourseSkippedByAge(ILogger logger, Guid runId, string courseGoogleId);

    [LoggerMessage(
        EventId = 5126,
        EventName = "SyncCourseSkipped",
        Level = LogLevel.Warning,
        Message = "Synchronization run {RunId} skipped course {CourseGoogleId}: unrecognised state {CourseState}")]
    private static partial void LogCourseSkipped(
        ILogger logger,
        Guid runId,
        string courseGoogleId,
        string courseState);

    // One method for the one event: the level is the caller's (Warning for GoogleUnavailable, Error otherwise), because
    // the generator allows one method per event name (SYSLIB1025).
    [LoggerMessage(
        EventId = 5123,
        EventName = "SyncRunFailed",
        Message = "Synchronization run {RunId} failed: {Diagnosis} at step {Step}, exception type {ExceptionType}")]
    private static partial void LogRunFailed(
        ILogger logger,
        LogLevel level,
        Guid runId,
        string diagnosis,
        string step,
        string exceptionType);

    [LoggerMessage(
        EventId = 5130,
        EventName = "SyncCourseGone",
        Level = LogLevel.Warning,
        Message = "Synchronization run {RunId} skipped course {CourseGoogleId}: Classroom reports it gone")]
    private static partial void LogCourseGone(ILogger logger, Guid runId, string courseGoogleId);

    [LoggerMessage(
        EventId = 5131,
        EventName = "SyncCourseNameBlank",
        Level = LogLevel.Warning,
        Message = "Synchronization run {RunId} skipped course {CourseGoogleId}: its name is blank")]
    private static partial void LogCourseNameBlank(ILogger logger, Guid runId, string courseGoogleId);

    [LoggerMessage(
        EventId = 5124,
        EventName = "SyncRunSkipped",
        Level = LogLevel.Information,
        Message = "Synchronization run {RunId} skipped: {Reason}")]
    private static partial void LogRunSkipped(ILogger logger, Guid runId, string reason);

    [LoggerMessage(
        EventId = 5125,
        EventName = "SyncRunException",
        Level = LogLevel.Error,
        Message = "Synchronization run {RunId} failed with an unexpected {ExceptionType}")]
    private static partial void LogRunException(ILogger logger, Guid runId, string exceptionType);
}
