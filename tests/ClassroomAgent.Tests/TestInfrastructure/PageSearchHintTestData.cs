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

        public const string TeacherList = "teacher-list";

        public const string MeetCodes = "meet-codes";
    }

    /// <summary>
    /// The list-field template of key <c>PageSearchHint.ListField</c>, Ukrainian (US-071 FR-011); <c>{0}</c> is the list's own
    /// search field label, <c>{1}</c> the header field's label.
    /// </summary>
    public const string ListFieldUk =
        "У цьому списку — поле «{0}» нижче (Ctrl+F бачить лише показані рядки), у всій програмі — поле «{1}» угорі";

    /// <summary>The list-field template, English.</summary>
    public const string ListFieldEn =
        "In this list: the \"{0}\" field below (Ctrl+F sees only the rows shown); across the program: the \"{1}\" field at the top";

    /// <summary>The ordinary template of key <c>PageSearchHint.Ordinary</c>, Ukrainian (US-071 FR-011); <c>{0}</c> is the header field's label.</summary>
    public const string OrdinaryUk =
        "На цій сторінці — Ctrl+F (на Mac — ⌘F; на телефоні — меню браузера → «Знайти на сторінці»), у всій програмі — поле «{0}» угорі";

    /// <summary>The ordinary template, English.</summary>
    public const string OrdinaryEn =
        "On this page: Ctrl+F (⌘F on Mac; on a phone — browser menu → \"Find in page\"); across the program: the \"{0}\" field at the top";

    /// <summary>The final texts of US-071 FR-011, by variant and language; the header field's label is «Пошук» / "Search".</summary>
    public static string Expected(string variant, string language) => (variant, language) switch
    {
        (Variants.Ordinary, "uk") =>
            "На цій сторінці — Ctrl+F (на Mac — ⌘F; на телефоні — меню браузера → «Знайти на сторінці»), у всій програмі — поле «Пошук» угорі",
        (Variants.Ordinary, "en") =>
            "On this page: Ctrl+F (⌘F on Mac; on a phone — browser menu → \"Find in page\"); across the program: the \"Search\" field at the top",
        (Variants.CourseList, "uk") =>
            "У цьому списку — поле «Пошук за назвою або розділом» нижче (Ctrl+F бачить лише показані рядки), у всій програмі — поле «Пошук» угорі",
        (Variants.CourseList, "en") =>
            "In this list: the \"Search by name or section\" field below (Ctrl+F sees only the rows shown); across the program: the \"Search\" field at the top",
        (Variants.TeacherList, "uk") =>
            "У цьому списку — поле «Пошук за ім'ям або email» нижче (Ctrl+F бачить лише показані рядки), у всій програмі — поле «Пошук» угорі",
        (Variants.TeacherList, "en") =>
            "In this list: the \"Search by name or email\" field below (Ctrl+F sees only the rows shown); across the program: the \"Search\" field at the top",
        (Variants.MeetCodes, "uk") =>
            "У цьому списку — поле «Пошук за кодом, організатором або курсом» нижче (Ctrl+F бачить лише показані рядки), у всій програмі — поле «Пошук» угорі",
        (Variants.MeetCodes, "en") =>
            "In this list: the \"Search by code, organizer or course\" field below (Ctrl+F sees only the rows shown); across the program: the \"Search\" field at the top",
        _ => throw new ArgumentOutOfRangeException(nameof(variant), $"{variant}/{language}"),
    };

    /// <summary>
    /// The (variant, language) pairs that are whole entries of the translation files. Since US-071 every variant is a template
    /// (<see cref="OrdinaryUk"/>, <see cref="ListFieldUk"/> and their English forms), checked apart, so none is a whole entry.
    /// </summary>
    public static IReadOnlyList<(string Variant, string Language)> AllTexts { get; } = [];

    /// <summary>Fragments of the texts that a view holding a literal would contain (FR-008).</summary>
    public static IReadOnlyList<string> LiteralFragments { get; } =
        ["Ctrl+F", "⌘F", "Find in page", "Знайти на сторінці", "across the program", "у всій програмі"];

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
    /// (1 = the layout title, the only one since US-069 FR-002), with leading white space and HTML
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
