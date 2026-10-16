using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The US-050 view scan (spec FR-006, VR-003): markup a strict Content-Security-Policy would refuse to run — a
/// <c>&lt;script&gt;</c> element with a body, an <c>on…=</c> event-handler attribute, a <c>style</c> attribute, and
/// (SPEC_REVIEW m-1) a <c>&lt;style&gt;</c> element with a body. Razor and HTML comments are not markup and are
/// removed first. Matching is case-insensitive.
/// </summary>
public static partial class InlineCode
{
    /// <summary>The violations found in one view's text, each with a short excerpt; empty when the view complies.</summary>
    public static IReadOnlyList<string> Violations(string view)
    {
        var text = HtmlComment().Replace(RazorComment().Replace(view, " "), " ");
        var violations = new List<string>();

        foreach (Match m in ScriptElement().Matches(text))
        {
            if (!string.IsNullOrWhiteSpace(m.Groups["body"].Value))
            {
                violations.Add($"<script> with a body: {Excerpt(m.Value)}");
            }
        }

        foreach (Match m in StyleElement().Matches(text))
        {
            if (!string.IsNullOrWhiteSpace(m.Groups["body"].Value))
            {
                violations.Add($"<style> with a body: {Excerpt(m.Value)}");
            }
        }

        foreach (Match m in EventHandlerAttribute().Matches(text))
        {
            violations.Add($"event-handler attribute: {Excerpt(m.Value)}");
        }

        foreach (Match m in StyleAttribute().Matches(text))
        {
            violations.Add($"style attribute: {Excerpt(m.Value)}");
        }

        return violations;
    }

    /// <summary>Every <c>.cshtml</c> of a host project, as repository-relative paths.</summary>
    public static IReadOnlyList<string> ViewsOf(string project)
    {
        var root = StaticFiles.RepositoryRoot();
        var projectDirectory = Path.Combine(root, "src", project);
        return Directory.EnumerateFiles(projectDirectory, "*.cshtml", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(Path.GetRelativePath(projectDirectory, f)))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Every script file of a host's <c>wwwroot/js</c>, as repository-relative paths.</summary>
    public static IReadOnlyList<string> ScriptsOf(string project)
    {
        var root = StaticFiles.RepositoryRoot();
        var scripts = Path.Combine(root, "src", project, "wwwroot", "js");
        return Directory.EnumerateFiles(scripts, "*.js", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Script constructs a policy without <c>'unsafe-eval'</c> and <c>'unsafe-inline'</c> refuses (spec FR-004,
    /// FR-007): evaluating a string as code, and setting a <c>style</c> or event-handler attribute. Setting a style
    /// property through the CSSOM stays allowed (FR-007). Line and block comments are removed first.
    /// </summary>
    public static IReadOnlyList<string> ScriptViolations(string script)
    {
        var text = LineComment().Replace(BlockComment().Replace(script, " "), " ");
        return UnsafeScriptConstruct().Matches(text).Select(m => $"refused by the policy: {Excerpt(m.Value)}").ToList();
    }

    private static bool IsBuildOutput(string relativePath) =>
        relativePath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || relativePath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Excerpt(string value)
    {
        var flat = Regex.Replace(value, @"\s+", " ");
        return flat.Length <= 80 ? flat : flat[..80] + "…";
    }

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorComment();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"<script\b[^>]*>(?<body>.*?)</script\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptElement();

    [GeneratedRegex(@"<style\b[^>]*>(?<body>.*?)</style\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StyleElement();

    [GeneratedRegex(@"(?<=\s)on[a-z]+\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex EventHandlerAttribute();

    [GeneratedRegex(@"(?<=\s)style\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttribute();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"//[^\r\n]*")]
    private static partial Regex LineComment();

    [GeneratedRegex(
        @"\beval\s*\(|\bnew\s+Function\s*\(|\bset(?:Timeout|Interval)\s*\(\s*[""'`]|setAttribute\s*\(\s*[""'`](?:style|on[a-z]+)[""'`]",
        RegexOptions.IgnoreCase)]
    private static partial Regex UnsafeScriptConstruct();
}
