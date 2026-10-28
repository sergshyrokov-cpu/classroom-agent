using System.Globalization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-063 spec FR-006: the "generated" and "data from Google as of" texts the tests pin, Ukrainian and English (FR-006.3,
/// I-7), and the date-time format of FR-006.1 / VR-003 — the UTC instant converted to the school's zone (Kyiv, TC-8) and
/// shown as the language's short date pattern plus <c>HH:mm</c>, no seconds.
/// </summary>
public sealed record GeneratedLinesTexts(string Language, string Generated, string DataAsOf, string Unknown)
{
    public static GeneratedLinesTexts Uk { get; } = new("uk", "Сформовано: {0}", "Дані з Google на: {0}", "невідомо");

    public static GeneratedLinesTexts En { get; } = new("en", "Generated: {0}", "Data from Google as of: {0}", "unknown");

    public static GeneratedLinesTexts Of(string language) => language == "en" ? En : Uk;

    public CultureInfo Culture => CultureInfo.GetCultureInfo(Language);

    /// <summary>FR-006.1, VR-003: the instant in Kyiv, as the culture's short date pattern and hours:minutes.</summary>
    public string Instant(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, JournalTestData.Kyiv)
            .ToString(Culture.DateTimeFormat.ShortDatePattern + " HH:mm", Culture);

    /// <summary>FR-001: the "generated" line of the build instant B.</summary>
    public string GeneratedLine(DateTimeOffset b) => string.Format(Culture, Generated, Instant(b));

    /// <summary>FR-002: the "data from Google as of" line of the last complete read, "unknown" when none (FR-002.3).</summary>
    public string DataAsOfLine(DateTimeOffset? lastRead) =>
        string.Format(Culture, DataAsOf, lastRead is { } read ? Instant(read) : Unknown);

    /// <summary>The label text of each line, without its value — what no printed cell may contain (AC-003).</summary>
    public IReadOnlyList<string> Stems =>
        [Generated[..Generated.IndexOf("{0}", StringComparison.Ordinal)].TrimEnd(),
         DataAsOf[..DataAsOf.IndexOf("{0}", StringComparison.Ordinal)].TrimEnd()];
}
