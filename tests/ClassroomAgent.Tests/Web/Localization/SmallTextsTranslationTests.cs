using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-069: the new and kept texts are entries of the Application translation files in both languages, the prompt of the
/// Meet reports picker reuses its existing key, and no installation view holds them as a literal (spec FR-001, FR-003,
/// FR-004, FR-005, AC-010, NFR-073).
/// </summary>
public sealed class SmallTextsTranslationTests
{
    private static readonly string[] ForbiddenLiterals =
        ["Показати пароль", "Сховати пароль", "Show password", "Hide password", "Роль:", "Role:"];

    private static readonly string[] ForbiddenHeadingPatterns =
    [
        @">\s*Курси\s*<", @">\s*Дані\s*<", @">\s*Синхронізація\s*<",
        @">\s*Courses\s*<", @">\s*Data\s*<", @">\s*Synchronization\s*<",
    ];

    public static TheoryData<string, string> Pairs => new()
    {
        { "Роль: {0}", "Role: {0}" },
        { "Курси", "Courses" },
        { "Дані", "Data" },
        { "Синхронізація", "Synchronization" },
        { "Показати пароль", "Show password" },
        { "Сховати пароль", "Hide password" },
    };

    private static Dictionary<string, string> Load(string language) =>
        XDocument.Load(
                Path.Combine(
                    StaticFiles.RepositoryRoot(), "src", "ClassroomAgent.Application", "Localization", $"SharedResource.{language}.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(
                data => (string)data.Attribute("name")!,
                data => (string?)data.Element("value") ?? string.Empty,
                StringComparer.Ordinal);

    /// <summary>AC-010: each text has one key whose Ukrainian and English values are the pair of the Specification.</summary>
    [Theory]
    [MemberData(nameof(Pairs))]
    public void EachText_IsOneKey_InBothLanguages(string uk, string en)
    {
        var ukrainian = Load("uk");
        var english = Load("en");

        Assert.Contains(
            ukrainian,
            entry => entry.Value == uk && english.TryGetValue(entry.Key, out var value) && value == en);
    }

    /// <summary>FR-001: the two role names keep their values.</summary>
    [Fact]
    public void TheRoleNames_KeepTheirValues()
    {
        var ukrainian = Load("uk");
        var english = Load("en");

        Assert.Equal("Адміністратор школи", ukrainian["Landing.Role.Admin"]);
        Assert.Equal("Декан", ukrainian["Landing.Role.Dean"]);
        Assert.Equal("School administrator", english["Landing.Role.Admin"]);
        Assert.Equal("Dean", english["Landing.Role.Dean"]);
    }

    /// <summary>FR-004: the prompt reuses the existing key; no second key carries the Ukrainian text.</summary>
    [Fact]
    public void ThePrompt_ReusesTheExistingKey()
    {
        var ukrainian = Load("uk");
        var english = Load("en");

        Assert.Equal("— оберіть курс —", ukrainian["Journal.Form.ChooseCourse"]);
        Assert.Equal("— choose a course —", english["Journal.Form.ChooseCourse"]);
        Assert.Single(ukrainian, entry => entry.Value == "— оберіть курс —");
    }

    /// <summary>AC-010: the two files define the same keys.</summary>
    [Fact]
    public void TheTwoFiles_HaveTheSameKeys()
    {
        var ukrainian = Load("uk").Keys.Order(StringComparer.Ordinal).ToList();
        var english = Load("en").Keys.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(ukrainian, english);
    }

    /// <summary>NFR-073: no installation view holds the role line, the password-control names or the Database headings as a literal.</summary>
    [Fact]
    public void NoInstallationView_HoldsTheTextsAsALiteral()
    {
        var views = InlineCode.ViewsOf(Styling.Installation);
        Assert.NotEmpty(views);
        var offenders = new List<string>();

        foreach (var view in views)
        {
            var content = Styling.WithoutViewComments(File.ReadAllText(Path.Combine(StaticFiles.RepositoryRoot(), view)));
            offenders.AddRange(
                ForbiddenLiterals
                    .Where(literal => content.Contains(literal, StringComparison.Ordinal))
                    .Select(literal => $"{view}: '{literal}'"));
            offenders.AddRange(
                ForbiddenHeadingPatterns
                    .Where(pattern => Regex.IsMatch(content, pattern))
                    .Select(pattern => $"{view}: heading {pattern}"));
        }

        Assert.Empty(offenders);
    }
}
