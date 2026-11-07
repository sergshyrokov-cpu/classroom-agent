using System.Net;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-062 over PostgreSQL and HTTP (TC-2): the stored facts the problems are computed from — courses, marks,
/// grades, items, skip records, <c>sync_state</c>, <c>legitimacy_state</c> — written straight into their columns, the way
/// US-060's <see cref="DataMarksHostExtensions"/> writes them, so nothing depends on synchronization and nothing
/// reaches Google (TC-4). Every name, title and id is invented. Also the readers of the page markup the test strategy
/// fixes (§3.2): the banner hook, the three section hooks, the read-only and message hooks and the form.
/// </summary>
public static partial class DataHealthHostExtensions
{
    public const string PagePath = "/workspace/data-health";

    public const string ConfirmationPath = "/workspace/data-health/course-deletion-confirmation";

    public const string ChangePasswordPath = "/sign-in/change-password";

    public const string BannerHook = "data-health-banner";

    public const string TechnicalHook = "data-health-technical";

    public const string TeachingHook = "data-health-teaching";

    public const string ArchivedHook = "data-health-archived";

    public const string ReadOnlyHook = "data-health-read-only";

    public const string MessageHook = "data-health-message";

    public const string FormHook = "course-deletion-confirmation";

    public const string CourseIdsField = "courseIds";

    public const string UnknownStateReason = "unknown_course_state";

    public const string ValueOutOfRangeReason = "value_out_of_range";

    private static readonly string[] SectionHooks = [ReadOnlyHook, TechnicalHook, TeachingHook, ArchivedHook];

    /// <summary>Seeding starts after the first retention purge (US-037), so the purge cannot race the inserts.</summary>
    public static Task ReadyToSeedAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.WaitForPurgeRunsAsync(1, cancellationToken);

    /// <summary>A course with Google id <c>CourseTestData.CourseId(ordinal)</c>; returns its internal id.</summary>
    public static async Task<long> CourseAsync(
        this InstallationTestHost host,
        int ordinal,
        string name,
        CancellationToken cancellationToken,
        string state = CourseTestData.States.Active,
        DateTimeOffset? lastCompleteReadAt = null)
    {
        var id = await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: CourseTestData.CourseId(ordinal), name: name, state: state);
        if (lastCompleteReadAt is not null)
        {
            await host.ExecuteAsync(
                "UPDATE course SET last_complete_read_at = @read WHERE id = @id",
                cancellationToken,
                ("read", lastCompleteReadAt),
                ("id", id));
        }

        return id;
    }

    /// <summary>
    /// An item of the course with maximum <paramref name="maximum"/> and one student <c>"&lt;surname&gt; Test"</c> on the
    /// roster whose submission carries the given grades. Returns the submission id.
    /// </summary>
    public static async Task<long> GradedAsync(
        this InstallationTestHost host,
        long courseId,
        int ordinal,
        string itemTitle,
        decimal? maximum,
        string surname,
        decimal? assigned,
        decimal? draft,
        CancellationToken cancellationToken)
    {
        var item = await CourseWorkRows.InsertCourseWorkAsync(
            host, courseId, cancellationToken, googleId: CourseWorkTestData.ItemId(ordinal), title: itemTitle, maxPoints: maximum);
        return await host.SubmissionAsync(courseId, item, ordinal, surname, assigned, draft, cancellationToken);
    }

    /// <summary>A further student's submission of an existing item.</summary>
    public static async Task<long> SubmissionAsync(
        this InstallationTestHost host,
        long courseId,
        long itemId,
        int ordinal,
        string surname,
        decimal? assigned,
        decimal? draft,
        CancellationToken cancellationToken)
    {
        var student = await CourseRows.InsertParticipantAsync(
            host, cancellationToken, googleUserId: CourseTestData.UserId(ordinal),
            email: CourseTestData.Email("student." + ordinal), fullName: surname + " Test", surname: surname, givenName: "Test");
        await CourseRows.InsertMembershipAsync(host, courseId, student, cancellationToken, firstSeenAt: host.Time.GetUtcNow());
        return await CourseWorkRows.InsertSubmissionAsync(
            host, itemId, student, cancellationToken, googleId: CourseWorkTestData.SubmissionId(ordinal),
            state: CourseWorkTestData.StateCodes.Returned, assignedGrade: assigned, draftGrade: draft);
    }

    /// <summary>An item of the course marked "not in Classroom" since <paramref name="since"/> (US-058).</summary>
    public static async Task<long> ItemNotInClassroomAsync(
        this InstallationTestHost host,
        long courseId,
        int ordinal,
        string title,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var item = await CourseWorkRows.InsertCourseWorkAsync(
            host, courseId, cancellationToken, googleId: CourseWorkTestData.ItemId(ordinal), title: title, maxPoints: 10m);
        await host.ExecuteAsync(
            "UPDATE course_work SET not_in_classroom_since = @since WHERE id = @id",
            cancellationToken,
            ("since", since),
            ("id", item));
        return item;
    }

    /// <summary>A skip record of US-058 for the Google course id (no foreign key to <c>course</c>).</summary>
    public static Task<int> SkipAsync(
        this InstallationTestHost host,
        string courseGoogleId,
        string? courseName,
        string reason,
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            """
            INSERT INTO sync_skipped_course (course_google_id, course_name, reason, skipped_since, created_at, updated_at)
            VALUES (@googleId, @name, @reason, @since, @since, @since)
            """,
            cancellationToken,
            ("googleId", courseGoogleId),
            ("name", courseName),
            ("reason", reason),
            ("since", since));

    /// <summary>
    /// The single <c>sync_state</c> row. <paramref name="emptySince"/> also sets the US-059 flag — FR-008.1 keeps the two
    /// consistent; the new column is written only when a test gives it, so the other tests insert the US-059 row shape.
    /// </summary>
    public static Task<int> SyncStateAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken,
        string status = "completed",
        DateTimeOffset? startedAt = null,
        string? lastError = null,
        string? failedStep = null,
        DateTimeOffset? lastSuccessfulRunAt = null,
        DateTimeOffset? emptySince = null)
    {
        var started = startedAt ?? host.Time.GetUtcNow().AddHours(-1);
        DateTimeOffset? finished = status == "running" ? null : started.AddMinutes(5);
        var columns = emptySince is null ? string.Empty : ", course_list_empty_since";
        var values = emptySince is null ? string.Empty : ", @emptySince";
        return host.ExecuteAsync(
            $"""
            INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count, last_error,
                                    failed_step, last_successful_run_at, last_course_list_empty, created_at, updated_at{columns})
            VALUES (true, @status, @runId, @startedAt, @finishedAt, 0, @lastError, @failedStep, @lastSuccess, @empty,
                    @stamp, @stamp{values})
            """,
            cancellationToken,
            ("status", status),
            ("runId", Guid.NewGuid()),
            ("startedAt", started),
            ("finishedAt", finished),
            ("lastError", lastError),
            ("failedStep", failedStep),
            ("lastSuccess", lastSuccessfulRunAt),
            ("empty", emptySince is not null),
            ("stamp", host.Time.GetUtcNow()),
            ("emptySince", emptySince));
    }

    /// <summary>A completed run that succeeded <paramref name="ago"/> before the clock — no technical problem of its own.</summary>
    public static Task<int> RecentSuccessAsync(
        this InstallationTestHost host, CancellationToken cancellationToken, TimeSpan? ago = null)
    {
        var success = host.Time.GetUtcNow() - (ago ?? TimeSpan.FromHours(1));
        return host.SyncStateAsync(
            cancellationToken, startedAt: success.AddMinutes(-5), lastSuccessfulRunAt: success);
    }

    /// <summary>Moves the stored last successful legitimacy check (and its compatibility answer) to the given values.</summary>
    public static Task<int> LastCheckAsync(
        this InstallationTestHost host,
        DateTimeOffset lastSuccessfulCheckAt,
        CancellationToken cancellationToken,
        string compatibility = "supported") =>
        host.ExecuteAsync(
            "UPDATE legitimacy_state SET last_successful_check_at = @at, compatibility = @compatibility",
            cancellationToken,
            ("at", lastSuccessfulCheckAt),
            ("compatibility", compatibility));

    /// <summary>The stored mark of one course: its code and instant.</summary>
    public static async Task<(string? Mark, DateTimeOffset? Since)> MarkOfAsync(
        this InstallationTestHost host, long courseId, CancellationToken cancellationToken) =>
        Assert.Single(await host.QueryAsync(
            "SELECT absence_mark, absent_since FROM course WHERE id = @id",
            r => (r.IsDBNull(0) ? null : r.GetString(0), r.IsDBNull(1) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(1)),
            cancellationToken,
            ("id", courseId)));

    /// <summary>The stored ids of every confirmation audit row, in row order (null for a refused row).</summary>
    public static Task<IReadOnlyList<long[]?>> ConfirmedIdsAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync(
            "SELECT confirmed_course_ids FROM audit_event WHERE action = 'courses_confirmed_deleted' ORDER BY id",
            r => r.IsDBNull(0) ? null : r.GetFieldValue<long[]>(0),
            cancellationToken);

    /// <summary>Every course row as JSON text, ordered — the "nothing changed" fingerprint of the confirmation.</summary>
    public static Task<IReadOnlyList<string>> CourseFingerprintAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync("SELECT row_to_json(c)::text FROM course c ORDER BY id", r => r.GetString(0), cancellationToken);

    /// <summary>The form fields of a confirmation naming the given values (one repeated field per value).</summary>
    public static IEnumerable<KeyValuePair<string, string>> Ids(params object[] values) =>
        values.Select(v => new KeyValuePair<string, string>(CourseIdsField, Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)!));

    // ---- HTML ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Strategy §3.2 (US-075 strategy §3.1): the banner's text, or null when the page has none. The banner is the
    /// <c>&lt;p&gt;</c> with id <c>data-health-banner</c>; since US-075 FR-001 its parts are separate links, so the text is
    /// the whole element's with tags removed, not replaced by spaces — as a browser shows «A, B» for
    /// <c>&lt;a&gt;A&lt;/a&gt;, &lt;a&gt;B&lt;/a&gt;</c>. The links are read by <see cref="BannerLinks"/>.
    /// </summary>
    public static string? Banner(string html) =>
        BannerHtml(html) is { } banner
            ? Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(banner, string.Empty)), " ").Trim()
            : null;

    /// <summary>The banner element's markup, from its opening tag to its closing <c>&lt;/p&gt;</c>, or null when absent.</summary>
    public static string? BannerHtml(string html)
    {
        var at = html.IndexOf($"id=\"{BannerHook}\"", StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var start = html.LastIndexOf('<', at);
        var end = html.IndexOf("</p>", at, StringComparison.OrdinalIgnoreCase);
        Assert.True(end > at, "The banner element is not closed.");
        return html[start..(end + "</p>".Length)];
    }

    /// <summary>US-075 strategy §3.1: every link inside the banner — its decoded href, its class attribute and its text.</summary>
    public static IReadOnlyList<(string Href, string Class, string Text)> BannerLinks(string html)
    {
        var banner = BannerHtml(html);
        Assert.True(banner is not null, "The page has no banner.");
        return AnchorTag().Matches(banner)
            .Select(m => (
                WebUtility.HtmlDecode(AttributeValue(m.Groups["attrs"].Value, "href") ?? string.Empty),
                AttributeValue(m.Groups["attrs"].Value, "class") ?? string.Empty,
                Text(m.Groups["body"].Value)))
            .ToList();
    }

    /// <summary>
    /// US-075 strategy §3.2: the raw markup of a page section, from its hook to the next section hook — the markup twin of
    /// <see cref="Section"/>, for placement and element checks.
    /// </summary>
    public static string SectionHtml(string html, string hook)
    {
        var start = html.IndexOf($"id=\"{hook}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The page has no section #{hook}.");
        var end = SectionHooks
            .Where(h => h != hook)
            .Select(h => html.IndexOf($"id=\"{h}\"", start, StringComparison.Ordinal))
            .Where(i => i > start)
            .DefaultIfEmpty(html.Length)
            .Min();
        return html[html.LastIndexOf('<', start)..end];
    }

    /// <summary>
    /// US-075 strategy §3.2: every hint paragraph in the markup — a <c>&lt;p&gt;</c> whose class list holds
    /// <see cref="HintClass"/> — with its raw inner markup, its text and its position, in page order.
    /// </summary>
    public static IReadOnlyList<(string Inner, string Text, int Position)> HintParagraphs(string markup) =>
        Paragraph().Matches(markup)
            .Where(m => (AttributeValue(m.Groups["attrs"].Value, "class") ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains(HintClass, StringComparer.Ordinal))
            .Select(m => (m.Groups["body"].Value, Text(m.Groups["body"].Value), m.Index))
            .ToList();

    /// <summary>US-075 strategy §3.2: the class of a hint paragraph.</summary>
    public const string HintClass = "data-health-hint";

    /// <summary>US-075 strategy §3.1: the no-wrap class of a banner link.</summary>
    public const string BannerPartClass = "data-health-banner-part";

    private static string? AttributeValue(string attributes, string name)
    {
        var match = Regex.Match(attributes, $@"\b{name}=""(?<v>[^""]*)""", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["v"].Value : null;
    }

    /// <summary>
    /// Strategy §3.2: the text of a page section, from its hook to the next section hook (sections may nest markup of the
    /// same tag, so no closing tag is matched). Fails when the page has no such section.
    /// </summary>
    public static string Section(string html, string hook)
    {
        var start = html.IndexOf($"id=\"{hook}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The page has no section #{hook}.");
        var ends = SectionHooks
            .Where(h => h != hook)
            .Select(h => html.IndexOf($"id=\"{h}\"", start, StringComparison.Ordinal))
            .Where(i => i > start)
            .DefaultIfEmpty(html.Length);
        var end = ends.Min();
        var tagStart = html.LastIndexOf('<', start);
        return Text(html[tagStart..end]);
    }

    /// <summary>Whether the page carries the hook at all.</summary>
    public static bool Has(string html, string hook) => html.Contains($"id=\"{hook}\"", StringComparison.Ordinal);

    /// <summary>The values of the confirmation form's checkboxes, in page order; empty when the page has no form.</summary>
    public static IReadOnlyList<string> CheckboxValues(string html)
    {
        var start = html.IndexOf($"id=\"{FormHook}\"", StringComparison.Ordinal);
        if (start < 0)
        {
            return [];
        }

        var end = html.IndexOf("</form>", start, StringComparison.OrdinalIgnoreCase);
        var form = end < 0 ? html[start..] : html[start..end];
        return Checkbox().Matches(form)
            .Select(m => m.Value)
            .Where(tag => tag.Contains($"name=\"{CourseIdsField}\"", StringComparison.Ordinal))
            .Select(tag => CheckboxValue().Match(tag).Groups["v"].Value)
            .ToList();
    }

    /// <summary>
    /// A fragment's text: tags replaced by spaces, entities decoded, white space collapsed, and the typographic
    /// apostrophes (’ ʼ) read as the Specification's ASCII one — FR-007 writes «зв'язку» with it.
    /// </summary>
    public static string Text(string fragment) =>
        Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(fragment, " ")), " ")
            .Replace('’', '\'')
            .Replace('ʼ', '\'')
            .Trim();

    /// <summary>The number of times <paramref name="text"/> occurs in <paramref name="within"/>.</summary>
    public static int Occurrences(string within, string text)
    {
        var count = 0;
        for (var i = within.IndexOf(text, StringComparison.Ordinal); i >= 0; i = within.IndexOf(text, i + text.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [GeneratedRegex(@"<a\b(?<attrs>[^>]*)>(?<body>.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex AnchorTag();

    [GeneratedRegex(@"<p\b(?<attrs>[^>]*)>(?<body>.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Paragraph();

    [GeneratedRegex(@"<input\b[^>]*type=""checkbox""[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Checkbox();

    [GeneratedRegex(@"\bvalue=""(?<v>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex CheckboxValue();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
