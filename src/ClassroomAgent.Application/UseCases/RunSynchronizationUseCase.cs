using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// A synchronization run (US-013 spec FR-005): (1) the read-only guard runs first, and a
/// <see cref="ReadOnlyModeException"/> becomes the skipped-read-only outcome; (2) only then the saved connection
/// is read, and a connection that is not usable becomes the skipped-connection outcome; (3) only then the run
/// itself happens.
/// </summary>
/// <remarks>
/// A skipped run writes <b>nothing</b>: a synchronization write is not on the BR-026 closed list, so the row is
/// neither created nor updated and no commit happens (US-013 spec FR-007, SC-5). Because the guard is called
/// first, this use case is a protected write path rather than a permitted service write, and
/// <c>PermittedServiceWrites</c> does not grow (US-013 spec FR-006).
/// <para>
/// US-014 adds the pipeline's first step: the school's courses and both rosters of each of them, one course per
/// transaction (spec FR-001, FR-012, OD-009), counted as courses processed (spec FR-013, OD-005).
/// </para>
/// </remarks>
public sealed class RunSynchronizationUseCase(
    ISyncStateRepository syncStates,
    GetWorkspaceConnectionQuery connectionQuery,
    IReadOnlyModeGuard readOnlyMode,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IClassroomReader classroom,
    ICourseRepository courses,
    IClassroomParticipantRepository participants,
    ICourseMembershipRepository memberships)
{
    /// <summary>The operation name the read-only refusal carries (US-007 spec VR-001).</summary>
    public const string Operation = "Sync.Run";

    public async Task<SynchronizationRunOutcome> ExecuteAsync(Guid runId, CancellationToken cancellationToken)
    {
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException refusal)
        {
            return SynchronizationRunOutcome.SkippedReadOnly(refusal.Reason);
        }

        var connection = await connectionQuery.ExecuteAsync(cancellationToken);

        // The technical account every Classroom call is made on behalf of (BR-015, spec S-03). A usable connection
        // always carries one; without it there is nothing to read Google as, which is the same skip.
        if (!connection.IsUsable || connection.SavedImpersonationUserEmail is not { Length: > 0 } impersonationUser)
        {
            return SynchronizationRunOutcome.SkippedConnection(connection.State);
        }

        var state = await BeginAsync(runId, cancellationToken);
        try
        {
            var import = await ImportCoursesAsync(impersonationUser, cancellationToken);
            state.CompleteRun(timeProvider.GetUtcNow(), import.ProcessedCount);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return SynchronizationRunOutcome.Ran(
                runId,
                import.ProcessedCount,
                null,
                import.SkippedCourses,
                import.MembershipsMarkedOffRoster);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A host stop is not a failed run (spec FR-010): the row stays as it is and the next run replaces it.
            throw;
        }
        catch (Exception failure)
        {
            state.FailRun(timeProvider.GetUtcNow(), 0, Diagnosis(failure));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return SynchronizationRunOutcome.Ran(runId, 0, state.LastError);
        }
    }

    /// <summary>
    /// A category and a short sentence, never the exception's own message: SC-10 forbids a raw error text, and a
    /// Google error object would be the worst of them (US-013 spec FR-008, S-06).
    /// </summary>
    private static string Diagnosis(Exception failure) => "RunFailed:" + failure.GetType().Name;

    /// <summary>
    /// US-014 spec VR-002, OD-010: the five values <c>trebovaniya.md</c> §3 lists, as Classroom spells them.
    /// Anything else is not translated into a sixth state — the course is skipped (spec FR-003).
    /// </summary>
    private static bool TryParseState(string reported, out CourseState state)
    {
        switch (reported?.Trim().ToUpperInvariant())
        {
            case "ACTIVE": state = CourseState.Active; return true;
            case "ARCHIVED": state = CourseState.Archived; return true;
            case "PROVISIONED": state = CourseState.Provisioned; return true;
            case "DECLINED": state = CourseState.Declined; return true;
            case "SUSPENDED": state = CourseState.Suspended; return true;
            default: state = default; return false;
        }
    }

    /// <summary>
    /// The teacher and student rosters as one list of people, each with one role and no repetition: a person
    /// Classroom returns on <b>both</b> rosters of one course keeps the role <c>teacher</c>, resolved here — before
    /// the write — so the unique index on (course, person) is never violated (spec FR-007, I-9).
    /// </summary>
    private static List<(RosterEntry Entry, ClassroomRole Role)> RosterOf(CourseRoster roster)
    {
        var people = new List<(RosterEntry Entry, ClassroomRole Role)>();
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in roster.Teachers)
        {
            if (taken.Add(entry.GoogleUserId))
            {
                people.Add((entry, ClassroomRole.Teacher));
            }
        }

        foreach (var entry in roster.Students)
        {
            if (taken.Add(entry.GoogleUserId))
            {
                people.Add((entry, ClassroomRole.Student));
            }
        }

        return people;
    }

    /// <summary>
    /// The row moves to <c>Running</c> and is committed before the run proceeds, so a reader sees the run in
    /// progress (US-013 spec FR-006). The first run of an installation creates the row; its absence until then is
    /// what "never synchronized" means (US-013 spec I-2).
    /// </summary>
    private async Task<SyncState> BeginAsync(Guid runId, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var state = await syncStates.GetAsync(cancellationToken);
        if (state is null)
        {
            state = SyncState.BeginFirstRun(runId, startedAt);
            syncStates.Add(state);
        }
        else
        {
            state.BeginRun(runId, startedAt);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return state;
    }

    /// <summary>
    /// US-014 spec FR-003, FR-004, FR-012: every course of every page, each with both of its rosters, committed one
    /// course at a time. A read that fails propagates, leaving the courses already committed complete (FR-014).
    /// </summary>
    private async Task<ImportResult> ImportCoursesAsync(string impersonationUser, CancellationToken cancellationToken)
    {
        // One instant for every observation of the run, so "seen in the same run" is expressible in a query
        // (spec VR-006, I-8).
        var observedAt = timeProvider.GetUtcNow();
        var processedCount = 0;
        var markedOffRoster = 0;
        var skipped = new List<SkippedCourse>();

        await foreach (var snapshot in classroom.ReadCoursesAsync(impersonationUser, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            if (!TryParseState(snapshot.State, out var state))
            {
                // The one course is skipped and the run completes with the others (spec FR-003, OD-010). It is not
                // counted, so the counter never overstates what was imported (I-5).
                skipped.Add(new SkippedCourse(snapshot.GoogleId, snapshot.State));
                continue;
            }

            // Outside the transaction on purpose: a roster read that fails leaves the roster unknown, so that
            // course is not committed and none of its memberships is marked off the roster (spec FR-010, I-6).
            var roster = await classroom.ReadRosterAsync(impersonationUser, snapshot.GoogleId, cancellationToken);

            var marked = 0;
            await unitOfWork.ExecuteInTransactionAsync(
                async transaction => marked = await ImportCourseAsync(snapshot, state, roster, observedAt, transaction),
                cancellationToken);

            markedOffRoster += marked;
            processedCount++;
        }

        return new ImportResult(processedCount, skipped, markedOffRoster);
    }

    /// <summary>
    /// One course, the people it introduced and its memberships, committed together (spec FR-012, OD-009). Returns
    /// how many memberships this course's roster marked as no longer on it (spec FR-010).
    /// </summary>
    private async Task<int> ImportCourseAsync(
        CourseSnapshot snapshot,
        CourseState state,
        CourseRoster roster,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        // The upsert on the Google id: an already known course is updated in place and keeps its surrogate
        // identity, so everything referring to it still does (spec FR-008).
        var course = await courses.GetByGoogleIdAsync(snapshot.GoogleId, cancellationToken);
        if (course is null)
        {
            course = Course.Import(snapshot.GoogleId, state, snapshot.Details);
            courses.Add(course);
        }
        else
        {
            course.UpdateFrom(state, snapshot.Details);
        }

        var people = RosterOf(roster);
        var stored = await ResolveParticipantsAsync(people, cancellationToken);
        var existing = await memberships.GetByCourseAsync(course.Id, cancellationToken);
        var byParticipant = existing.ToDictionary(m => m.ParticipantId);
        var seenParticipants = new HashSet<long>();

        foreach (var (entry, role) in people)
        {
            var participant = stored[entry.GoogleUserId];
            if (participant.Id != 0 && byParticipant.TryGetValue(participant.Id, out var membership))
            {
                // The same membership is reused, its role refreshed and its first sighting untouched (spec FR-009).
                membership.SeenAgain(role, observedAt);
            }
            else
            {
                memberships.Add(CourseMembership.FirstSeen(course, participant, role, observedAt));
            }

            if (participant.Id != 0)
            {
                seenParticipants.Add(participant.Id);
            }
        }

        var marked = 0;
        foreach (var membership in existing)
        {
            if (!membership.OnRoster || seenParticipants.Contains(membership.ParticipantId))
            {
                continue;
            }

            // Never deleted: the flag falls and the last-seen date stays where it was, because the leaver's own
            // expiry counts from it (spec FR-010, BR-051, PC-11).
            membership.NotOnRoster();
            marked++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return marked;
    }

    /// <summary>
    /// The people of one roster, upserted on the Google <c>userId</c> — the person's only identity (spec FR-006,
    /// OD-011). One query for the whole course rather than one per person (entity model §7).
    /// </summary>
    private async Task<Dictionary<string, ClassroomParticipant>> ResolveParticipantsAsync(
        List<(RosterEntry Entry, ClassroomRole Role)> people,
        CancellationToken cancellationToken)
    {
        var googleUserIds = people.Select(p => p.Entry.GoogleUserId).ToList();
        var stored = await participants.GetByGoogleUserIdsAsync(googleUserIds, cancellationToken);
        var byGoogleUserId = stored.ToDictionary(p => p.GoogleUserId, StringComparer.Ordinal);

        var resolved = new Dictionary<string, ClassroomParticipant>(StringComparer.Ordinal);
        foreach (var (entry, _) in people)
        {
            if (byGoogleUserId.TryGetValue(entry.GoogleUserId, out var participant))
            {
                // A person who changed their address or name in Google keeps their row (entity model §3.2).
                participant.UpdateFrom(entry.Email, entry.FullName);
            }
            else
            {
                participant = ClassroomParticipant.Import(entry.GoogleUserId, entry.Email, entry.FullName);
                participants.Add(participant);
                byGoogleUserId[participant.GoogleUserId] = participant;
            }

            resolved[entry.GoogleUserId] = participant;
        }

        return resolved;
    }

    /// <summary>What the import did, for the outcome the host logs (spec FR-013, FR-016).</summary>
    private sealed record ImportResult(
        int ProcessedCount,
        IReadOnlyList<SkippedCourse> SkippedCourses,
        int MembershipsMarkedOffRoster);
}
