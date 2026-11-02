using System.Text;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-068 test support: the files the Story adds to each host (spec FR-001, FR-007), a small stylesheet reader that
/// lists every declaration with its selector (AC-004), the class names a host's views can use, and the stylesheets a
/// page's <c>&lt;head&gt;</c> links (AC-001).
/// </summary>
public static partial class Styling
{
    public const string Installation = "ClassroomAgent.Web";

    public const string ControlPlane = "ClassroomAgent.ControlPlane";

    /// <summary>FR-002.1: the library stylesheet, linked first.</summary>
    public const string BootstrapPath = "/lib/bootstrap/css/bootstrap.min.css";

    /// <summary>FR-002.1: the host's own stylesheet, linked second.</summary>
    public const string SiteCssPath = "/css/site.css";

    /// <summary>FR-001.1: the licence and the version note, next to the stylesheet.</summary>
    public const string LicensePath = "/lib/bootstrap/LICENSE.txt";

    public const string VersionPath = "/lib/bootstrap/VERSION.txt";

    /// <summary>FR-001.1: the exact content of each host's <c>wwwroot/lib/bootstrap/</c>.</summary>
    public static readonly IReadOnlyList<string> LibraryFiles = ["LICENSE.txt", "VERSION.txt", "css/bootstrap.min.css"];

    public static string WebRoot(string project) => Path.Combine(StaticFiles.RepositoryRoot(), "src", project, "wwwroot");

    /// <summary>The file on disk behind a root-relative URL path of a host.</summary>
    public static string FileOf(string project, string urlPath) =>
        Path.Combine(WebRoot(project), urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    public static string LibraryFolder(string project) => Path.Combine(WebRoot(project), "lib", "bootstrap");

    public static string ProjectFile(string project, string relative) =>
        Path.Combine(StaticFiles.RepositoryRoot(), "src", project, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Reads a host file the Story must have added, failing with a readable message while it is missing.</summary>
    public static string ReadRequired(string path)
    {
        Assert.True(File.Exists(path), $"Missing file: {Path.GetRelativePath(StaticFiles.RepositoryRoot(), path)}");
        return File.ReadAllText(path);
    }

    // ---- Stylesheet reading -------------------------------------------------------------------------------------

    /// <summary>
    /// Every declaration of every style rule, including rules nested in <c>@media</c>, <c>@supports</c>,
    /// <c>@container</c> and <c>@layer</c>; other at-rules (<c>@keyframes</c>, <c>@font-face</c>, …) are skipped.
    /// A rule with a selector list yields one entry per selector. Comments are removed first; quoted strings and
    /// parentheses (a <c>url(…)</c>, <c>:not(…)</c>) are never split.
    /// </summary>
    public static IReadOnlyList<CssDeclaration> Declarations(string css)
    {
        var result = new List<CssDeclaration>();
        ReadBlock(CssComment().Replace(css, " "), result);
        return result;
    }

    /// <summary>
    /// The comparable form of one selector: whitespace collapsed, no space around the combinators <c>&gt; + ~</c>,
    /// quotes removed (<c>[type="checkbox"]</c> equals <c>[type=checkbox]</c>).
    /// </summary>
    public static string NormalizeSelector(string selector)
    {
        var s = selector.Replace("\"", string.Empty, StringComparison.Ordinal).Replace("'", string.Empty, StringComparison.Ordinal);
        s = Whitespace().Replace(s.Trim(), " ");
        return CombinatorSpace().Replace(s, "$1");
    }

    /// <summary>The class names a selector requires, attribute-selector contents excluded.</summary>
    public static IReadOnlyList<string> ClassesOf(string selector) =>
        SelectorClass().Matches(AttributeSelector().Replace(selector, "[]"))
            .Select(m => m.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Whether a selector is scoped to Bootstrap's dark theme, which is never active (spec FR-007.4).</summary>
    public static bool IsDarkThemeScoped(string selector) =>
        selector.Contains("[data-bs-theme=dark]", StringComparison.Ordinal);

    /// <summary>The root-relative same-host path in a <c>url(…)</c> value, or null when the value has none.</summary>
    public static string? RootRelativeUrl(string value)
    {
        var match = RootRelativeUrlPattern().Match(value);
        return match.Success ? match.Groups["path"].Value : null;
    }

    /// <summary>Every root-relative <c>url(…)</c> path a stylesheet references, in order of first appearance.</summary>
    public static IReadOnlyList<string> RootRelativeUrls(string css) =>
        RootRelativeUrlPattern().Matches(CssComment().Replace(css, " "))
            .Select(m => m.Groups["path"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>The text with CSS comments removed.</summary>
    public static string WithoutComments(string css) => CssComment().Replace(css, " ");

    // ---- Views ---------------------------------------------------------------------------------------------------

    /// <summary>A view's text with Razor and HTML comments removed.</summary>
    public static string WithoutViewComments(string view) => HtmlComment().Replace(RazorComment().Replace(view, " "), " ");

    /// <summary>
    /// Every name-like token (letters, digits, <c>-</c>, <c>_</c>) in the host's views, comments removed — a
    /// deliberately wide superset of the class names the views can produce, literal or computed in Razor, so a
    /// component a view uses is never missed (AC-004).
    /// </summary>
    public static IReadOnlySet<string> ViewTokens(string project)
    {
        var root = StaticFiles.RepositoryRoot();
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var view in InlineCode.ViewsOf(project))
        {
            foreach (Match m in Token().Matches(WithoutViewComments(File.ReadAllText(Path.Combine(root, view)))))
            {
                tokens.Add(m.Value);
            }
        }

        return tokens;
    }

    // ---- Pages ---------------------------------------------------------------------------------------------------

    /// <summary>The <c>href</c> of every <c>&lt;link rel="stylesheet"&gt;</c> in a page's <c>&lt;head&gt;</c>, in order, query string removed.</summary>
    public static IReadOnlyList<string> StylesheetLinks(string html)
    {
        var head = HeadElement().Match(html);
        Assert.True(head.Success, "The page has no <head> element.");
        var links = new List<string>();
        foreach (Match link in LinkTag().Matches(head.Groups["body"].Value))
        {
            var rel = Attribute(link.Value, "rel");
            if (rel is null || !rel.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("stylesheet", StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var href = Attribute(link.Value, "href") ?? string.Empty;
            var query = href.IndexOf('?', StringComparison.Ordinal);
            links.Add(query < 0 ? href : href[..query]);
        }

        return links;
    }

    /// <summary>The value of one attribute of a tag, or null.</summary>
    public static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, $@"\s{Regex.Escape(name)}\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)'|(?<v>[^\s>]+))", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["v"].Value : null;
    }

    /// <summary>The value of a response header, every occurrence joined; null when absent.</summary>
    public static string? HeaderOf(PageResponse response, string name)
    {
        var values = response.Headers.Where(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).ToList();
        return values.Count == 0 ? null : string.Join(", ", values);
    }

    // ---- Reader internals ----------------------------------------------------------------------------------------

    private static readonly string[] NestingAtRules = ["@media", "@supports", "@container", "@layer"];

    private static void ReadBlock(string text, List<CssDeclaration> result)
    {
        var position = 0;
        while (position < text.Length)
        {
            var stop = IndexOfTopLevel(text, position, ['{', ';', '}']);
            if (stop < 0)
            {
                return;
            }

            if (text[stop] != '{')
            {
                position = stop + 1;
                continue;
            }

            var prelude = text[position..stop].Trim();
            var close = MatchingBrace(text, stop);
            var body = text[(stop + 1)..close];
            position = close + 1;

            if (prelude.StartsWith('@'))
            {
                if (NestingAtRules.Any(a => prelude.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
                {
                    ReadBlock(body, result);
                }

                continue;
            }

            var selectors = SplitTopLevel(prelude, ',').Select(NormalizeSelector).Where(s => s.Length > 0).ToList();
            foreach (var declaration in SplitTopLevel(body, ';'))
            {
                var colon = declaration.IndexOf(':', StringComparison.Ordinal);
                if (colon <= 0)
                {
                    continue;
                }

                var property = declaration[..colon].Trim().ToLowerInvariant();
                var value = declaration[(colon + 1)..].Trim();
                foreach (var selector in selectors)
                {
                    result.Add(new CssDeclaration(selector, property, value));
                }
            }
        }
    }

    private static int IndexOfTopLevel(string text, int start, char[] targets)
    {
        char? quote = null;
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (quote is { } q)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == q)
                {
                    quote = null;
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')' && depth > 0)
            {
                depth--;
            }
            else if (depth == 0 && targets.Contains(c))
            {
                return i;
            }
        }

        return -1;
    }

    private static int MatchingBrace(string text, int open)
    {
        var depth = 0;
        var position = open;
        while (true)
        {
            var next = IndexOfTopLevel(text, position, ['{', '}']);
            Assert.True(next >= 0, "Unbalanced braces in a stylesheet.");
            depth += text[next] == '{' ? 1 : -1;
            if (depth == 0)
            {
                return next;
            }

            position = next + 1;
        }
    }

    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var position = 0;
        while (position <= text.Length)
        {
            var next = IndexOfTopLevel(text, position, [separator]);
            var end = next < 0 ? text.Length : next;
            current.Append(text, position, end - position);
            parts.Add(current.ToString().Trim());
            current.Clear();
            if (next < 0)
            {
                break;
            }

            position = next + 1;
        }

        return parts.Where(p => p.Length > 0).ToList();
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CssComment();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\s*([>+~])\s*")]
    private static partial Regex CombinatorSpace();

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex AttributeSelector();

    [GeneratedRegex(@"\.(?<name>-?[_a-zA-Z][_a-zA-Z0-9-]*)")]
    private static partial Regex SelectorClass();

    [GeneratedRegex(@"url\(\s*[""']?(?<path>/[^/""')\s][^""')\s]*)[""']?\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex RootRelativeUrlPattern();

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorComment();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"[A-Za-z0-9_-]+")]
    private static partial Regex Token();

    [GeneratedRegex(@"<head\b[^>]*>(?<body>.*?)</head\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeadElement();

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTag();
}
