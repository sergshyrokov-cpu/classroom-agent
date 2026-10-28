using System.Globalization;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-066 spec FR-002, FR-003 (BR-086): which student rows of one journal or report are namesakes — their displayed
/// name coincides with another row's under the UI culture's comparer with letter case ignored, names as stored — and
/// the email such a row carries.
/// </summary>
internal static class NamesakeRule
{
    /// <summary>
    /// For each displayed name, in order, whether another name of the list coincides with it (FR-002.3, FR-002.4); a
    /// null name (an unnamed row) never coincides (FR-002.2).
    /// </summary>
    public static IReadOnlyList<bool> Find(IReadOnlyList<string?> names, CultureInfo uiCulture)
    {
        var counts = new Dictionary<string, int>(StringComparer.Create(uiCulture, ignoreCase: true));
        foreach (var name in names)
        {
            if (name is not null)
            {
                counts[name] = counts.GetValueOrDefault(name) + 1;
            }
        }

        return names.Select(n => n is not null && counts[n] > 1).ToList();
    }

    /// <summary>FR-003.1, VR-001: the stored email a namesake row carries, or null when it has none.</summary>
    public static string? Email(bool isNamesake, string? email) =>
        isNamesake && !string.IsNullOrEmpty(email) ? email : null;
}
