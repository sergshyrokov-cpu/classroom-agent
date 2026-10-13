using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the Database page and its home-page section (US-024 spec FR-012, test strategy §5). Every
/// key exists in both the Ukrainian and the English file; the five course-state names are the course list's and the
/// button and messages are US-019's.
/// </summary>
public static class DatabaseTextKeys
{
    public const string Title = "Database.Title";

    public const string Section = "Database.Section";

    public const string NavigationEntry = "Database.NavigationEntry";

    public const string CounterTotal = "Database.Counter.Total";

    public const string CounterTeachers = "Database.Counter.Teachers";

    public const string CounterStudents = "Database.Counter.Students";

    public const string CounterCourseWorkItems = "Database.Counter.CourseWorkItems";

    public const string CounterSubmissions = "Database.Counter.Submissions";

    public const string CounterMeetMeetings = "Database.Counter.MeetMeetings";

    public const string CounterMeetCodesAwaitingDecision = "Database.Counter.MeetCodesAwaitingDecision";

    public const string SyncLastSuccessfulRun = "Database.Sync.LastSuccessfulRun";

    public const string SyncState = "Database.Sync.State";

    public const string SyncLastFullRun = "Database.Sync.LastFullRun";

    public const string SyncMeetLoadedUpTo = "Database.Sync.MeetLoadedUpTo";

    public const string NotYet = "Database.Sync.NotYet";

    public const string FailureAskAdmin = "Database.Failure.AskAdmin";

    public const string FailureConnectionPageLink = "Database.Failure.ConnectionPageLink";

    /// <summary>The state line's text; no row reads "not yet" (spec FR-005, I-2).</summary>
    public static string State(SynchronizationDisplayState state) => state switch
    {
        SynchronizationDisplayState.NotYet => NotYet,
        SynchronizationDisplayState.InProgress => "Database.Sync.InProgress",
        SynchronizationDisplayState.Completed => "Database.Sync.Completed",
        SynchronizationDisplayState.Failed => "Database.Sync.Failed",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };
}
