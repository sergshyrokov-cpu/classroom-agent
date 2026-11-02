using System.Xml.Linq;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Localization;

/// <summary>
/// US-069: the show/hide password control names are entries of the Control Plane translation files in both languages,
/// and no Control Plane view holds them as a literal (spec FR-005, NFR-073).
/// </summary>
public sealed class PasswordToggleTranslationTests
{
    private static readonly string[] ForbiddenLiterals = ["Показати пароль", "Сховати пароль", "Show password", "Hide password"];

    private static Dictionary<string, string> Load(string language) =>
        XDocument.Load(
                Path.Combine(
                    StaticFiles.RepositoryRoot(), "src", "ClassroomAgent.ControlPlane", "Localization", $"SharedResource.{language}.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(
                data => (string)data.Attribute("name")!,
                data => (string?)data.Element("value") ?? string.Empty,
                StringComparer.Ordinal);

    /// <summary>FR-005.4: the name of the control while the password is hidden.</summary>
    [Fact]
    public void TheShowName_IsOneKey_InBothLanguages()
    {
        var english = Load("en");

        Assert.Contains(
            Load("uk"),
            entry => entry.Value == "Показати пароль" && english.TryGetValue(entry.Key, out var value) && value == "Show password");
    }

    /// <summary>FR-005.4: the name of the control while the password is shown.</summary>
    [Fact]
    public void TheHideName_IsOneKey_InBothLanguages()
    {
        var english = Load("en");

        Assert.Contains(
            Load("uk"),
            entry => entry.Value == "Сховати пароль" && english.TryGetValue(entry.Key, out var value) && value == "Hide password");
    }

    /// <summary>The two files define the same keys.</summary>
    [Fact]
    public void TheTwoFiles_HaveTheSameKeys()
    {
        var ukrainian = Load("uk").Keys.Order(StringComparer.Ordinal).ToList();
        var english = Load("en").Keys.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(ukrainian, english);
    }

    /// <summary>NFR-073: no Control Plane view holds the control names as a literal.</summary>
    [Fact]
    public void NoControlPlaneView_HoldsTheNamesAsALiteral()
    {
        var views = InlineCode.ViewsOf(Styling.ControlPlane);
        Assert.NotEmpty(views);
        var offenders = new List<string>();

        foreach (var view in views)
        {
            var content = Styling.WithoutViewComments(File.ReadAllText(Path.Combine(StaticFiles.RepositoryRoot(), view)));
            offenders.AddRange(
                ForbiddenLiterals
                    .Where(literal => content.Contains(literal, StringComparison.Ordinal))
                    .Select(literal => $"{view}: '{literal}'"));
        }

        Assert.Empty(offenders);
    }
}
