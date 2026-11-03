using System.Xml.Linq;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-070 AC-010, FR-006, FR-009, NFR-073, NFR-078: the new texts are entries of the installation's translation files in
/// Ukrainian and English, with the exact wording of the specification, and none of them ends with a period.
/// </summary>
public sealed class ListSearchTranslationTests
{
    private static readonly string[] NewKeys =
    [
        "Teachers.Form.Search", "MeetCodes.Form.Search", "Teachers.Empty.NoMatch", "MeetCodes.Empty.NoMatch",
        "Picker.Narrow.Label", "Picker.Narrow.NothingFound", "PageSearchHint.ListField",
    ];

    public static TheoryData<string, string, string> Texts => new()
    {
        { "Teachers.Form.Search", "uk", "Пошук за ім'ям або email" },
        { "Teachers.Form.Search", "en", "Search by name or email" },
        { "MeetCodes.Form.Search", "uk", "Пошук за кодом, організатором або курсом" },
        { "MeetCodes.Form.Search", "en", "Search by code, organizer or course" },
        { "Teachers.Empty.NoMatch", "uk", "Жоден викладач не відповідає пошуку" },
        { "Teachers.Empty.NoMatch", "en", "No teacher matches the search" },
        { "MeetCodes.Empty.NoMatch", "uk", "Жоден код у цьому списку не відповідає пошуку" },
        { "MeetCodes.Empty.NoMatch", "en", "No code in this list matches the search" },
        { "Picker.Narrow.Label", "uk", "Почніть вводити, щоб звузити список" },
        { "Picker.Narrow.Label", "en", "Start typing to narrow the list" },
        { "Picker.Narrow.NothingFound", "uk", "Нічого не знайдено" },
        { "Picker.Narrow.NothingFound", "en", "Nothing found" },
    };

    private static IReadOnlyDictionary<string, string> Entries(string language) =>
        XDocument.Load(
                Path.Combine(StaticFiles.RepositoryRoot(), "src", "ClassroomAgent.Application", "Localization", $"SharedResource.{language}.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(data => (string?)data.Attribute("name") ?? string.Empty, data => (string?)data.Element("value") ?? string.Empty);

    /// <summary>AC-010: each new text is an entry of the file of its language, worded exactly as specified.</summary>
    [Theory]
    [MemberData(nameof(Texts))]
    public void ANewText_IsAnEntry_WithTheSpecifiedWording(string key, string language, string expected)
    {
        var entries = Entries(language);

        Assert.True(entries.TryGetValue(key, out var value), $"{language}: no entry {key}.");
        Assert.Equal(expected, value);
    }

    /// <summary>The unwritten rule for one-line hints and notices: no final period, including the page-search hint template.</summary>
    [Fact]
    public void NoNewText_EndsWithAPeriod()
    {
        foreach (var language in new[] { "uk", "en" })
        {
            var entries = Entries(language);
            foreach (var key in NewKeys)
            {
                Assert.True(entries.TryGetValue(key, out var value), $"{language}: no entry {key}.");
                Assert.False(value.TrimEnd().EndsWith('.'), $"{language}: {key} ends with a period.");
            }
        }
    }
}
