using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>The Database page (US-024 spec FR-001 … FR-008, entity model §3). Writes nothing; permitted in read-only mode.</summary>
/// <remarks>
/// Two reads, whatever the size of the school (db-design §1, AC-017): the eleven counters in one statement and the
/// <c>SyncState</c> row. Only the state and the three times leave this class — the stored diagnosis, the failed step and
/// the processed counter have no field in the DTO (spec FR-005, S-08). No read-only guard: viewing is permitted in every
/// mode (spec FR-008).
/// </remarks>
public sealed class GetDatabaseStatisticsQuery(
    IDatabaseStatisticsSource counts,
    ISyncStateRepository states,
    SchoolTimeZone schoolTimeZone)
{
    public async Task<DatabaseStatisticsView> ExecuteAsync(AppRole role, CancellationToken cancellationToken)
    {
        var stored = await counts.GetCountsAsync(cancellationToken);
        var state = await states.GetForReadAsync(cancellationToken);

        var synchronization = state is null
            ? new SynchronizationBlock(SynchronizationDisplayState.NotYet, null, null, null)
            : new SynchronizationBlock(
                state.Status switch
                {
                    SyncRunStatus.Running => SynchronizationDisplayState.InProgress,
                    SyncRunStatus.Completed => SynchronizationDisplayState.Completed,
                    SyncRunStatus.Failed => SynchronizationDisplayState.Failed,
                    _ => throw new ArgumentOutOfRangeException(nameof(state), state.Status, null),
                },
                Local(state.LastSuccessfulRunAt),
                Local(state.LastFullRunAt),
                Local(state.MeetLoadedUpTo));

        // Spec FR-006, I-7: guidance only for a failed run, chosen by the session's role.
        var guidance = synchronization.State != SynchronizationDisplayState.Failed
            ? FailureGuidance.None
            : role switch
            {
                AppRole.Dean => FailureGuidance.AskAdmin,
                AppRole.Admin => FailureGuidance.ConnectionPageLink,
                _ => FailureGuidance.None,
            };

        // Spec I-3: the total is the sum of the five states.
        var courses = new CourseCounts(
            stored.ActiveCourses,
            stored.ArchivedCourses,
            stored.ProvisionedCourses,
            stored.DeclinedCourses,
            stored.SuspendedCourses,
            stored.ActiveCourses + stored.ArchivedCourses + stored.ProvisionedCourses + stored.DeclinedCourses
                + stored.SuspendedCourses);

        return new DatabaseStatisticsView(
            courses,
            stored.Teachers,
            stored.Students,
            stored.CourseWorkItems,
            stored.Submissions,
            stored.MeetMeetings,
            stored.MeetCodesAwaitingDecision,
            synchronization,
            guidance);
    }

    /// <summary>Spec FR-005, AC-014: a stored UTC instant as wall-clock time of the school's zone, so the view does no arithmetic (AD-3).</summary>
    private DateTime? Local(DateTimeOffset? instant) =>
        instant is { } value ? TimeZoneInfo.ConvertTime(value, schoolTimeZone.Zone).DateTime : null;
}
