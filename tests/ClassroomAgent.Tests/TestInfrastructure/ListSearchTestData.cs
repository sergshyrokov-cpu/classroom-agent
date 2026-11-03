using System.Net;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-070: the search hooks of the three lists (openapi <c>x-markup</c>), translation keys and texts of spec FR-003,
/// FR-004, FR-006, FR-009, URLs written out literally in the openapi's <c>query-order</c>, and the refused texts of
/// VR-001. Every text the tests send is invented (TC-4).
/// </summary>
public static partial class ListSearchTestData
{
    /// <summary>What a refused or searched text carries, so a test can look for it anywhere.</summary>
    public const string Needle = "zzneedle";

    public const string CourseField = "course-list-search";

    public const string TeacherField = "teacher-list-search";

    public const string MeetField = "meet-codes-search";

    public const string MeetForm = "meet-codes-search-form";

    public const string MeetEmpty = "meet-codes-empty";

    public const string MeetLists = "meet-codes-lists";

    public const string MeetPaging = "meet-codes-pagination";

    public const string ChoiceForm = "meet-code-choice-form";

    public const string ChoiceSubmit = "meet-code-choice-submit";

    public const string ReturnQ = "returnQ";

    /// <summary>Api-design D-11, the existing keys VR-001 reuses and the hint's key (D-10).</summary>
    public static class Keys
    {
        public const string CoursesSearch = "Courses.Form.Search";

        public const string TeachersSearch = "Teachers.Form.Search";

        public const string MeetCodesSearch = "MeetCodes.Form.Search";

        public const string CoursesNoMatch = "Courses.Empty.NoMatch";

        public const string TeachersNoMatch = "Teachers.Empty.NoMatch";

        public const string MeetCodesNoMatch = "MeetCodes.Empty.NoMatch";

        public const string TeachersEmpty = "Teachers.Empty";

        public const string PickerLabel = "Picker.Narrow.Label";

        public const string PickerNone = "Picker.Narrow.NothingFound";

        public const string Hint = "PageSearchHint.ListField";

        public const string SearchRepeated = "Courses.Validation.SearchRepeated";

        public const string SearchTooLong = "Courses.Validation.SearchTooLong";

        public const string SearchControl = "Courses.Validation.SearchControlCharacter";

        public const string TeachersTotal = "Teachers.Paging.Total";

        public const string ColumnOrganizers = "MeetCodes.Column.Organizers";

        public const string QueryInvalid = "MeetCodes.Message.QueryInvalid";

        public static string MeetEmptyOf(string list) => list switch
        {
            MeetCodesHostExtensions.Lists.Unassigned => "MeetCodes.Empty.Unassigned",
            MeetCodesHostExtensions.Lists.Linked => "MeetCodes.Empty.Linked",
            MeetCodesHostExtensions.Lists.NotACourse => "MeetCodes.Empty.NotACourse",
            _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
        };

        public static string MeetListName(string list) => list switch
        {
            MeetCodesHostExtensions.Lists.Unassigned => "MeetCodes.List.Unassigned",
            MeetCodesHostExtensions.Lists.Linked => "MeetCodes.List.Linked",
            MeetCodesHostExtensions.Lists.NotACourse => "MeetCodes.List.NotACourse",
            _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
        };
    }

    /// <summary>Spec VR-001: the raw query fragment of each refusal, with the key of its message; each carries <see cref="Needle"/>.</summary>
    public static TheoryData<string, string> RefusedTexts => new()
    {
        { "q=" + Needle + "&q=" + Needle + "2", Keys.SearchRepeated },
        { "q=" + Needle + new string('я', 93), Keys.SearchTooLong },
        { "q=" + Needle + "%07", Keys.SearchControl },
    };

    /// <summary>The same three, for a <c>returnQ</c> posted in a form (FR-005.5); empty means no field.</summary>
    public static TheoryData<string> RefusedReturnTexts => new(Needle + new string('я', 93), Needle + "\u0007");

    /// <summary>Openapi <c>query-order</c>: teachers are <c>status</c>, <c>q</c>, <c>page</c>; defaults are omitted (US-022).</summary>
    public static string TeachersUrl(string? status = null, string? q = null, int? page = null) =>
        Join(TeacherListTestData.Path, status is null ? null : "status=" + status, QueryOf("q", q), page is null ? null : "page=" + page);

    /// <summary>Openapi <c>query-order</c>: <c>list</c>, <c>q</c>, <c>page</c>, <c>size</c>.</summary>
    public static string MeetUrl(string? list = null, string? q = null, int? page = null, int? size = null) =>
        Join(
            MeetCodesHostExtensions.Path,
            list is null ? null : "list=" + list,
            QueryOf("q", q),
            page is null ? null : "page=" + page,
            size is null ? null : "size=" + size);

    /// <summary>Openapi <c>query-order</c>: <c>returnPage</c>, <c>returnQ</c>.</summary>
    public static string ChoiceUrl(string code, int returnPage, string? returnQ) =>
        Join(
            MeetCodesHostExtensions.Path + "/" + Uri.EscapeDataString(code) + "/course-choice",
            "returnPage=" + returnPage,
            QueryOf(ReturnQ, returnQ));

    public static string? QueryOf(string name, string? value) =>
        value is null ? null : name + "=" + Uri.EscapeDataString(value);

    private static string Join(string path, params string?[] parts)
    {
        var present = parts.Where(p => p is not null).ToList();
        return present.Count == 0 ? path : path + "?" + string.Join('&', present);
    }

    /// <summary>The opening tag of the element with that id; null when there is none.</summary>
    public static string? TagOf(string html, string id)
    {
        var match = Regex.Match(html, @"<[a-z][a-z0-9]*\b[^>]*\bid=""" + Regex.Escape(id) + @"""[^>]*>", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    /// <summary>The decoded attribute of an opening tag; null when absent.</summary>
    public static string? AttributeOf(string? tag, string name)
    {
        if (tag is null)
        {
            return null;
        }

        var match = Regex.Match(tag, @"\s" + Regex.Escape(name) + @"\s*=\s*""(?<v>[^""]*)""", RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["v"].Value) : null;
    }

    /// <summary>The value the search field shows; null when the field or the attribute is absent, or the value is empty.</summary>
    public static string? FieldValue(string html, string id) =>
        AttributeOf(TagOf(html, id), "value") is { Length: > 0 } value ? value : null;

    /// <summary>The field's label text, by <c>for</c>; null when there is none.</summary>
    public static string? LabelOf(string html, string id)
    {
        var match = Regex.Match(html, @"<label\b[^>]*\bfor=""" + Regex.Escape(id) + @"""[^>]*>(?<t>[^<]*)</label>", RegexOptions.IgnoreCase);
        return match.Success ? Spaces().Replace(WebUtility.HtmlDecode(match.Groups["t"].Value), " ").Trim() : null;
    }

    /// <summary>The markup of the element with that id, to its first closing tag of the same name.</summary>
    public static string? ElementOf(string html, string id)
    {
        var open = Regex.Match(html, @"<(?<n>[a-z][a-z0-9]*)\b[^>]*\bid=""" + Regex.Escape(id) + @"""[^>]*>", RegexOptions.IgnoreCase);
        if (!open.Success)
        {
            return null;
        }

        var close = html.IndexOf("</" + open.Groups["n"].Value, open.Index, StringComparison.OrdinalIgnoreCase);
        return close < 0 ? html[open.Index..] : html[open.Index..close];
    }

    /// <summary>The header cells of the Meet code table, in order, as text.</summary>
    public static IReadOnlyList<string> TableHeaders(string html) =>
        HeaderCell().Matches(ElementOf(html, "meet-codes-table") ?? string.Empty)
            .Select(m => Spaces().Replace(WebUtility.HtmlDecode(m.Groups["t"].Value), " ").Trim())
            .ToList();

    /// <summary>The Meet codes listed, in order (each row's <c>&lt;th scope="row"&gt;</c>).</summary>
    public static IReadOnlyList<string> ListedCodes(string html) =>
        RowCode().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups["c"].Value).Trim()).ToList();

    /// <summary>The cells of the row of a code, as text, after the code itself; empty when it is not listed.</summary>
    public static IReadOnlyList<string> CellsOfCode(string html, string code)
    {
        var match = Regex.Match(
            html,
            @"<tr[^>]*>\s*<th scope=""row"">\s*" + Regex.Escape(code) + @"\s*</th>(?<cells>.*?)</tr>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return !match.Success
            ? []
            : Cell().Matches(match.Groups["cells"].Value)
                .Select(m => Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(m.Groups["t"].Value, " ")), " ").Trim())
                .ToList();
    }

    /// <summary>The hidden field values of the forms on a page, by field name; a form may repeat one.</summary>
    public static IReadOnlyList<string> HiddenValues(string html, string name) =>
        Regex.Matches(html, @"<input\b[^>]*>", RegexOptions.IgnoreCase)
            .Where(m => string.Equals(AttributeOf(m.Value, "name"), name, StringComparison.Ordinal))
            .Select(m => AttributeOf(m.Value, "value") ?? string.Empty)
            .ToList();

    /// <summary>The number of forms on a page posting to a path that ends with that suffix.</summary>
    public static int FormCount(string html, string suffix) =>
        Regex.Count(html, @"<form\b[^>]*\baction=""[^""]*" + Regex.Escape(suffix) + @"""", RegexOptions.IgnoreCase);

    /// <summary>The list text "name (n)" as the nav of the page shows it.</summary>
    public static string Counter(InstallationTestHost host, string list, int count, string language = "uk") =>
        host.Text(Keys.MeetListName(list), language) + " (" + count + ")";

    /// <summary>The nav of the lists as text.</summary>
    public static string ListsNavText(string html) =>
        MeetCodesHostExtensions.VisibleText(ElementOf(html, MeetLists) ?? string.Empty);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"<th scope=""col""[^>]*>(?<t>[^<]*)</th>", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderCell();

    [GeneratedRegex(@"<th scope=""row"">(?<c>[^<]*)</th>", RegexOptions.IgnoreCase)]
    private static partial Regex RowCode();

    [GeneratedRegex(@"<td[^>]*>(?<t>.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Cell();
}
