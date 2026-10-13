using System.Net;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-046: the page-search hint's markup hook (api-design §3) and its three texts, verbatim from spec FR-003
/// (<c>trebovaniya.md</c> §5, v91).
/// </summary>
public static partial class PageSearchHintTestData
{
    /// <summary>The hook's id and class: <c>&lt;p id="page-search-hint" class="page-search-hint" data-variant="…"&gt;</c>.</summary>
    public const string Hook = "page-search-hint";

    public static class Variants
    {
        public const string Ordinary = "ordinary";

        public const string CourseList = "course-list";

        public const string PagedList = "paged-list";
    }

    /// <summary>Spec FR-003, by variant and language; the outer «» of the specification are not part of the text.</summary>
    public static string Expected(string variant, string language) => (variant, language) switch
    {
        (Variants.Ordinary, "uk") =>
            "Пошук на сторінці: Ctrl+F (на Mac — ⌘F), на телефоні — меню браузера → «Знайти на сторінці».",
        (Variants.Ordinary, "en") =>
            "Search this page: Ctrl+F (⌘F on Mac); on a phone — browser menu → \"Find in page\".",
        (Variants.CourseList, "uk") =>
            "Курс шукайте в полі «Пошук за назвою або розділом» нижче — воно шукає в усьому списку; "
            + "Ctrl+F (на Mac — ⌘F) знаходить лише показані рядки.",
        (Variants.CourseList, "en") =>
            "Find a course in the \"Search by name or section\" field below — it searches the whole list; "
            + "Ctrl+F (⌘F on Mac) finds only the rows shown.",
        (Variants.PagedList, "uk") =>
            "Ctrl+F (на Mac — ⌘F; на телефоні — меню браузера → «Знайти на сторінці») шукає лише серед показаних "
            + "рядків; решта — на наступних сторінках списку (кнопки внизу).",
        (Variants.PagedList, "en") =>
            "Ctrl+F (⌘F on Mac; on a phone — browser menu → \"Find in page\") searches only the rows shown; "
            + "the rest are on the next pages of the list (buttons below).",
        _ => throw new ArgumentOutOfRangeException(nameof(variant), $"{variant}/{language}"),
    };

    /// <summary>Every (variant, language) pair, for the translation tests.</summary>
    public static IReadOnlyList<(string Variant, string Language)> AllTexts { get; } =
    [
        (Variants.Ordinary, "uk"), (Variants.Ordinary, "en"),
        (Variants.CourseList, "uk"), (Variants.CourseList, "en"),
        (Variants.PagedList, "uk"), (Variants.PagedList, "en"),
    ];

    /// <summary>Fragments of the texts that a view holding a literal would contain (FR-008).</summary>
    public static IReadOnlyList<string> LiteralFragments { get; } =
        ["Ctrl+F", "⌘F", "Find in page", "Знайти на сторінці", "Search this page", "Пошук на сторінці"];

    /// <summary>One rendered hook element.</summary>
    public sealed record Hint(int Index, string Tag, string? Variant, string InnerHtml)
    {
        /// <summary>The decoded text with white space collapsed.</summary>
        public string Text => WhiteSpace().Replace(WebUtility.HtmlDecode(InnerHtml), " ").Trim();
    }

    /// <summary>
    /// Every element carrying the hook as its id or among its classes. The element is a leaf (api-design §3), so the
    /// match runs to the first closing tag of the same name.
    /// </summary>
    public static IReadOnlyList<Hint> Hints(string html)
    {
        var hints = new List<Hint>();
        foreach (Match open in OpenTag().Matches(html))
        {
            var attributes = open.Groups["attributes"].Value;
            var id = Attribute(attributes, "id");
            var classes = (Attribute(attributes, "class") ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (id != Hook && !classes.Contains(Hook, StringComparer.Ordinal))
            {
                continue;
            }

            var name = open.Groups["name"].Value;
            var start = open.Index + open.Length;
            var close = html.IndexOf("</" + name, start, StringComparison.OrdinalIgnoreCase);
            var inner = close < 0 ? html[start..] : html[start..close];
            hints.Add(new Hint(open.Index, name.ToLowerInvariant(), Attribute(attributes, "data-variant"), inner));
        }

        return hints;
    }

    /// <summary>Whether the page holds the hook anywhere — as an id, a class or any other mention.</summary>
    public static bool HasAnyHint(string html) => html.Contains(Hook, StringComparison.Ordinal);

    /// <summary>
    /// The markup that follows the <paramref name="ordinal"/>-th <c>&lt;/h1&gt;</c> inside <c>&lt;main&gt;</c>
    /// (1 = the layout title; 2 = the view's own title on Meet codes, OD-002 a), with leading white space and HTML
    /// comments removed; null when there is no such title.
    /// </summary>
    public static string? AfterTitle(string html, int ordinal)
    {
        var main = html.IndexOf("<main", StringComparison.OrdinalIgnoreCase);
        if (main < 0)
        {
            return null;
        }

        var position = main;
        for (var i = 0; i < ordinal; i++)
        {
            var close = html.IndexOf("</h1>", position, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
            {
                return null;
            }

            position = close + "</h1>".Length;
        }

        return LeadingFiller().Replace(html[position..], string.Empty);
    }

    /// <summary>Whether the markup after the title starts with the hook element (api-design §3 placement).</summary>
    public static bool StartsWithHint(string? markupAfterTitle) =>
        markupAfterTitle is not null && HintFirst().IsMatch(markupAfterTitle);

    private static string? Attribute(string attributes, string name)
    {
        var match = Regex.Match(
            attributes,
            @"(?:^|\s)" + Regex.Escape(name) + @"\s*=\s*(""(?<v>[^""]*)""|'(?<v>[^']*)')",
            RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["v"].Value) : null;
    }

    [GeneratedRegex(@"<(?<name>[a-z][a-z0-9]*)\b(?<attributes>[^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex OpenTag();

    [GeneratedRegex(@"^(?:\s+|<!--.*?-->)*", RegexOptions.Singleline)]
    private static partial Regex LeadingFiller();

    [GeneratedRegex(@"^<p\b[^>]*\bid\s*=\s*""page-search-hint""", RegexOptions.IgnoreCase)]
    private static partial Regex HintFirst();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
