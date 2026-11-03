using System.Xml.Linq;
using ClassroomAgent.Tests.TestInfrastructure;
using S = ClassroomAgent.Tests.TestInfrastructure.SearchTestData;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-071 AC-001, AC-002, AC-005, AC-009, FR-001, FR-011, NFR-073, NFR-078: the texts of the header search and its results page are
/// entries of the installation's translation files in Ukrainian and English, worded exactly as openapi
/// <c>x-translation-keys</c>, none ends with a period, and no view holds one as a literal.
/// </summary>
public sealed class SearchTranslationTests
{
    /// <summary>Openapi <c>x-translation-keys.new-in-both-files</c>, byte for byte.</summary>
    public static TheoryData<string, string, string> NewTexts => new()
    {
        { "Search.Field.Label", "uk", "Пошук" },
        { "Search.Field.Label", "en", "Search" },
        { "Search.Field.Submit", "uk", "Знайти" },
        { "Search.Field.Submit", "en", "Find" },
        { "Search.Title", "uk", "Результати пошуку" },
        { "Search.Title", "en", "Search results" },
        { "Search.Message.TooShort", "uk", "Введіть щонайменше 2 символи" },
        { "Search.Message.TooShort", "en", "Enter at least 2 characters" },
        { "Search.Empty.NothingFound", "uk", "Нічого не знайдено за запитом «{0}»" },
        { "Search.Empty.NothingFound", "en", "Nothing found for \"{0}\"" },
        { "Search.Empty.Reminder", "uk", "Пошук знаходить поточні імена й назви з Google; за колишнім прізвищем або старим email не знайти" },
        { "Search.Empty.Reminder", "en", "Search finds current names and titles from Google; a former surname or an old email will not be found" },
        { "Search.Warning", "uk", "Шуканий текст залишається в історії браузера; на спільному комп'ютері очистіть її" },
        { "Search.Warning", "en", "The text you search for stays in the browser history; clear it on a shared computer" },
        { "Search.Group.Students", "uk", "Студенти" },
        { "Search.Group.Students", "en", "Students" },
        { "Search.Group.Teachers", "uk", "Викладачі" },
        { "Search.Group.Teachers", "en", "Teachers" },
        { "Search.Group.Courses", "uk", "Курси" },
        { "Search.Group.Courses", "en", "Courses" },
        { "Search.Group.MeetCodes", "uk", "Коди зустрічей Meet" },
        { "Search.Group.MeetCodes", "en", "Meet codes" },
        { "Search.Limit.Shown", "uk", "показано {0} з {1}, уточніть запит" },
        { "Search.Limit.Shown", "en", "showing {0} of {1}, refine your search" },
        { "Search.Limit.WholeList", "uk", "Увесь список із цим пошуком" },
        { "Search.Limit.WholeList", "en", "The whole list with this search" },
        { "Search.Teacher.Courses", "uk", "Курси викладача" },
        { "Search.Teacher.Courses", "en", "Teacher's courses" },
    };

    /// <summary>Openapi <c>x-translation-keys.changed-texts</c>, byte for byte.</summary>
    public static TheoryData<string, string, string> ChangedTexts => new()
    {
        {
            "PageSearchHint.Ordinary", "uk",
            "На цій сторінці — Ctrl+F (на Mac — ⌘F; на телефоні — меню браузера → «Знайти на сторінці»), у всій програмі — поле «{0}» угорі"
        },
        {
            "PageSearchHint.Ordinary", "en",
            "On this page: Ctrl+F (⌘F on Mac; on a phone — browser menu → \"Find in page\"); across the program: the \"{0}\" field at the top"
        },
        {
            "PageSearchHint.ListField", "uk",
            "У цьому списку — поле «{0}» нижче (Ctrl+F бачить лише показані рядки), у всій програмі — поле «{1}» угорі"
        },
        {
            "PageSearchHint.ListField", "en",
            "In this list: the \"{0}\" field below (Ctrl+F sees only the rows shown); across the program: the \"{1}\" field at the top"
        },
    };

    private static readonly string[] LiteralFragments =
        ["Результати пошуку", "Search results", "Шуканий текст", "The text you search for"];

    private static IReadOnlyDictionary<string, string> Entries(string language) =>
        XDocument.Load(
                Path.Combine(StaticFiles.RepositoryRoot(), "src", "ClassroomAgent.Application", "Localization", $"SharedResource.{language}.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(data => (string?)data.Attribute("name") ?? string.Empty, data => (string?)data.Element("value") ?? string.Empty);

    private static IEnumerable<string> Views() =>
        new[] { "ClassroomAgent.Web", "ClassroomAgent.ControlPlane" }
            .Select(project => Path.Combine(StaticFiles.RepositoryRoot(), "src", project, "Views"))
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories));

    /// <summary>AC-001, AC-002, AC-005: each new text is an entry of the file of its language, worded exactly as specified.</summary>
    [Theory]
    [MemberData(nameof(NewTexts))]
    public void ANewText_IsAnEntry_WithTheSpecifiedWording(string key, string language, string expected)
    {
        var entries = Entries(language);

        Assert.True(entries.TryGetValue(key, out var value), $"{language}: no entry {key}.");
        Assert.Equal(expected, value);
    }

    /// <summary>AC-009, FR-011: the two page-search hint templates changed to the final wording.</summary>
    [Theory]
    [MemberData(nameof(ChangedTexts))]
    public void AChangedHintText_HasTheSpecifiedWording(string key, string language, string expected)
    {
        var entries = Entries(language);

        Assert.True(entries.TryGetValue(key, out var value), $"{language}: no entry {key}.");
        Assert.Equal(expected, value);
    }

    /// <summary>The unwritten rule for one-line hints and notices: no final period in any new text.</summary>
    [Fact]
    public void NoNewText_EndsWithAPeriod()
    {
        foreach (var language in new[] { "uk", "en" })
        {
            var entries = Entries(language);
            foreach (var key in S.Keys.All)
            {
                Assert.True(entries.TryGetValue(key, out var value), $"{language}: no entry {key}.");
                Assert.False(value.TrimEnd().EndsWith('.'), $"{language}: {key} ends with a period.");
            }
        }
    }

    /// <summary>AC-004: the limit line is a template for the rows shown and the total.</summary>
    [Fact]
    public void TheLimitLine_HoldsBothPlaceholders()
    {
        foreach (var language in new[] { "uk", "en" })
        {
            Assert.True(Entries(language).TryGetValue(S.Keys.LimitShown, out var value), $"{language}: no entry {S.Keys.LimitShown}.");
            Assert.Contains("{0}", value, StringComparison.Ordinal);
            Assert.Contains("{1}", value, StringComparison.Ordinal);
        }
    }

    /// <summary>NFR-073: no view of the installation or the Control Plane holds a heading or a warning of the results page as a literal.</summary>
    [Fact]
    public void NoView_HoldsAResultsPageTextAsALiteral()
    {
        var views = Views().ToList();
        Assert.NotEmpty(views);

        foreach (var view in views)
        {
            var content = File.ReadAllText(view);
            foreach (var fragment in LiteralFragments)
            {
                Assert.False(
                    content.Contains(fragment, StringComparison.Ordinal),
                    $"{Path.GetFileName(view)} holds the literal '{fragment}'.");
            }
        }
    }
}
