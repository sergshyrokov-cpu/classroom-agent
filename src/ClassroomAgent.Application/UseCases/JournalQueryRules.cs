using System.Globalization;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The query-string rules of the journal (US-025 VR-001 … VR-004) that the report page (US-027 spec FR-011) applies
/// unchanged.
/// </summary>
internal static class JournalQueryRules
{
    /// <summary>VR-002: the form an HTML date input sends.</summary>
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>VR-002, spec I-4: the years a period may lie in.</summary>
    private const int FirstYear = 2000;

    private const int LastYear = 2100;

    /// <summary>VR-001: <see cref="long.MaxValue"/> has 19 digits.</summary>
    private const int MaxCourseIdDigits = 19;

    internal enum Presence
    {
        Absent,
        One,
        Repeated,
    }

    /// <summary>VR-001 … VR-004: absent or empty is absent; one value is one; more is malformed.</summary>
    internal static Presence Single(IReadOnlyList<string?> values, out string value)
    {
        value = string.Empty;
        switch (values.Count)
        {
            case 0:
                return Presence.Absent;
            case 1:
                value = values[0] ?? string.Empty;
                return value.Length == 0 ? Presence.Absent : Presence.One;
            default:
                return Presence.Repeated;
        }
    }

    /// <summary>VR-001: ASCII decimal digits only, a positive 64-bit integer.</summary>
    internal static long? ParseCourseId(string text) =>
        text.Length <= MaxCourseIdDigits
        && text.All(char.IsAsciiDigit)
        && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id > 0
            ? id
            : null;

    /// <summary>
    /// VR-002: absent gives the default; otherwise one real <c>yyyy-MM-dd</c> date of 2000 … 2100. A malformed value
    /// sets <paramref name="malformed"/> and gives null.
    /// </summary>
    internal static DateOnly? ReadDate(IReadOnlyList<string?> values, DateOnly fallback, out bool malformed)
    {
        malformed = false;
        switch (Single(values, out var text))
        {
            case Presence.Absent:
                return fallback;
            case Presence.One
                when text.All(c => char.IsAsciiDigit(c) || c == '-')
                     && DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                     && date.Year is >= FirstYear and <= LastYear:
                return date;
            default:
                malformed = true;
                return null;
        }
    }

    /// <summary>A validated date as the form sends it.</summary>
    internal static string FormatDate(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
}
