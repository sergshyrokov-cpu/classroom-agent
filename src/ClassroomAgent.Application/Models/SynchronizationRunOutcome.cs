namespace ClassroomAgent.Application.Models;

/// <summary>
/// What a synchronization run did (US-013 spec FR-005, FR-014): ran with a run identifier, a processed count and
/// an optional error, or was skipped with its reason — read-only mode with the BR-025 cause, or an unusable
/// connection with its state.
/// </summary>
/// <remarks>
/// US-014 adds what the host must log about the import but the Application layer cannot log itself: the courses a
/// run skipped because of an unrecognised state (spec FR-003, OD-010) and how many memberships it marked off a
/// roster (spec FR-016). Both are counters and identifiers only, never personal data (SC-10).
/// </remarks>
public sealed class SynchronizationRunOutcome
{
    private SynchronizationRunOutcome(
        Guid? runId,
        int? processedCount,
        string? error,
        LegitimacyModeReason? readOnlyReason,
        WorkspaceConnectionState? connectionState,
        IReadOnlyList<SkippedCourse>? skippedCourses = null,
        int membershipsMarkedOffRoster = 0,
        IReadOnlyList<string>? coursesSkippedByAge = null,
        IReadOnlyList<UnrecognisedSubmission>? unrecognisedSubmissions = null)
    {
        RunId = runId;
        ProcessedCount = processedCount;
        Error = error;
        ReadOnlyReason = readOnlyReason;
        ConnectionState = connectionState;
        SkippedCourses = skippedCourses ?? [];
        MembershipsMarkedOffRoster = membershipsMarkedOffRoster;
        CoursesSkippedByAge = coursesSkippedByAge ?? [];
        UnrecognisedSubmissions = unrecognisedSubmissions ?? [];
    }

    public Guid? RunId { get; }

    public int? ProcessedCount { get; }

    public string? Error { get; }

    public LegitimacyModeReason? ReadOnlyReason { get; }

    public WorkspaceConnectionState? ConnectionState { get; }

    /// <summary>US-014 spec FR-003, OD-010: the courses an unrecognised state made unimportable, for one Warning line each.</summary>
    public IReadOnlyList<SkippedCourse> SkippedCourses { get; }

    /// <summary>US-014 spec FR-010, FR-016: how many memberships this run marked as no longer on a roster.</summary>
    public int MembershipsMarkedOffRoster { get; }

    /// <summary>
    /// US-015 spec FR-011, I-5: the Google ids of courses the §5 age rule left unimported. <c>SyncState</c> has
    /// one counter and cannot carry them, so the log line the host writes from this list is what makes the skip
    /// discoverable at all.
    /// </summary>
    public IReadOnlyList<string> CoursesSkippedByAge { get; }

    /// <summary>US-015 spec VR-004, OD-005: the submissions stored with the unrecognised marker, one Warning line each.</summary>
    public IReadOnlyList<UnrecognisedSubmission> UnrecognisedSubmissions { get; }

    public bool Failed => Error is not null;

    public static SynchronizationRunOutcome Ran(
        Guid runId,
        int processedCount,
        string? error,
        IReadOnlyList<SkippedCourse>? skippedCourses = null,
        int membershipsMarkedOffRoster = 0,
        IReadOnlyList<string>? coursesSkippedByAge = null,
        IReadOnlyList<UnrecognisedSubmission>? unrecognisedSubmissions = null) =>
        new(
            runId,
            processedCount,
            error,
            null,
            null,
            skippedCourses,
            membershipsMarkedOffRoster,
            coursesSkippedByAge,
            unrecognisedSubmissions);

    public static SynchronizationRunOutcome SkippedReadOnly(LegitimacyModeReason reason) =>
        new(null, null, null, reason, null);

    public static SynchronizationRunOutcome SkippedConnection(WorkspaceConnectionState state) =>
        new(null, null, null, null, state);
}
