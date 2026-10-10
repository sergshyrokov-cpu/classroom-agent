using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The daily retention purge (US-037; <c>trebovaniya.md</c> §5; PC-11): expired courses, leavers, orphaned
/// participants, unused accounts and old audit rows, then one audit event with the counts. A permitted service write
/// of the BR-026 closed list (<see cref="PermittedServiceWrite.RetentionPurge"/>), so it runs in read-only mode too,
/// and it calls no Google port.
/// </summary>
/// <remarks>
/// Every unit — one expired course, one kept course's leavers, and each of the participant, account and audit-row
/// steps — is its own transaction (spec FR-004, FR-009, I-4). A unit that fails is rolled back whole, reported in the
/// outcome and retried by the next run (OD-003); the run goes on. Counts are added only after their unit commits
/// (VR-003). Shutdown is honoured between units; a unit in flight either commits or rolls back (FR-015).
/// </remarks>
public sealed class RunRetentionPurgeUseCase(
    IRetentionPurgeStore store,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider,
    RetentionSettings retention)
{
    /// <summary>US-031 db-design §6: how many meetings one transaction deletes.</summary>
    public const int MeetBatchSize = 500;

    public async Task<RetentionPurgeOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var cutoff = RetentionRule.Cutoff(startedAt, retention.Years);
        var counts = RetentionPurgeCounts.Zero;
        var failures = new List<RetentionPurgeFailure>();

        using var declaration = writeScope.Declare(PermittedServiceWrite.RetentionPurge);

        // FR-002, FR-003: one transaction per expired course.
        var courses = await store.GetCourseActivityDatesAsync(cancellationToken);
        foreach (var course in courses.Where(c => IsExpired(c, cutoff)))
        {
            if (await TryUnitAsync(
                    RetentionPurgeStep.ExpiredCourse,
                    course.CourseId,
                    ct => store.DeleteCourseAsync(course.CourseId, ct),
                    failures,
                    cancellationToken))
            {
                counts = counts with { Courses = counts.Courses + 1 };
            }
        }

        // FR-005: leavers of the courses that remain, one transaction per course.
        foreach (var courseId in await store.GetCourseIdsWithExpiredLeaversAsync(cutoff, cancellationToken))
        {
            var deleted = 0;
            if (await TryUnitAsync(
                    RetentionPurgeStep.Leavers,
                    courseId,
                    async ct => deleted = await store.DeleteExpiredLeaversAsync(courseId, cutoff, ct),
                    failures,
                    cancellationToken))
            {
                counts = counts with { LeaverMemberships = counts.LeaverMemberships + deleted };
            }
        }

        // US-031 spec FR-013, db-design §6: every meeting whose own date is past the cutoff, with all its connections,
        // whether or not its code is linked — a batch per transaction, so a meeting is never left half-deleted. A batch
        // that fails stops the step (it would select the same ids again); the next run retries it (OD-003).
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expired = await store.GetExpiredMeetSessionIdsAsync(cutoff, MeetBatchSize, cancellationToken);
            if (expired.Count == 0)
            {
                break;
            }

            (int Sessions, int Participations) deleted = default;
            if (!await TryUnitAsync(
                    RetentionPurgeStep.ExpiredMeetings,
                    null,
                    async ct => deleted = await store.DeleteMeetSessionsAsync(expired, ct),
                    failures,
                    cancellationToken))
            {
                break;
            }

            counts = counts with
            {
                MeetSessions = counts.MeetSessions + deleted.Sessions,
                MeetParticipations = counts.MeetParticipations + deleted.Participations,
            };
        }

        // FR-006 … FR-008, in the order FR-009 fixes; the run's own audit row comes after the audit-row delete.
        var participants = 0;
        if (await TryUnitAsync(
                RetentionPurgeStep.OrphanedParticipants,
                null,
                async ct => participants = await store.DeleteOrphanedParticipantsAsync(ct),
                failures,
                cancellationToken))
        {
            counts = counts with { Participants = participants };
        }

        var accounts = 0;
        if (await TryUnitAsync(
                RetentionPurgeStep.Accounts,
                null,
                async ct => accounts = await store.DeleteExpiredAccountsAsync(cutoff, ct),
                failures,
                cancellationToken))
        {
            counts = counts with { Accounts = accounts };
        }

        var auditRows = 0;
        if (await TryUnitAsync(
                RetentionPurgeStep.AuditRows,
                null,
                async ct => auditRows = await store.DeleteAuditEventsOlderThanAsync(cutoff, ct),
                failures,
                cancellationToken))
        {
            counts = counts with { AuditRows = auditRows };
        }

        // FR-010: exactly one row per run that reaches its end. If it cannot be written, the deletions stay (I-5).
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            auditEvents.Add(AuditEvent.RetentionPurgeRun(counts, startedAt));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception failure) when (!IsShutdown(failure, cancellationToken))
        {
            failures.Add(new RetentionPurgeFailure(RetentionPurgeStep.RunAuditEvent, null, failure.GetType().Name));
        }

        return new RetentionPurgeOutcome(cutoff, counts, failures);
    }

    /// <summary>FR-002, OD-006: the latest Google date, or the import time when there is none at all.</summary>
    private static bool IsExpired(CourseActivityDates course, DateTimeOffset cutoff)
    {
        var lastActivity = RetentionRule.LatestActivity(
            [
                course.CourseUpdateTime,
                course.LatestItemCreationTime,
                course.LatestItemUpdateTime,
                course.LatestSubmissionUpdateTime,
            ]) ?? course.CourseCreatedAt;
        return RetentionRule.IsExpired(lastActivity, cutoff);
    }

    /// <summary>Runs one unit in its own transaction; a failure is recorded, never thrown, unless it is shutdown.</summary>
    private async Task<bool> TryUnitAsync(
        RetentionPurgeStep step,
        long? courseId,
        Func<CancellationToken, Task> work,
        List<RetentionPurgeFailure> failures,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(work, cancellationToken);
            return true;
        }
        catch (Exception failure) when (!IsShutdown(failure, cancellationToken))
        {
            failures.Add(new RetentionPurgeFailure(step, courseId, failure.GetType().Name));
            return false;
        }
    }

    private static bool IsShutdown(Exception failure, CancellationToken cancellationToken) =>
        failure is OperationCanceledException && cancellationToken.IsCancellationRequested;
}
