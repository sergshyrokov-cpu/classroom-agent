using System.Net;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-032 (spec FR-007 … FR-017, api-design §2): paths, form fields, translation keys and shared SQL seeding for the
/// "Meet meetings" page tests. Rows are inserted directly (TC-2) so the tests do not depend on the production code
/// under test; every name, email and code is invented (TC-4).
/// </summary>
public static partial class MeetCodesHostExtensions
{
    public const string Path = "/workspace/meet-codes";

    public static string Url(string? list = null, string? page = null, string? size = null)
    {
        var parts = new List<string>();
        if (list is not null)
        {
            parts.Add("list=" + Uri.EscapeDataString(list));
        }

        if (page is not null)
        {
            parts.Add("page=" + Uri.EscapeDataString(page));
        }

        if (size is not null)
        {
            parts.Add("size=" + Uri.EscapeDataString(size));
        }

        return parts.Count == 0 ? Path : Path + "?" + string.Join('&', parts);
    }

    public static string ChoicePath(string code, int? returnPage = null) =>
        $"{Path}/{Uri.EscapeDataString(code)}/course-choice" + (returnPage is null ? string.Empty : $"?returnPage={returnPage}");

    public static string LinkPath(string code) => $"{Path}/{Uri.EscapeDataString(code)}/link";

    public static string ConfirmationPath(string code) => $"{Path}/{Uri.EscapeDataString(code)}/confirmation";

    public static string MarkPath(string code) => $"{Path}/{Uri.EscapeDataString(code)}/not-a-course-mark";

    public static class Fields
    {
        public const string CourseId = "courseId";

        public const string ExpectedState = "expectedState";

        public const string ExpectedCourseId = "expectedCourseId";

        public const string ReturnPage = "returnPage";
    }

    public static class States
    {
        public const string Unassigned = "unassigned";

        public const string Linked = "linked";

        public const string Marked = "marked";
    }

    public static class Lists
    {
        public const string Unassigned = "unassigned";

        public const string Linked = "linked";

        public const string NotACourse = "not-a-course";
    }

    public static class Actions
    {
        public const string Picked = "meet_code_course_picked";

        public const string Confirmed = "meet_code_link_confirmed";

        public const string Relinked = "meet_code_relinked";

        public const string Marked = "meet_code_marked_not_a_course";

        public const string MarkRemoved = "meet_code_mark_removed";
    }

    /// <summary>The translation keys of api-design §5 and spec FR-017.</summary>
    public static class Keys
    {
        public const string Title = "MeetCodes.Title";

        public const string NavigationEntry = "MeetCodes.NavigationEntry";

        public const string NoCandidates = "MeetCodes.NoCandidates";

        public const string Automatically = "MeetCodes.Automatically";

        public const string DeletedAccount = "MeetCodes.DeletedAccount";

        public const string MessagePicked = "MeetCodes.Message.CoursePicked";

        public const string MessageRelinked = "MeetCodes.Message.Relinked";

        public const string MessageMarkRemoved = "MeetCodes.Message.MarkRemoved";

        public const string MessageConfirmed = "MeetCodes.Message.Confirmed";

        public const string MessageMarked = "MeetCodes.Message.Marked";

        public const string MessageCodeNotFound = "MeetCodes.Message.CodeNotFound";

        public const string MessageStateChanged = "MeetCodes.Message.StateChanged";

        public const string MessageQueryInvalid = "MeetCodes.Message.QueryInvalid";

        public const string FieldSameCourse = "MeetCodes.FieldError.SameCourse";

        public const string FieldCourseNotFound = "MeetCodes.FieldError.CourseNotFound";

        public const string StepLinking = "LastSync.Step.Linking";

        public static readonly string[] All =
        [
            NavigationEntry,
            Title,
            "MeetCodes.List.Unassigned",
            "MeetCodes.List.Linked",
            "MeetCodes.List.NotACourse",
            "MeetCodes.Empty.Unassigned",
            "MeetCodes.Empty.Linked",
            "MeetCodes.Empty.NotACourse",
            NoCandidates,
            Automatically,
            DeletedAccount,
            "MeetCodes.NotConfirmed",
            "MeetCodes.Action.PickCourse",
            "MeetCodes.Action.Confirm",
            "MeetCodes.Action.Relink",
            "MeetCodes.Action.MarkNotACourse",
            "MeetCodes.Choice.Title",
            "MeetCodes.Choice.Candidates",
            "MeetCodes.Choice.OtherCourses",
            "MeetCodes.Choice.Submit",
            MessagePicked,
            MessageRelinked,
            MessageMarkRemoved,
            MessageConfirmed,
            MessageMarked,
            MessageCodeNotFound,
            MessageStateChanged,
            MessageQueryInvalid,
            FieldSameCourse,
            FieldCourseNotFound,
            StepLinking,
        ];
    }

    /// <summary>The translation key of the read-only reason of a cause (existing ReadOnly.Refused.* texts).</summary>
    public static string ReasonKeyOf(ReadOnlyModeHost.Cause cause) => cause switch
    {
        ReadOnlyModeHost.Cause.NeverConfirmed => SignInTestData.TextKeys.RefusedNotYetConfirmed,
        ReadOnlyModeHost.Cause.Suspended => SignInTestData.TextKeys.RefusedSuspendedByOwner,
        ReadOnlyModeHost.Cause.GracePeriodExpired => SignInTestData.TextKeys.RefusedGracePeriodExpired,
        _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, null),
    };

    public static string Email(string local) => $"{local}@{InstallationTestData.Domain}";

    /// <summary>The email of the account an actor signs in as.</summary>
    public static string EmailOf(JournalHostExtensions.Actor actor) => actor == JournalHostExtensions.Actor.Admin
        ? SignInTestData.AdminEmail
        : DeanAccountTestData.DeanEmail;

    /// <summary>Waits for the purge that runs at host start, so it cannot race the seeding below (US-037).</summary>
    public static Task PrepareAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.WaitForPurgeRunsAsync(1, cancellationToken);

    /// <summary>Sends a form after fetching a page for the antiforgery token (landing page, or sign-in when anonymous).</summary>
    public static async Task<PageResponse> PostWithTokenAsync(
        this FormClient client,
        string path,
        IEnumerable<KeyValuePair<string, string>> fields,
        CancellationToken cancellationToken,
        bool withToken = true,
        string tokenPage = SignInTestData.LandingPath)
    {
        if (withToken)
        {
            await client.GetAsync(tokenPage, cancellationToken);
        }

        return await client.PostFormAsync(path, fields, cancellationToken, withToken);
    }

    public static KeyValuePair<string, string> Field(string name, object value) =>
        new(name, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!);

    /// <summary>A course with its roster; the memberships were first seen at <paramref name="firstSeenAt"/>.</summary>
    public static async Task<long> SeedRosterCourseAsync(
        this InstallationTestHost host,
        string name,
        IEnumerable<string> teacherEmails,
        IEnumerable<string> studentEmails,
        CancellationToken cancellationToken,
        DateTimeOffset? firstSeenAt = null,
        string? section = null)
    {
        var seen = firstSeenAt ?? new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var courseId = await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: "6" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999), name: name, section: section);
        foreach (var email in teacherEmails)
        {
            var participant = await ParticipantAsync(host, email, cancellationToken);
            await CourseRows.InsertMembershipAsync(
                host, courseId, participant, cancellationToken, role: CourseTestData.Roles.Teacher, firstSeenAt: seen);
        }

        foreach (var email in studentEmails)
        {
            var participant = await ParticipantAsync(host, email, cancellationToken);
            await CourseRows.InsertMembershipAsync(host, courseId, participant, cancellationToken, firstSeenAt: seen);
        }

        return courseId;
    }

    private static async Task<long> ParticipantAsync(InstallationTestHost host, string email, CancellationToken cancellationToken)
    {
        var existing = await host.ScalarAsync<long?>(
            "SELECT id FROM classroom_participant WHERE lower(email) = @e", cancellationToken, ("e", email.ToLowerInvariant()));
        return existing ?? await CourseRows.InsertParticipantAsync(
            host,
            cancellationToken,
            googleUserId: "8" + Random.Shared.NextInt64(10_000_000_000_000_000, 99_999_999_999_999_999),
            email: email,
            fullName: "Test Person " + email.Split('@')[0]);
    }

    /// <summary>A stored meeting with domain-account participants and "other participant" connections.</summary>
    public static async Task<long> SeedMeetingAsync(
        this InstallationTestHost host,
        string code,
        string organizer,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken,
        IEnumerable<string>? participants = null,
        int otherParticipants = 0)
    {
        var session = await host.ScalarAsync<long>(
            """
            INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
            VALUES (@conferenceId, @code, @organizer, @startedAt, @endedAt, @startedAt, @startedAt)
            RETURNING id
            """,
            cancellationToken,
            ("conferenceId", "conf-" + Guid.NewGuid().ToString("N")),
            ("code", code),
            ("organizer", organizer),
            ("startedAt", startedAt),
            ("endedAt", startedAt + TimeSpan.FromHours(1)));
        var endpoint = 0;
        foreach (var email in participants ?? [])
        {
            await InsertConnectionAsync(host, session, ++endpoint, email, startedAt, cancellationToken);
        }

        for (var i = 0; i < otherParticipants; i++)
        {
            await InsertConnectionAsync(host, session, ++endpoint, null, startedAt, cancellationToken);
        }

        return session;
    }

    private static Task InsertConnectionAsync(
        InstallationTestHost host, long session, int endpoint, string? email, DateTimeOffset at, CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            """
            INSERT INTO meet_participation (meet_session_id, endpoint_id, email, joined_at, duration_seconds, created_at, updated_at)
            VALUES (@session, @endpoint, @email, @at, 600, @at, @at)
            """,
            cancellationToken,
            ("session", session),
            ("endpoint", "ep-" + endpoint),
            ("email", email),
            ("at", at));

    /// <summary>An automatic link (spec FR-006), optionally confirmed.</summary>
    public static Task<int> SeedAutoLinkAsync(
        this InstallationTestHost host,
        string code,
        long courseId,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken,
        long? confirmedBy = null,
        DateTimeOffset? confirmedAt = null) =>
        SeedLinkRowAsync(host, code, courseId, true, null, linkedAt, confirmedBy, confirmedAt, null, null, cancellationToken);

    /// <summary>A link made by a person; <paramref name="linkedBy"/> may name no <c>app_user</c> row (a deleted account).</summary>
    public static Task<int> SeedPersonLinkAsync(
        this InstallationTestHost host, string code, long courseId, long linkedBy, DateTimeOffset linkedAt, CancellationToken cancellationToken) =>
        SeedLinkRowAsync(host, code, courseId, false, linkedBy, linkedAt, null, null, null, null, cancellationToken);

    /// <summary>A "not a course" mark (course_id null).</summary>
    public static Task<int> SeedMarkAsync(
        this InstallationTestHost host, string code, long markedBy, DateTimeOffset markedAt, CancellationToken cancellationToken) =>
        SeedLinkRowAsync(host, code, null, null, null, null, null, null, markedBy, markedAt, cancellationToken);

    private static Task<int> SeedLinkRowAsync(
        InstallationTestHost host,
        string code,
        long? courseId,
        bool? automatically,
        long? linkedBy,
        DateTimeOffset? linkedAt,
        long? confirmedBy,
        DateTimeOffset? confirmedAt,
        long? markedBy,
        DateTimeOffset? markedAt,
        CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            """
            INSERT INTO meeting_code_link (meeting_code, course_id, linked_automatically, linked_by_app_user_id, linked_at,
                                           confirmed_by_app_user_id, confirmed_at, marked_by_app_user_id, marked_at,
                                           concurrency_stamp, created_at, updated_at)
            VALUES (@code, @courseId, @automatically, @linkedBy, @linkedAt, @confirmedBy, @confirmedAt, @markedBy, @markedAt,
                    @stamp, @created, @created)
            """,
            cancellationToken,
            ("code", code),
            ("courseId", courseId),
            ("automatically", automatically),
            ("linkedBy", linkedBy),
            ("linkedAt", linkedAt),
            ("confirmedBy", confirmedBy),
            ("confirmedAt", confirmedAt),
            ("markedBy", markedBy),
            ("markedAt", markedAt),
            ("stamp", Guid.NewGuid().ToString("N")),
            ("created", linkedAt ?? markedAt ?? host.Time.GetUtcNow()));

    /// <summary>A stored <c>meeting_code_link</c> row.</summary>
    public sealed record LinkRow(
        string MeetingCode,
        long? CourseId,
        bool? LinkedAutomatically,
        long? LinkedBy,
        DateTimeOffset? LinkedAt,
        long? ConfirmedBy,
        DateTimeOffset? ConfirmedAt,
        long? MarkedBy,
        DateTimeOffset? MarkedAt);

    public static Task<IReadOnlyList<LinkRow>> McLinkRowsAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync(
            """
            SELECT meeting_code, course_id, linked_automatically, linked_by_app_user_id, linked_at,
                   confirmed_by_app_user_id, confirmed_at, marked_by_app_user_id, marked_at
            FROM meeting_code_link ORDER BY id
            """,
            r => new LinkRow(
                r.GetString(0),
                r.IsDBNull(1) ? null : r.GetInt64(1),
                r.IsDBNull(2) ? null : r.GetBoolean(2),
                r.IsDBNull(3) ? null : r.GetInt64(3),
                r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4),
                r.IsDBNull(5) ? null : r.GetInt64(5),
                r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6),
                r.IsDBNull(7) ? null : r.GetInt64(7),
                r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8)),
            cancellationToken);

    /// <summary>Every column of every link row as text: any insert, update or delete changes it.</summary>
    public static async Task<string> McLinksFingerprintAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        await host.ScalarAsync<string>(
            "SELECT coalesce(string_agg(t::text, '|' ORDER BY id), '') FROM meeting_code_link t", cancellationToken) ?? string.Empty;

    /// <summary>A stored US-032 audit row.</summary>
    public sealed record MeetAuditRow(
        string ActorType,
        long? ActorId,
        string? ActorRole,
        string Action,
        string? TargetType,
        long? TargetId,
        string Outcome,
        string? RefusalCategory,
        string? MeetCode,
        long? PreviousCourseId);

    public static Task<IReadOnlyList<MeetAuditRow>> McAuditRowsAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync(
            """
            SELECT actor_type, actor_id, actor_role, action, target_type, target_id, outcome, refusal_category,
                   meet_code, meet_previous_course_id
            FROM audit_event WHERE action LIKE 'meet\_code\_%' ORDER BY id
            """,
            r => new MeetAuditRow(
                r.GetString(0),
                r.IsDBNull(1) ? null : r.GetInt64(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetInt64(5),
                r.GetString(6),
                r.IsDBNull(7) ? null : r.GetString(7),
                r.IsDBNull(8) ? null : r.GetString(8),
                r.IsDBNull(9) ? null : r.GetInt64(9)),
            cancellationToken);

    /// <summary>The page text with markup removed, for assertions on what a person reads.</summary>
    public static string VisibleText(string html)
    {
        var withoutBlocks = Blocks().Replace(html, " ");
        var withoutTags = Tags().Replace(withoutBlocks, " ");
        return Spaces().Replace(WebUtility.HtmlDecode(withoutTags), " ").Trim();
    }

    /// <summary>The distinct values of the course controls of a choice form (options and <c>courseId</c> inputs).</summary>
    public static IReadOnlyList<long> OfferedCourseIds(string html)
    {
        var ids = new List<long>();
        foreach (Match tag in ControlTag().Matches(html))
        {
            var isOption = tag.Value.StartsWith("<option", StringComparison.OrdinalIgnoreCase);
            var name = Regex.Match(tag.Value, "\\sname\\s*=\\s*\"(?<n>[^\"]*)\"", RegexOptions.IgnoreCase).Groups["n"].Value;
            var value = Regex.Match(tag.Value, "\\svalue\\s*=\\s*\"(?<v>[0-9]+)\"", RegexOptions.IgnoreCase);
            if (value.Success && (isOption || string.Equals(name, Fields.CourseId, StringComparison.OrdinalIgnoreCase)))
            {
                ids.Add(long.Parse(value.Groups["v"].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return ids.Distinct().ToList();
    }

    /// <summary>Forms whose action ends with <c>/confirmation</c>.</summary>
    public static IReadOnlyList<string> ConfirmationActions(string html) =>
        ConfirmationForm().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups["a"].Value)).ToList();

    [GeneratedRegex(@"<(script|style)\b[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex Blocks();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"<(option|input|button)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ControlTag();

    [GeneratedRegex("action\\s*=\\s*\"(?<a>[^\"]*/confirmation)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ConfirmationForm();
}
