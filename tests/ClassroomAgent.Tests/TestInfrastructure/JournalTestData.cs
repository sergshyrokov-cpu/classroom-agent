using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The fixed values of US-025: the page, its query parameters, the time zone setting, the translation key families
/// of the openapi enums, and a synthetic period in <c>Europe/Kyiv</c> (TC-8). Kept in one place so a rename shows up
/// as one edit (the <see cref="CourseWorkTestData"/> pattern).
/// </summary>
/// <remarks>Every name, email and title used with these values is invented (TC-4).</remarks>
public static class JournalTestData
{
    /// <summary>api-design §2.1: the journal page.</summary>
    public const string Path = "/workspace/journal";

    /// <summary>Spec FR-010: the required school time zone setting (an IANA id).</summary>
    public const string TimeZoneKey = "Installation:TimeZone";

    /// <summary>
    /// TC-8: every period and date test runs in a non-UTC zone with daylight saving — Kyiv, under the IANA id this
    /// runtime knows. The current id is <c>Europe/Kyiv</c> (tzdata 2022b); Windows' bundled ICU may know only the
    /// older <c>Europe/Kiev</c>, which is the same zone (finding in the US-025 test-generation report).
    /// </summary>
    public static string KyivZoneId { get; } = ResolvableId("Europe/Kyiv", "Europe/Kiev");

    public static TimeZoneInfo Kyiv { get; } = TimeZoneInfo.FindSystemTimeZoneById(KyivZoneId);

    /// <summary>The Dean's password in the host tests (any value the US-012 policy accepts).</summary>
    public const string DeanPassword = "Journal-Dean-Password-2026";

    /// <summary>Query parameter names (api-design §2.2).</summary>
    public static class Parameters
    {
        public const string CourseId = "courseId";

        public const string From = "from";

        public const string To = "to";

        public const string View = "view";
    }

    /// <summary>
    /// A September 2026 period. <see cref="InstallationTestHost.DefaultStart"/> (2026-09-17 08:00 UTC) lies inside it,
    /// so B is after every due date dated before the 17th and before every one dated after it.
    /// </summary>
    public static class Period
    {
        public static readonly DateOnly From = new(2026, 9, 1);

        public static readonly DateOnly To = new(2026, 9, 30);

        public const string FromText = "2026-09-01";

        public const string ToText = "2026-09-30";

        /// <summary>2026-09-01 00:00 in Kyiv (UTC+3, summer time) — the start of the UTC interval (spec FR-003).</summary>
        public static readonly DateTimeOffset StartUtc = new(2026, 8, 31, 21, 0, 0, TimeSpan.Zero);

        /// <summary>2026-10-01 00:00 in Kyiv — the exclusive end of the UTC interval.</summary>
        public static readonly DateTimeOffset EndUtc = new(2026, 9, 30, 21, 0, 0, TimeSpan.Zero);

        /// <summary>A UTC instant inside the period, before B.</summary>
        public static readonly DateTimeOffset Early = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

        /// <summary>A UTC instant inside the period, after B (2026-09-17 08:00 UTC).</summary>
        public static readonly DateTimeOffset Late = new(2026, 9, 25, 9, 0, 0, TimeSpan.Zero);
    }

    private static string ResolvableId(params string[] ids)
    {
        foreach (var id in ids)
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out _))
            {
                return id;
            }
        }

        throw new InvalidOperationException("None of the Kyiv time zone ids is known to this runtime: " + string.Join(", ", ids));
    }

    /// <summary>The query string of a journal request; null values are left out.</summary>
    public static string Url(long? courseId = null, string? from = null, string? to = null, string? view = null)
    {
        var parts = new List<string>();
        if (courseId is not null)
        {
            parts.Add($"{Parameters.CourseId}={courseId}");
        }

        if (from is not null)
        {
            parts.Add($"{Parameters.From}={Uri.EscapeDataString(from)}");
        }

        if (to is not null)
        {
            parts.Add($"{Parameters.To}={Uri.EscapeDataString(to)}");
        }

        if (view is not null)
        {
            parts.Add($"{Parameters.View}={Uri.EscapeDataString(view)}");
        }

        return parts.Count == 0 ? Path : Path + "?" + string.Join('&', parts);
    }

    /// <summary>The journal of the September period for one course.</summary>
    public static string SeptemberUrl(long courseId, string? view = null) =>
        Url(courseId, Period.FromText, Period.ToText, view);

    /// <summary>The translation keys of the openapi enums (api-design §2.5; spec FR-015).</summary>
    public static class TextKeys
    {
        /// <summary>Every cell state but <c>Empty</c> and <c>Grade</c> has one key (openapi <c>JournalCellState</c>).</summary>
        public static string Cell(JournalCellState state) => "Journal.Cell." + state;

        public static string Validation(JournalMessageKey key) => "Journal.Validation." + key;

        public static string Empty(JournalEmptyStateKey key) => "Journal.Empty." + key;

        public static IEnumerable<string> All =>
            Enum.GetValues<JournalCellState>()
                .Where(s => s is not JournalCellState.Empty and not JournalCellState.Grade)
                .Select(Cell)
                .Concat(Enum.GetValues<JournalMessageKey>().Select(Validation))
                .Concat(Enum.GetValues<JournalEmptyStateKey>().Select(Empty));
    }
}
