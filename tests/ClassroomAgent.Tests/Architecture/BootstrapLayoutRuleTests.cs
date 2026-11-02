using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-068 AC-001 (a), AC-002 (no script), AC-008 and the component guards of spec FR-005.4, FR-005.5, FR-006.3 and
/// FR-009.1, read from the source files of both hosts: one layout links the library and then the site stylesheet,
/// every view renders through it, the theme is light only, and neither the views nor <c>site.css</c> bring in what the
/// Specification keeps out.
/// </summary>
public sealed partial class BootstrapLayoutRuleTests
{
    public static TheoryData<string> Hosts => new(Styling.Installation, Styling.ControlPlane);

    /// <summary>AC-001 (a), FR-002.1: the layout links the library first, then <c>site.css</c>, and no other stylesheet.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheLayout_LinksTheLibraryThenTheSiteStylesheet_AndNothingElse(string host)
    {
        var layout = Styling.ReadRequired(Styling.ProjectFile(host, "Views/Shared/_Layout.cshtml"));

        Assert.Equal([Styling.BootstrapPath, Styling.SiteCssPath], Styling.StylesheetLinks(layout));
    }

    /// <summary>AC-001 (a), FR-002.2: <c>_ViewStart.cshtml</c> sets the shared layout.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheViewStart_SetsTheSharedLayout(string host)
    {
        var viewStart = Styling.ReadRequired(Styling.ProjectFile(host, "Views/_ViewStart.cshtml"));

        Assert.Matches(@"Layout\s*=\s*""~/Views/Shared/_Layout\.cshtml""\s*;", Styling.WithoutViewComments(viewStart));
    }

    /// <summary>AC-001 (a), FR-002.2: apart from <c>_ViewStart.cshtml</c>, no view assigns <c>Layout</c>.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void NoView_AssignsItsOwnLayout(string host)
    {
        var root = StaticFiles.RepositoryRoot();
        var views = InlineCode.ViewsOf(host).Where(v => !v.EndsWith("/_ViewStart.cshtml", StringComparison.Ordinal)).ToList();
        Assert.True(views.Count > 10, $"Only {views.Count} views found under src/{host}.");

        var offenders = views
            .Where(v => LayoutAssignment().IsMatch(Styling.WithoutViewComments(File.ReadAllText(Path.Combine(root, v)))))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>AC-002, FR-001.4, FR-002.1: no layout or view references a Bootstrap script.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void NoView_ReferencesABootstrapScript(string host)
    {
        var root = StaticFiles.RepositoryRoot();

        var offenders = InlineCode.ViewsOf(host)
            .Where(v => BootstrapScript().IsMatch(File.ReadAllText(Path.Combine(root, v))))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>AC-008, FR-003.2: no layout or view sets <c>data-bs-theme</c>.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void NoView_SetsABootstrapTheme(string host)
    {
        var root = StaticFiles.RepositoryRoot();

        var offenders = InlineCode.ViewsOf(host)
            .Where(v => File.ReadAllText(Path.Combine(root, v)).Contains("data-bs-theme", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>AC-008, FR-003.2: <c>site.css</c> asks for no dark colour scheme and has no <c>prefers-color-scheme</c> rule.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheSiteStylesheet_KeepsTheLightThemeOnly(string host)
    {
        var css = Styling.WithoutComments(Styling.ReadRequired(Styling.FileOf(host, Styling.SiteCssPath)));

        Assert.DoesNotContain("prefers-color-scheme", css, StringComparison.OrdinalIgnoreCase);
        var darkSchemes = Styling.Declarations(css)
            .Where(d => d.Property == "color-scheme" && d.Value.Contains("dark", StringComparison.OrdinalIgnoreCase))
            .Select(d => $"{d.Selector} {{ color-scheme: {d.Value} }}")
            .ToList();
        Assert.Empty(darkSchemes);
    }

    /// <summary>
    /// FR-005.4, FR-005.5 (guard for AC-004 and AC-007): no view uses Bootstrap's validation-state classes or a
    /// component that needs its scripts or draws an embedded image beyond FR-007.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void NoView_UsesAComponentTheSpecificationKeepsOut(string host)
    {
        string[] excluded =
        [
            "is-invalid", "is-valid", "was-validated", "btn-close", "alert-dismissible", "form-switch", "navbar",
            "navbar-toggler", "dropdown", "dropdown-menu", "modal", "accordion", "tooltip", "popover", "carousel",
            "spinner-border", "spinner-grow", "data-bs-toggle", "data-bs-dismiss",
        ];

        var used = Styling.ViewTokens(host).Intersect(excluded, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.Empty(used);
    }

    /// <summary>
    /// FR-009.1, FR-006.3 (guard for AC-006): <c>site.css</c> produces no words (<c>content:</c> with letters), changes no
    /// letters (<c>text-transform</c>) and adds no animation or blinking.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheSiteStylesheet_AddsNoTextAndNoAnimation(string host)
    {
        var declarations = Styling.Declarations(Styling.ReadRequired(Styling.FileOf(host, Styling.SiteCssPath)));

        var offenders = declarations
            .Where(d => (d.Property == "content" && ProducesWords(d.Value))
                || (d.Property == "text-transform" && !string.Equals(d.Value, "none", StringComparison.OrdinalIgnoreCase))
                || d.Property.StartsWith("animation", StringComparison.Ordinal)
                || d.Value.Contains("blink", StringComparison.OrdinalIgnoreCase))
            .Select(d => $"{d.Selector} {{ {d.Property}: {d.Value} }}")
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>The reader the AC-004 and AC-008 checks rely on: nesting, selector lists, strings and comments.</summary>
    [Fact]
    public void TheStylesheetReader_FindsDeclarationsInsideMediaRulesAndSelectorLists()
    {
        const string css = """
            /* .ignored{background:url("data:x")} */
            .a, .b > .c{color:red;background-image:url("data:image/svg+xml,%3csvg;x%3e")}
            @media (min-width: 576px){.d:not(.e)[type="checkbox"]{--bs-img:url('/img/x.svg')}}
            @keyframes spin{to{transform:rotate(1turn)}}
            """;

        var declarations = Styling.Declarations(css);

        Assert.DoesNotContain(declarations, d => d.Selector == ".ignored");
        Assert.Contains(new CssDeclaration(".b>.c", "color", "red"), declarations);
        Assert.Contains(new CssDeclaration(".a", "background-image", "url(\"data:image/svg+xml,%3csvg;x%3e\")"), declarations);
        var nested = Assert.Single(declarations, d => d.Property == "--bs-img");
        Assert.Equal(".d:not(.e)[type=checkbox]", nested.Selector);
        Assert.Equal("/img/x.svg", Styling.RootRelativeUrl(nested.Value));
        Assert.Equal(["d", "e"], Styling.ClassesOf(nested.Selector));
        Assert.DoesNotContain(declarations, d => d.Property == "transform");
        Assert.Null(Styling.RootRelativeUrl("url(//cdn.example.test/x.svg)"));
        Assert.Null(Styling.RootRelativeUrl("url(\"img/x.svg\")"));
    }

    /// <summary>The page reader the AC-001 checks rely on: only stylesheet links of the head, in order, without the query.</summary>
    [Fact]
    public void TheStylesheetLinkReader_ReadsTheHeadInOrder()
    {
        const string html = """
            <html><head><link rel="icon" href="/x.ico"><link href="/a.css?v=1" rel="stylesheet" />
            <link rel='stylesheet' href='/b.css'></head><body><link rel="stylesheet" href="/c.css"></body></html>
            """;

        Assert.Equal(["/a.css", "/b.css"], Styling.StylesheetLinks(html));
    }

    private static bool ProducesWords(string value)
    {
        var withoutEmptyStrings = value.Replace("\"\"", string.Empty, StringComparison.Ordinal).Replace("''", string.Empty, StringComparison.Ordinal).Trim();
        return withoutEmptyStrings is not ("none" or "normal" or "") && withoutEmptyStrings.Any(char.IsLetter);
    }

    [GeneratedRegex(@"\bLayout\s*=(?!=)")]
    private static partial Regex LayoutAssignment();

    [GeneratedRegex(@"bootstrap[^""'\s>]*\.js\b", RegexOptions.IgnoreCase)]
    private static partial Regex BootstrapScript();
}
