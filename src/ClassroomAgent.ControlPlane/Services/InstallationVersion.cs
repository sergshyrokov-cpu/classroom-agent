using System.Globalization;
using System.Text.RegularExpressions;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// An installation release version <c>MAJOR.MINOR.PATCH</c>: each part 0–999999 in decimal digits without
/// leading zeros, no suffix; compared numerically part by part (US-005 spec VR-002, I-7).
/// </summary>
public readonly partial record struct InstallationVersion(int Major, int Minor, int Patch) : IComparable<InstallationVersion>
{
    public const int MaxLength = 20;

    public static bool TryParse(string? text, out InstallationVersion version)
    {
        version = default;
        if (text is null || text.Length > MaxLength || !Shape().IsMatch(text))
        {
            return false;
        }

        var parts = text.Split('.');
        version = new InstallationVersion(
            int.Parse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture),
            int.Parse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture),
            int.Parse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture));
        return true;
    }

    public static bool operator <(InstallationVersion left, InstallationVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(InstallationVersion left, InstallationVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(InstallationVersion left, InstallationVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(InstallationVersion left, InstallationVersion right) => left.CompareTo(right) >= 0;

    /// <summary>The canonical text, as stored and logged.</summary>
    public string ToText() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

    public int CompareTo(InstallationVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major)
        : Minor != other.Minor ? Minor.CompareTo(other.Minor)
        : Patch.CompareTo(other.Patch);

    [GeneratedRegex(@"\A(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})\z", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
