using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-024 fixed names: the two paths and the policy of docs/designs/api/US-024-openapi.yaml, and the markup hooks and
/// translation keys of docs/tests/US-024-test-strategy.md §4–§5, which IMPLEMENTATION renders exactly.
/// </summary>
public static partial class DatabaseStatisticsTestData
{
    /// <summary>openapi <c>GET /database</c>.</summary>
    public const string Path = "/database";

    /// <summary>openapi <c>POST /database/synchronization-requests</c>.</summary>
    public const string PressPath = "/database/synchronization-requests";

    /// <summary>The new policy of spec FR-010 (api-design §2.5).</summary>
    public const string Policy = "ViewDatabaseStatistics";

    /// <summary>The route patterns as the endpoint data source lists them.</summary>
    public const string Pattern = "database";

    public const string PressPattern = "database/synchronization-requests";

    /// <summary>Markup hooks (strategy §4).</summary>
    public static class Markup
    {
        /// <summary>The home page section: <c>&lt;nav id="database-section"&gt;</c> with an h2 and one link to <see cref="Path"/>.</summary>
        public const string Section = "database-section";

        /// <summary>The element whose only text is a counter's number carries <c>data-counter="&lt;name&gt;"</c>.</summary>
        public const string CounterAttribute = "data-counter";

        /// <summary>The element whose only text is a synchronization line's value carries <c>data-sync-line="&lt;name&gt;"</c>.</summary>
        public const string SyncLineAttribute = "data-sync-line";

        /// <summary>The Dean's hint for a failed run.</summary>
        public const string FailureHint = "database-failure-hint";

        /// <summary>The Admin's link to the connection page for a failed run.</summary>
        public const string ConnectionLink = "database-connection-link";

        /// <summary>The form posting to <see cref="PressPath"/>.</summary>
        public const string Form = "database-synchronization-form";

        /// <summary>The one-time US-019 message (the US-019 id, reused).</summary>
        public const string Message = SynchronizationRequestTestData.Markup.MessageId;
    }

    /// <summary>The values of <see cref="Markup.CounterAttribute"/>.</summary>
    public static class Counters
    {
        public const string Active = "courses-active";
        public const string Archived = "courses-archived";
        public const string Provisioned = "courses-provisioned";
        public const string Declined = "courses-declined";
        public const string Suspended = "courses-suspended";
        public const string Total = "courses-total";
        public const string Teachers = "teachers";
        public const string Students = "students";
        public const string CourseWorkItems = "course-work-items";
        public const string Submissions = "submissions";
        public const string MeetMeetings = "meet-meetings";
        public const string MeetCodesAwaitingDecision = "meet-codes-awaiting-decision";

        public static IReadOnlyList<string> All { get; } =
        [
            Active, Archived, Provisioned, Declined, Suspended, Total, Teachers, Students, CourseWorkItems, Submissions,
            MeetMeetings, MeetCodesAwaitingDecision,
        ];
    }

    /// <summary>The values of <see cref="Markup.SyncLineAttribute"/>.</summary>
    public static class SyncLines
    {
        public const string LastSuccessfulRun = "last-successful-run";
        public const string State = "state";
        public const string LastFullRun = "last-full-run";
        public const string MeetLoadedUpTo = "meet-loaded-up-to";

        public static IReadOnlyList<string> All { get; } = [LastSuccessfulRun, State, LastFullRun, MeetLoadedUpTo];
    }

    /// <summary>Translation keys (strategy §5).</summary>
    public static class Keys
    {
        public const string Title = "Database.Title";
        public const string Section = "Database.Section";
        public const string NavigationEntry = "Database.NavigationEntry";
        public const string Total = "Database.Counter.Total";
        public const string Teachers = "Database.Counter.Teachers";
        public const string Students = "Database.Counter.Students";
        public const string CourseWorkItems = "Database.Counter.CourseWorkItems";
        public const string Submissions = "Database.Counter.Submissions";
        public const string MeetMeetings = "Database.Counter.MeetMeetings";
        public const string MeetCodesAwaitingDecision = "Database.Counter.MeetCodesAwaitingDecision";
        public const string LastSuccessfulRun = "Database.Sync.LastSuccessfulRun";
        public const string State = "Database.Sync.State";
        public const string LastFullRun = "Database.Sync.LastFullRun";
        public const string MeetLoadedUpTo = "Database.Sync.MeetLoadedUpTo";
        public const string InProgress = "Database.Sync.InProgress";
        public const string Completed = "Database.Sync.Completed";
        public const string Failed = "Database.Sync.Failed";
        public const string NotYet = "Database.Sync.NotYet";
        public const string AskAdmin = "Database.Failure.AskAdmin";
        public const string ConnectionPageLink = "Database.Failure.ConnectionPageLink";

        public static IReadOnlyList<string> New { get; } =
        [
            Title, Section, NavigationEntry, Total, Teachers, Students, CourseWorkItems, Submissions, MeetMeetings,
            MeetCodesAwaitingDecision, LastSuccessfulRun, State, LastFullRun, MeetLoadedUpTo, InProgress, Completed,
            Failed, NotYet, AskAdmin, ConnectionPageLink,
        ];

        /// <summary>The five course-state names (US-020) and the US-019 button and messages, reused (spec FR-012).</summary>
        public static IReadOnlyList<string> Reused { get; } =
        [
            CourseListTestData.Keys.StatusActive, CourseListTestData.Keys.StatusArchived,
            CourseListTestData.Keys.StatusProvisioned, CourseListTestData.Keys.StatusDeclined,
            CourseListTestData.Keys.StatusSuspended, .. SynchronizationRequestTestData.TextKeys.All,
        ];
    }

    /// <summary>The number shown by the counter <paramref name="name"/>; null when the page has no such counter.</summary>
    public static int? Counter(string html, string name)
    {
        var match = Regex.Match(
            html,
            @"<[a-z0-9]+[^>]*\b" + Markup.CounterAttribute + @"=""" + Regex.Escape(name) + @"""[^>]*>\s*(?<n>[0-9]+)\s*<",
            RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>The decoded, trimmed text of the synchronization line <paramref name="name"/>; null when there is none.</summary>
    public static string? SyncLine(string html, string name)
    {
        var match = Regex.Match(
            html,
            @"<[a-z0-9]+[^>]*\b" + Markup.SyncLineAttribute + @"=""" + Regex.Escape(name) + @"""[^>]*>(?<text>[^<]*)<",
            RegexOptions.IgnoreCase);
        return match.Success ? WhiteSpace().Replace(WebUtility.HtmlDecode(match.Groups["text"].Value), " ").Trim() : null;
    }

    /// <summary>
    /// A wall-clock time of the school's zone as the page shows it: the culture's short date and <c>HH:mm</c>
    /// (spec FR-005, api-design §2.7) — the format of the connection page's Meet watermark.
    /// </summary>
    public static string Format(DateTime local, string culture) => MeetTestData.FormatLocal(local, culture);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
