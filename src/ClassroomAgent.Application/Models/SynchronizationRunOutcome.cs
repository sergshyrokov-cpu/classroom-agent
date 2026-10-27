using ClassroomAgent.Domain.Enums;

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
/// <para>
/// US-017 adds what the host logs about a failure: the diagnosis a failed run stored (spec FR-006), the exception
/// <b>type</b> name of an unexpected one (spec FR-010 — never its message), and the courses skipped because they
/// are gone or have a blank name (spec FR-005, FR-012).
/// </para>
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
        IReadOnlyList<UnrecognisedSubmission>? unrecognisedSubmissions = null,
        SyncDiagnosis? diagnosis = null,
        string? unexpectedExceptionType = null,
        IReadOnlyList<string>? coursesGone = null,
        IReadOnlyList<string>? coursesWithBlankName = null,
        SyncStep? failedStep = null,
        MeetPullCounts? meet = null,
        MeetLinkingCounts? linking = null,
        int coursesSkippedUnchanged = 0,
        IReadOnlyList<string>? coursesValueOutOfRange = null,
        int courseWorkMarked = 0,
        int courseWorkUnmarked = 0,
        CourseListMarkCounts? courseList = null)
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
        Diagnosis = diagnosis;
        UnexpectedExceptionType = unexpectedExceptionType;
        CoursesGone = coursesGone ?? [];
        CoursesWithBlankName = coursesWithBlankName ?? [];
        FailedStep = failedStep;
        Meet = meet;
        Linking = linking;
        CoursesSkippedUnchanged = coursesSkippedUnchanged;
        CoursesValueOutOfRange = coursesValueOutOfRange ?? [];
        CourseWorkMarked = courseWorkMarked;
        CourseWorkUnmarked = courseWorkUnmarked;
        CourseList = courseList;
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

    /// <summary>US-017 spec FR-006: the diagnosis a failed run stored; null unless the run failed.</summary>
    public SyncDiagnosis? Diagnosis { get; }

    /// <summary>
    /// US-017 spec FR-010: the exception type name of an <see cref="SyncDiagnosis.Unexpected"/> failure, for the one
    /// Error line; null otherwise. A type name only — a message may carry remote detail (SC-10).
    /// </summary>
    public string? UnexpectedExceptionType { get; }

    /// <summary>US-017 spec FR-005: the Google ids of courses skipped because Classroom reported them gone.</summary>
    public IReadOnlyList<string> CoursesGone { get; }

    /// <summary>US-017 spec FR-012: the Google ids of courses skipped before their reads because their name is blank.</summary>
    public IReadOnlyList<string> CoursesWithBlankName { get; }

    /// <summary>
    /// US-031 spec FR-012: what the Meet step did, for the host's log lines — counts and instants only, never an event
    /// value (SC-10); null when the step did not run.
    /// </summary>
    public MeetPullCounts? Meet { get; }

    /// <summary>US-032 spec FR-006, §9: what the linking step did, for the host's log line; null when it did not complete.</summary>
    public MeetLinkingCounts? Linking { get; }

    /// <summary>US-031 spec FR-010: the step that stopped a failed run; null otherwise.</summary>
    public SyncStep? FailedStep { get; }

    /// <summary>
    /// US-018 spec FR-008, FR-009, I-2: how many archived courses this run skipped as unchanged, for the host's
    /// completion and failure lines — a count only (SC-10).
    /// </summary>
    public int CoursesSkippedUnchanged { get; }

    /// <summary>
    /// US-057 spec FR-002.2, FR-003: the Google ids of courses skipped because Classroom returned points beyond the range
    /// the program holds, reported whether the run completed or stopped — identifiers only, never a value (SC-10).
    /// </summary>
    public IReadOnlyList<string> CoursesValueOutOfRange { get; }

    /// <summary>
    /// US-058 FR-004: items newly marked "not in Classroom" in the courses this run committed, reported whether the run
    /// completed or stopped — a count only (SC-10).
    /// </summary>
    public int CourseWorkMarked { get; }

    /// <summary>US-058 FR-004: marked items Google returned again in the courses this run committed — a count only.</summary>
    public int CourseWorkUnmarked { get; }

    /// <summary>
    /// US-059 FR-007: the course marks of the end of the list, reported whether the run completed or stopped later; null
    /// when the run stopped before the list was read to its end.
    /// </summary>
    public CourseListMarkCounts? CourseList { get; }

    public bool Failed => Error is not null;

    public static SynchronizationRunOutcome Ran(
        Guid runId,
        int processedCount,
        string? error,
        IReadOnlyList<SkippedCourse>? skippedCourses = null,
        int membershipsMarkedOffRoster = 0,
        IReadOnlyList<string>? coursesSkippedByAge = null,
        IReadOnlyList<UnrecognisedSubmission>? unrecognisedSubmissions = null,
        IReadOnlyList<string>? coursesGone = null,
        IReadOnlyList<string>? coursesWithBlankName = null,
        MeetPullCounts? meet = null,
        MeetLinkingCounts? linking = null,
        int coursesSkippedUnchanged = 0,
        IReadOnlyList<string>? coursesValueOutOfRange = null,
        int courseWorkMarked = 0,
        int courseWorkUnmarked = 0,
        CourseListMarkCounts? courseList = null) =>
        new(
            runId,
            processedCount,
            error,
            null,
            null,
            skippedCourses,
            membershipsMarkedOffRoster,
            coursesSkippedByAge,
            unrecognisedSubmissions,
            null,
            null,
            coursesGone,
            coursesWithBlankName,
            null,
            meet,
            linking,
            coursesSkippedUnchanged,
            coursesValueOutOfRange,
            courseWorkMarked,
            courseWorkUnmarked,
            courseList);

    /// <summary>
    /// A run that stopped with a diagnosis (US-017 spec FR-005, FR-006): <see cref="Error"/> is the diagnosis name,
    /// <paramref name="processedCount"/> the courses committed before the stop (I-5).
    /// </summary>
    public static SynchronizationRunOutcome RanAndFailed(
        Guid runId,
        int processedCount,
        SyncDiagnosis diagnosis,
        string? unexpectedExceptionType,
        IReadOnlyList<SkippedCourse>? skippedCourses = null,
        IReadOnlyList<string>? coursesSkippedByAge = null,
        IReadOnlyList<string>? coursesGone = null,
        IReadOnlyList<string>? coursesWithBlankName = null,
        SyncStep? failedStep = null,
        MeetPullCounts? meet = null,
        int coursesSkippedUnchanged = 0,
        IReadOnlyList<string>? coursesValueOutOfRange = null,
        int courseWorkMarked = 0,
        int courseWorkUnmarked = 0,
        CourseListMarkCounts? courseList = null) =>
        new(
            runId,
            processedCount,
            diagnosis.ToString(),
            null,
            null,
            skippedCourses,
            0,
            coursesSkippedByAge,
            null,
            diagnosis,
            unexpectedExceptionType,
            coursesGone,
            coursesWithBlankName,
            failedStep,
            meet,
            null,
            coursesSkippedUnchanged,
            coursesValueOutOfRange,
            courseWorkMarked,
            courseWorkUnmarked,
            courseList);

    public static SynchronizationRunOutcome SkippedReadOnly(LegitimacyModeReason reason) =>
        new(null, null, null, reason, null);

    public static SynchronizationRunOutcome SkippedConnection(WorkspaceConnectionState state) =>
        new(null, null, null, null, state);
}
