using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-077 AC-008, spec FR-007: the log event ids declared under <c>src</c> do not collide — the id of
/// <c>ReadOnlyWriteRefused</c> is 5290, <c>RetentionPurgeStarted</c> keeps 5201, and each id names one event.
/// </summary>
public sealed partial class EventIdCollisionTests
{
    private static readonly Lazy<IReadOnlyList<(int Id, string Name)>> Declarations = new(Scan);

    /// <summary>AC-008, FR-007: the refused-write event has exactly the id 5290.</summary>
    [Fact]
    public void ReadOnlyWriteRefused_HasTheIdFrom_FR007() =>
        Assert.Equal([5290], IdsOf("ReadOnlyWriteRefused"));

    /// <summary>AC-008, FR-007: the retention purge start event keeps exactly the id 5201.</summary>
    [Fact]
    public void RetentionPurgeStarted_KeepsItsId() =>
        Assert.Equal([5201], IdsOf("RetentionPurgeStarted"));

    /// <summary>AC-008, FR-007: id 5290 and id 5201 each belong to one event name only.</summary>
    [Fact]
    public void TheTwoIds_BelongToOneEventEach()
    {
        Assert.Equal(["ReadOnlyWriteRefused"], NamesOf(5290));
        Assert.Equal(["RetentionPurgeStarted"], NamesOf(5201));
    }

    /// <summary>AC-008: guard that the scan is not vacuous — it finds events known to exist in both declaration styles.</summary>
    [Fact]
    public void TheScan_FindsKnownEvents()
    {
        Assert.Contains(2203, IdsOf("AdminSignInCallbackFailed"));
        Assert.Contains(2420, IdsOf("SyncGoogleRetry"));
    }

    private static List<int> IdsOf(string name) =>
        Declarations.Value.Where(d => d.Name == name).Select(d => d.Id).Distinct().Order().ToList();

    private static List<string> NamesOf(int id) =>
        Declarations.Value.Where(d => d.Id == id).Select(d => d.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    private static List<(int Id, string Name)> Scan()
    {
        var source = Path.Combine(StaticFiles.RepositoryRoot(), "src");
        var found = new List<(int, string)>();
        foreach (var file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (relative.Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase) || part.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (Match m in Declared().Matches(text))
            {
                found.Add((int.Parse(m.Groups["id"].Value, System.Globalization.CultureInfo.InvariantCulture), m.Groups["name"].Value));
            }

            foreach (Match m in Initialised().Matches(text))
            {
                found.Add((int.Parse(m.Groups["id"].Value, System.Globalization.CultureInfo.InvariantCulture), m.Groups["name"].Value));
            }
        }

        return found;
    }

    [GeneratedRegex(@"EventId\s+\w+\s*=\s*new\(\s*(?<id>\d+)\s*,\s*""(?<name>\w+)""\s*\)")]
    private static partial Regex Declared();

    [GeneratedRegex(@"EventId\s*=\s*(?<id>\d+)\s*,\s*(?:\r?\n\s*)?EventName\s*=\s*""(?<name>\w+)""", RegexOptions.Multiline)]
    private static partial Regex Initialised();
}
