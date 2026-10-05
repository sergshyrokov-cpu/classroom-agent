using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

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
    ICourseMembershipRepository memberships,
    ICourseWorkRepository courseWork,
    ISubmissionRepository submissions,
    RetentionSettings retention)
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
        var import = new ImportProgress();
        try
        {
            await ImportCoursesAsync(impersonationUser, import, cancellationToken);
            state.CompleteRun(timeProvider.GetUtcNow(), import.ProcessedCount);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return SynchronizationRunOutcome.Ran(
                runId,
                import.ProcessedCount,
                null,
                import.SkippedCourses,
                import.MembershipsMarkedOffRoster,
                import.CoursesSkippedByAge,
                import.UnrecognisedSubmissions,
                import.CoursesGone,
                import.CoursesWithBlankName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A host stop is not a failed run (spec FR-010): the row stays as it is and the next run replaces it.
            throw;
        }
        catch (Exception failure)
        {
            // US-017 spec FR-005, FR-006: the run stops; the courses committed before the stop stay and are counted
            // (I-5). The stored value is a code of the closed list, never the exception's own message or type
            // (SC-10); the type name travels in the outcome for the host's one Error line (spec FR-010).
            var diagnosis = DiagnosisOf(failure);
            state.FailRun(timeProvider.GetUtcNow(), import.ProcessedCount, diagnosis);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return SynchronizationRunOutcome.RanAndFailed(
                runId,
                import.ProcessedCount,
                diagnosis,
                diagnosis == SyncDiagnosis.Unexpected ? failure.GetType().Name : null,
                import.SkippedCourses,
                import.CoursesSkippedByAge,
                import.CoursesGone,
                import.CoursesWithBlankName);
        }
    }

    /// <summary>
    /// The adapter's own diagnosis when it classified the failure; anything else, including a classified failure
    /// that carries none, is <see cref="SyncDiagnosis.Unexpected"/> (US-017 spec FR-006).
    /// </summary>
    private static SyncDiagnosis DiagnosisOf(Exception failure) =>
        failure is GoogleReadFailedException { Diagnosis: { } diagnosis } ? diagnosis : SyncDiagnosis.Unexpected;

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
    /// course at a time. A read that fails propagates, leaving the courses already committed complete (FR-014), except
    /// a course Classroom reports gone, which is skipped (US-017 spec FR-005). What was done so far is kept in
    /// <paramref name="progress"/>, so a failed run can still report the courses committed before it stopped.
    /// </summary>
    private async Task ImportCoursesAsync(
        string impersonationUser,
        ImportProgress progress,
        CancellationToken cancellationToken)
    {
        // One instant for every observation of the run, so "seen in the same run" is expressible in a query
        // (spec VR-006, I-8).
        var observedAt = timeProvider.GetUtcNow();

        await foreach (var snapshot in classroom.ReadCoursesAsync(impersonationUser, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            if (!TryParseState(snapshot.State, out var state))
            {
                // The one course is skipped and the run completes with the others (spec FR-003, OD-010). It is not
                // counted, so the counter never overstates what was imported (I-5).
                progress.SkippedCourses.Add(new SkippedCourse(snapshot.GoogleId, snapshot.State));
                continue;
            }

            if (string.IsNullOrWhiteSpace(snapshot.Details.Name))
            {
                // US-017 spec FR-012: skipped before its reads and its transaction, so nothing of it is written and
                // a stored course keeps the name it has. Not counted (I-5).
                progress.CoursesWithBlankName.Add(snapshot.GoogleId);
                continue;
            }

            CourseRoster roster;
            CourseWorkPage items;
            IReadOnlyList<SubmissionSnapshot> submissionSnapshots;
            try
            {
                // Outside the transaction on purpose: a roster read that fails leaves the roster unknown, so that
                // course is not committed and none of its memberships is marked off the roster (spec FR-010, I-6).
                roster = await classroom.ReadRosterAsync(impersonationUser, snapshot.GoogleId, cancellationToken);

                // US-015 spec FR-003, FR-004: the course's items and its submissions, both read before anything is
                // written, which is also what lets FR-011 judge an unknown course's age from Google's data alone.
                items = await classroom.ReadCourseWorkAsync(impersonationUser, snapshot.GoogleId, cancellationToken);
                submissionSnapshots = await classroom.ReadSubmissionsAsync(
                    impersonationUser,
                    snapshot.GoogleId,
                    cancellationToken);
            }
            catch (GoogleReadFailedException gone) when (gone.Kind == GoogleReadFailureKind.CourseGone)
            {
                // US-017 spec FR-005: the course vanished between the listing and its reads. Every read precedes the
                // transaction, so nothing of it is written and a stored copy stays as it was.
                progress.CoursesGone.Add(snapshot.GoogleId);
                continue;
            }

            var result = default(CourseResult);
            await unitOfWork.ExecuteInTransactionAsync(
                async transaction => result = await ImportCourseAsync(
                    snapshot,
                    state,
                    roster,
                    items,
                    submissionSnapshots,
                    observedAt,
                    transaction),
                cancellationToken);

            if (result.SkippedByAge)
            {
                // US-015 spec FR-011, I-5: nothing was written and the course is not counted, so the counter never
                // claims data the installation does not hold. The host writes the one line that makes it visible.
                progress.CoursesSkippedByAge.Add(snapshot.GoogleId);
                continue;
            }

            progress.MembershipsMarkedOffRoster += result.MembershipsMarkedOffRoster;
            progress.UnrecognisedSubmissions.AddRange(result.UnrecognisedSubmissions);
            progress.ProcessedCount++;
        }
    }

    /// <summary>
    /// One course, the people it introduced and its memberships, committed together (spec FR-012, OD-009). Returns
    /// how many memberships this course's roster marked as no longer on it (spec FR-010).
    /// </summary>
    private async Task<CourseResult> ImportCourseAsync(
        CourseSnapshot snapshot,
        CourseState state,
        CourseRoster roster,
        CourseWorkPage items,
        IReadOnlyList<SubmissionSnapshot> submissionSnapshots,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        // The upsert on the Google id: an already known course is updated in place and keeps its surrogate
        // identity, so everything referring to it still does (spec FR-008).
        var course = await courses.GetByGoogleIdAsync(snapshot.GoogleId, cancellationToken);

        // US-015 spec FR-011, §5 v55: the age rule applies ONLY to a course the database does not hold. One
        // already imported is updated for ever, until the purge deletes it (I-4) — otherwise a course kept alive
        // by recent Meet sessions would have its roster go stale.
        if (course is null && IsOlderThanRetention(snapshot, items, submissionSnapshots, observedAt))
        {
            return CourseResult.AgedOut;
        }

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

        // The course, its people and its memberships are persisted first so their generated identities exist for
        // the rows that reference them; everything still commits inside the one transaction of OD-003.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var storedItems = await ImportCourseWorkAsync(course.Id, items, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var unrecognised = await ImportSubmissionsAsync(
            course,
            storedItems,
            submissionSnapshots,
            stored,
            observedAt,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CourseResult(false, marked, unrecognised);
    }

    /// <summary>
    /// US-015 spec FR-011: a course the database does not hold is judged by Google's data alone — its own update
    /// time, the creation or update of any of its items, and the update of any of its submissions (§5 v36, v55).
    /// </summary>
    /// <remarks>
    /// "Older than N" is strict, so a course whose last activity falls exactly on the boundary is imported (I-3).
    /// A course for which Google carries <b>no</b> date at all is imported too: refusing on absent evidence is the
    /// failure mode OD-001 rejected, and it would hide a live course for ever. The rule itself is
    /// <see cref="RetentionRule"/>, shared with the retention purge (US-037 spec FR-002).
    /// </remarks>
    private bool IsOlderThanRetention(
        CourseSnapshot snapshot,
        CourseWorkPage items,
        IReadOnlyList<SubmissionSnapshot> submissionSnapshots,
        DateTimeOffset observedAt)
    {
        var lastActivity = RetentionRule.LatestActivity(
            items.Items
                .SelectMany(item => new[] { item.Details.CreationTime, item.Details.UpdateTime })
                .Concat(submissionSnapshots.Select(submission => submission.UpdateTime))
                .Prepend(snapshot.Details.UpdateTime));

        return lastActivity is { } activity
               && RetentionRule.IsExpired(activity, RetentionRule.Cutoff(observedAt, retention.Years));
    }

    /// <summary>
    /// US-015 spec FR-003, FR-005, FR-010: the course's items of both Classroom resources, upserted on the
    /// natural key Specification v2 fixes — (course, resource, Google id). Returns them by that key, so the
    /// submissions can be attributed without a second query.
    /// </summary>
    private async Task<Dictionary<(CourseWorkResource Resource, string GoogleId), CourseWork>> ImportCourseWorkAsync(
        long courseId,
        CourseWorkPage items,
        CancellationToken cancellationToken)
    {
        var existing = await courseWork.GetByCourseAsync(courseId, cancellationToken);
        var byKey = existing.ToDictionary(i => (i.Resource, i.GoogleId));

        foreach (var snapshot in items.Items)
        {
            var key = (snapshot.Resource, snapshot.GoogleId);
            if (byKey.TryGetValue(key, out var item))
            {
                // The same row is updated, so an item that gained or lost maximum points keeps its identity and
                // simply changes the kind it derives (spec FR-005, FR-010, PC-3).
                item.UpdateFrom(snapshot.Details);
            }
            else
            {
                item = CourseWork.Import(courseId, snapshot.GoogleId, snapshot.Resource, snapshot.Details);
                courseWork.Add(item);
                byKey[key] = item;
            }
        }

        return byKey;
    }

    /// <summary>
    /// US-015 spec FR-004, FR-006, FR-007: every submission of the course, attributed by its own
    /// <c>courseWorkId</c> and by the Google <c>userId</c> of the person who made it (I-7). Returns the ones whose
    /// state Classroom reported outside the vocabulary, for the host's one Warning line each (OD-005).
    /// </summary>
    private async Task<List<UnrecognisedSubmission>> ImportSubmissionsAsync(
        Course course,
        Dictionary<(CourseWorkResource Resource, string GoogleId), CourseWork> storedItems,
        IReadOnlyList<SubmissionSnapshot> submissionSnapshots,
        Dictionary<string, ClassroomParticipant> rosterPeople,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        var unrecognised = new List<UnrecognisedSubmission>();
        if (submissionSnapshots.Count == 0)
        {
            return unrecognised;
        }

        var itemIds = storedItems.Values.Select(i => i.Id).ToList();
        var existing = await submissions.GetByCourseWorkIdsAsync(itemIds, cancellationToken);
        var byKey = existing.ToDictionary(s => (s.CourseWorkId, s.GoogleId));
        var people = new Dictionary<string, ClassroomParticipant>(rosterPeople, StringComparer.Ordinal);

        foreach (var snapshot in submissionSnapshots)
        {
            // A submission whose item is not among the course's own is attributed to nothing and is skipped: the
            // alternative would be inventing a parent row no Classroom resource reported.
            if (!storedItems.TryGetValue((CourseWorkResource.CourseWork, snapshot.CourseWorkGoogleId), out var item))
            {
                continue;
            }

            var participant = await ResolveSubmitterAsync(course, people, snapshot.GoogleUserId, observedAt, cancellationToken);

            if (!TryParseSubmissionState(snapshot.State, out var state))
            {
                state = SubmissionState.Unrecognised;
                unrecognised.Add(new UnrecognisedSubmission(snapshot.GoogleId, snapshot.State));
            }

            var details = new SubmissionDetails(
                state,
                state == SubmissionState.Unrecognised ? snapshot.State : null,
                snapshot.AssignedGrade,
                snapshot.DraftGrade,
                snapshot.TurnedInAt,
                snapshot.Late,
                snapshot.UpdateTime);

            if (byKey.TryGetValue((item.Id, snapshot.GoogleId), out var submission))
            {
                // A grade changed in Google replaces the stored one; the previous value is kept nowhere (BR-059).
                submission.UpdateFrom(details);
            }
            else
            {
                submission = Submission.Import(item.Id, participant.Id, snapshot.GoogleId, details);
                submissions.Add(submission);
                byKey[(item.Id, snapshot.GoogleId)] = submission;
            }
        }

        return unrecognised;
    }

    /// <summary>
    /// US-015 spec FR-007, BR-051 v56: the person who made a submission. One never seen on this course's roster —
    /// they left before the first synchronization — gets a <c>student</c> membership marked <b>off</b> the roster,
    /// first and last seen at this run's instant, so the leaver expiry of PC-11 has a date to count from.
    /// </summary>
    /// <remarks>
    /// The participant is created from the Google <c>userId</c> alone, with no name and no email (OD-006): a
    /// submission carries neither, and asking Google for a profile would rely on a Workspace role §7 item 10
    /// records as unverified.
    /// </remarks>
    private async Task<ClassroomParticipant> ResolveSubmitterAsync(
        Course course,
        Dictionary<string, ClassroomParticipant> people,
        string googleUserId,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        if (people.TryGetValue(googleUserId, out var known))
        {
            return known;
        }

        var stored = await participants.GetByGoogleUserIdsAsync([googleUserId], cancellationToken);
        var participant = stored.FirstOrDefault();
        if (participant is null)
        {
            participant = ClassroomParticipant.Import(googleUserId, null, null);
            participants.Add(participant);
        }

        people[googleUserId] = participant;

        var existing = await memberships.GetByCourseAsync(course.Id, cancellationToken);
        if (existing.All(m => m.ParticipantId != participant.Id))
        {
            var membership = CourseMembership.FirstSeen(course, participant, ClassroomRole.Student, observedAt);
            membership.NotOnRoster();
            memberships.Add(membership);
        }

        return participant;
    }

    /// <summary>
    /// US-015 spec VR-004, OD-011: the six values the vocabulary holds — the five the Classroom API documents and
    /// the one BR-056 names. Anything else is stored with OD-005's marker rather than skipped, because a missing
    /// submission would read as «не сдано» and state something false about a student's work.
    /// </summary>
    private static bool TryParseSubmissionState(string reported, out SubmissionState state)
    {
        switch (reported?.Trim().ToUpperInvariant())
        {
            case "NEW": state = SubmissionState.New; return true;
            case "CREATED": state = SubmissionState.Created; return true;
            case "TURNED_IN": state = SubmissionState.TurnedIn; return true;
            case "RETURNED": state = SubmissionState.Returned; return true;
            case "RECLAIMED_BY_STUDENT": state = SubmissionState.ReclaimedByStudent; return true;
            case "STUDENT_EDITED_AFTER_TURN_IN": state = SubmissionState.StudentEditedAfterTurnIn; return true;
            default: state = SubmissionState.Unrecognised; return false;
        }
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
                participant.UpdateFrom(entry.Email, entry.FullName, entry.Surname, entry.GivenName);
            }
            else
            {
                participant = ClassroomParticipant.Import(
                    entry.GoogleUserId, entry.Email, entry.FullName, entry.Surname, entry.GivenName);
                participants.Add(participant);
                byGoogleUserId[participant.GoogleUserId] = participant;
            }

            resolved[entry.GoogleUserId] = participant;
        }

        return resolved;
    }

    /// <summary>
    /// What the import did so far, for the outcome the host logs (spec FR-013, FR-016). A run that stops part-way
    /// still reports the courses it committed and the ones it skipped (US-017 spec I-5).
    /// </summary>
    private sealed class ImportProgress
    {
        public int ProcessedCount { get; set; }

        public int MembershipsMarkedOffRoster { get; set; }

        public List<SkippedCourse> SkippedCourses { get; } = [];

        public List<string> CoursesSkippedByAge { get; } = [];

        public List<UnrecognisedSubmission> UnrecognisedSubmissions { get; } = [];

        public List<string> CoursesGone { get; } = [];

        public List<string> CoursesWithBlankName { get; } = [];
    }

    /// <summary>What one course's transaction did (US-015 spec FR-011, FR-016, OD-005).</summary>
    private readonly record struct CourseResult(
        bool SkippedByAge,
        int MembershipsMarkedOffRoster,
        IReadOnlyList<UnrecognisedSubmission> UnrecognisedSubmissions)
    {
        /// <summary>Nothing was written for the course, so it is not counted (spec FR-011, I-5).</summary>
        public static CourseResult AgedOut => new(true, 0, []);
    }
}
