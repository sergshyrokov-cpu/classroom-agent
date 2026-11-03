using System.Net;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-071: what the tests and the future page must agree on: the path, the markup hooks and translation keys of openapi
/// <c>x-page-frame</c> and <c>x-markup-results-page</c>, and small readers of the rendered groups. Every value is invented (TC-4).
/// </summary>
public static partial class SearchTestData
{
    public const string Path = "/workspace/search";

    public const string HeaderForm = "header-search-form";

    public const string HeaderField = "header-search";

    public const string Warning = "search-history-warning";

    public const string Message = "search-message";

    public const string NothingFound = "search-nothing-found";

    public const string Reminder = "search-reminder";

    public const string Students = "search-group-students";

    public const string Teachers = "search-group-teachers";

    public const string Courses = "search-group-courses";

    public const string MeetCodes = "search-group-meet-codes";

    /// <summary>Openapi <c>x-translation-keys</c>.</summary>
    public static class Keys
    {
        public const string FieldLabel = "Search.Field.Label";

        public const string FieldSubmit = "Search.Field.Submit";

        public const string Title = "Search.Title";

        public const string TooShort = "Search.Message.TooShort";

        public const string NothingFound = "Search.Empty.NothingFound";

        public const string Reminder = "Search.Empty.Reminder";

        public const string Warning = "Search.Warning";

        public const string GroupStudents = "Search.Group.Students";

        public const string GroupTeachers = "Search.Group.Teachers";

        public const string GroupCourses = "Search.Group.Courses";

        public const string GroupMeetCodes = "Search.Group.MeetCodes";

        public const string LimitShown = "Search.Limit.Shown";

        public const string WholeList = "Search.Limit.WholeList";

        public const string TeacherCourses = "Search.Teacher.Courses";

        public static readonly string[] All =
        [
            FieldLabel, FieldSubmit, Title, TooShort, NothingFound, Reminder, Warning, GroupStudents, GroupTeachers, GroupCourses,
            GroupMeetCodes, LimitShown, WholeList, TeacherCourses,
        ];
    }

    public static string Url(string? q) => q is null ? Path : Path + "?q=" + Uri.EscapeDataString(q);

    /// <summary>The ids of the groups on the page, in page order.</summary>
    public static IReadOnlyList<string> GroupsInOrder(string html) =>
        GroupOpening().Matches(html).Select(m => m.Groups["id"].Value).ToList();

    /// <summary>The markup of a group; null when the group is hidden.</summary>
    public static string? Group(string html, string groupId) => ListSearchTestData.ElementOf(html, groupId);

    /// <summary>A group's markup, failing with a readable message while it is not rendered.</summary>
    public static string RequireGroup(string html, string groupId)
    {
        var group = Group(html, groupId);
        Assert.True(group is not null, $"The group {groupId} is not on the page.");
        return group;
    }

    /// <summary>Tags replaced by spaces, entities decoded, white space collapsed.</summary>
    public static string TextOf(string fragment) => DataMarksHostExtensions.Text(fragment);

    /// <summary>The text of a <c>&lt;p&gt;</c> with that id; null when there is none.</summary>
    public static string? ParagraphText(string html, string id) =>
        ListSearchTestData.ElementOf(html, id) is { } element ? TextOf(element) : null;

    /// <summary>The group's limit line (<c>search-group-limit</c>); null when there is none.</summary>
    public static string? LimitLine(string group)
    {
        var match = LimitParagraph().Match(group);
        return match.Success ? TextOf(match.Groups["t"].Value) : null;
    }

    /// <summary>The group's link to the whole list (<c>search-group-whole-list</c>); null when there is none.</summary>
    public static string? WholeListHref(string group)
    {
        var match = WholeListAnchor().Match(group);
        return match.Success ? ListSearchTestData.AttributeOf(match.Value, "href") : null;
    }

    /// <summary>Every link of a fragment, decoded, in order.</summary>
    public static IReadOnlyList<string> Hrefs(string fragment) =>
        Anchor().Matches(fragment).Select(m => ListSearchTestData.AttributeOf(m.Value, "href") ?? string.Empty).ToList();

    /// <summary>
    /// The text from the link with that address up to the next link or the end of the fragment: the link's own text and what
    /// follows it, so a mark belongs to the course before it. Fails while there is no such link.
    /// </summary>
    public static string TextFromLink(string fragment, string href)
    {
        var anchors = Anchor().Matches(fragment).ToList();
        var index = anchors.FindIndex(m => ListSearchTestData.AttributeOf(m.Value, "href") == href);
        Assert.True(index >= 0, $"No link to {href}.");
        var start = anchors[index].Index;
        var end = index + 1 < anchors.Count ? anchors[index + 1].Index : fragment.Length;
        return TextOf(fragment[start..end]);
    }

    /// <summary>The word of the mark as it stands in a piece of text: dated, or alone with no digit after it.</summary>
    public static bool HasAnyLeftMark(string text, DataMarksTexts texts) =>
        text.Contains(texts.Left, StringComparison.Ordinal);

    /// <summary>The value of the header field; null when the field is absent or has no value.</summary>
    public static string? HeaderValue(string html) => ListSearchTestData.FieldValue(html, HeaderField);

    /// <summary>Whether the page holds the header field or its form.</summary>
    public static bool HasHeaderField(string html) =>
        ListSearchTestData.TagOf(html, HeaderField) is not null || ListSearchTestData.TagOf(html, HeaderForm) is not null;

    /// <summary>The markup between the header's opening and closing tags; empty when there is no header.</summary>
    public static string HeaderMarkup(string html)
    {
        var match = HeaderElement().Match(html);
        return match.Success ? match.Groups["body"].Value : string.Empty;
    }

    /// <summary>The course card's address.</summary>
    public static string CardHref(long courseId) => CourseCardTestData.Path(courseId);

    /// <summary>A body with no markup at all from the text, for the refused and encoded cases.</summary>
    public static string DecodedAttributeFree(string html) => WebUtility.HtmlDecode(AttributeValue().Replace(html, "\"\""));

    [GeneratedRegex(@"<section\b[^>]*\bid=""(?<id>search-group-[a-z-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex GroupOpening();

    [GeneratedRegex(@"<p\b[^>]*\bclass=""[^""]*\bsearch-group-limit\b[^""]*""[^>]*>(?<t>.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LimitParagraph();

    [GeneratedRegex(@"<a\b[^>]*\bclass=""[^""]*\bsearch-group-whole-list\b[^""]*""[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex WholeListAnchor();

    [GeneratedRegex(@"<a\b[^>]*\bhref=""[^""]*""[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Anchor();

    [GeneratedRegex(@"<header\b[^>]*>(?<body>.*?)</header>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HeaderElement();

    [GeneratedRegex(@"""[^""]*""")]
    private static partial Regex AttributeValue();
}
