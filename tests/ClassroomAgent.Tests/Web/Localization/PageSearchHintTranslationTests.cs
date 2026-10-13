using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClassroomAgent.Tests.TestInfrastructure;
using Hint = ClassroomAgent.Tests.TestInfrastructure.PageSearchHintTestData;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-046 AC-006, spec FR-008, S-03, I-3: the three hint texts are entries of the installation's translation files in
/// Ukrainian and English, no view holds them as a literal or renders the hint raw, and the installation stylesheet
/// styles the hook class.
/// </summary>
public sealed class PageSearchHintTranslationTests
{
    private static string Source(params string[] parts) =>
        Path.Combine([StaticFiles.RepositoryRoot(), "src", .. parts]);

    private static IReadOnlyList<string> ResourceValues(string language) =>
        XDocument.Load(Source("ClassroomAgent.Application", "Localization", $"SharedResource.{language}.resx"))
            .Root!
            .Elements("data")
            .Select(data => (string?)data.Element("value") ?? string.Empty)
            .ToList();

    private static IEnumerable<string> Views() =>
        new[] { "ClassroomAgent.Web", "ClassroomAgent.ControlPlane" }
            .Select(project => Source(project, "Views"))
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories));

    /// <summary>AC-006, FR-008: each text of spec FR-003 is a value of the translation file of its language.</summary>
    [Fact]
    public void EachHintText_IsAnEntryOfTheTranslationFileOfItsLanguage()
    {
        foreach (var (variant, language) in Hint.AllTexts)
        {
            Assert.Contains(Hint.Expected(variant, language), ResourceValues(language));
        }
    }

    /// <summary>
    /// AC-006, FR-008: no view of the installation or the Control Plane holds a fragment of the texts as a literal.
    /// </summary>
    [Fact]
    public void NoView_HoldsTheHintTextAsALiteral()
    {
        var views = Views().ToList();
        Assert.NotEmpty(views);

        foreach (var view in views)
        {
            var content = File.ReadAllText(view);
            foreach (var fragment in Hint.LiteralFragments)
            {
                Assert.False(
                    content.Contains(fragment, StringComparison.Ordinal),
                    $"{Path.GetFileName(view)} holds the literal '{fragment}'.");
            }
        }
    }

    /// <summary>S-03: wherever the hook is rendered, it is not rendered through <c>Html.Raw</c>.</summary>
    [Fact]
    public void TheHint_IsNeverRenderedRaw()
    {
        foreach (var view in Views())
        {
            var content = File.ReadAllText(view);
            if (!content.Contains(Hint.Hook, StringComparison.Ordinal))
            {
                continue;
            }

            Assert.False(
                content.Contains("Html.Raw", StringComparison.Ordinal),
                $"{Path.GetFileName(view)} renders the hint and uses Html.Raw.");
        }
    }

    /// <summary>I-3, FR-002: the installation stylesheet has a rule for the hook class that sets its size.</summary>
    [Fact]
    public void TheStylesheet_StylesTheHintClass()
    {
        var css = File.ReadAllText(Source("ClassroomAgent.Web", "wwwroot", "css", "site.css"));

        var rule = Regex.Match(css, @"\.page-search-hint\b[^{]*\{(?<body>[^}]*)\}");
        Assert.True(rule.Success, "site.css has no rule for .page-search-hint.");
        Assert.Contains("font-size", rule.Groups["body"].Value, StringComparison.Ordinal);
    }
}
