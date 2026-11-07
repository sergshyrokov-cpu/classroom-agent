using System.Xml.Linq;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.DataHealthHostExtensions;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-075 FR-008, VR-003, NFR-073, FR-001.4, FR-009.1, SEC-004: the eight new texts are entries of both installation
/// translation files under the keys API design §3 fixed, with the agreed values; no view holds them as literals; and the
/// installation's own stylesheet carries the no-wrap rule of the banner links and the hint class.
/// </summary>
public sealed class DataHealthHintTranslationTests
{
    public static TheoryData<string> Languages => new("uk", "en");

    private static IReadOnlyDictionary<string, string> Entries(string language) =>
        XDocument.Load(Path.Combine(
                StaticFiles.RepositoryRoot(), "src", "ClassroomAgent.Application", "Localization", $"SharedResource.{language}.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(d => (string?)d.Attribute("name") ?? string.Empty, d => (string?)d.Element("value") ?? string.Empty);

    /// <summary>Api-design §3: key → agreed text (spec FR-008), apostrophes as the files may write them read as ASCII.</summary>
    private static IReadOnlyList<(string Key, string Value)> Expected(string language)
    {
        var t = DataHealthHintTexts.Of(language);
        return
        [
            ("DataHealth.Hint.AboveMaximum", t.AboveMaximum),
            ("DataHealth.Hint.ItemsNotInClassroom", t.ItemsNotInClassroom),
            ("DataHealth.Hint.NoRecentSuccess", t.NoRecentSuccess),
            ("DataHealth.Hint.NoSuccessYet", t.NoSuccessYet),
            ("DataHealth.Hint.CourseListEmpty", t.CourseListEmpty),
            ("DataHealth.Hint.CoursesNotVisible", t.CoursesNotVisible),
            ("DataHealth.Hint.UnknownCourseState", t.UnknownCourseState),
            ("DataHealth.Technical.CourseListEmpty.Dean", t.DeanCourseListEmptySince),
        ];
    }

    /// <summary>FR-008, VR-003, NFR-073: each new key exists in the file of each language, non-empty, with the agreed text.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void EachNewText_IsAnEntry_WithTheAgreedValue(string language)
    {
        var entries = Entries(language);
        foreach (var (key, value) in Expected(language))
        {
            Assert.True(entries.TryGetValue(key, out var actual), $"{language}: no {key}.");
            Assert.False(string.IsNullOrWhiteSpace(actual), $"{language}: {key} is empty.");
            Assert.Equal(value, actual!.Replace('’', '\'').Replace('ʼ', '\''));
        }
    }

    /// <summary>FR-004.2, FR-001.1: the Admin's X4 title and the banner keys stay as they were.</summary>
    [Fact]
    public void TheAdminsX4Title_AndTheBannerKeys_AreUnchanged()
    {
        Assert.Equal("Google не повертає жодного курсу з {0} — перевірте роль технічного акаунта", Entries("uk")["DataHealth.Technical.CourseListEmpty"]);
        Assert.Equal("Google has returned no course since {0} — check the technical account's role", Entries("en")["DataHealth.Technical.CourseListEmpty"].Replace('’', '\''));
        Assert.Equal("{0}, {1}", Entries("uk")["DataHealth.Banner"]);
        Assert.Equal("{0}, {1}", Entries("en")["DataHealth.Banner"]);
    }

    /// <summary>NFR-073: no installation view holds a new hint text as a literal.</summary>
    [Fact]
    public void NoView_HoldsAHintTextAsALiteral()
    {
        var views = Directory.EnumerateFiles(
            Path.Combine(StaticFiles.RepositoryRoot(), "src", "ClassroomAgent.Web", "Views"), "*.cshtml", SearchOption.AllDirectories);
        foreach (var view in views)
        {
            var source = File.ReadAllText(view);
            foreach (var hint in DataHealthHintTexts.Uk.All.Concat(DataHealthHintTexts.En.All))
            {
                Assert.DoesNotContain(hint[..40], source, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>FR-001.4, SEC-004: the installation stylesheet keeps each banner link on one line, by a class, not inline.</summary>
    [Fact]
    public void TheStylesheet_KeepsEachBannerLinkOnOneLine()
    {
        var css = Styling.ReadRequired(Styling.FileOf(Styling.Installation, Styling.SiteCssPath));

        Assert.Contains(
            Styling.Declarations(css),
            d => Styling.ClassesOf(d.Selector).Contains(BannerPartClass)
                 && d.Property == "white-space"
                 && d.Value.Trim() == "nowrap");
    }

    /// <summary>FR-009.1, SEC-004: the installation stylesheet styles the hint class.</summary>
    [Fact]
    public void TheStylesheet_StylesTheHintClass()
    {
        var css = Styling.ReadRequired(Styling.FileOf(Styling.Installation, Styling.SiteCssPath));

        Assert.Contains(Styling.Declarations(css), d => Styling.ClassesOf(d.Selector).Contains(HintClass));
    }
}
