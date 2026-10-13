namespace ClassroomAgent.Web.Security;

/// <summary>
/// The page-search hint (US-046 spec FR-001, FR-003, FR-008; api-design §3): its three variants, as rendered in the
/// hook's <c>data-variant</c>, and the translation key of each. Every key exists in both the Ukrainian and the English
/// file.
/// </summary>
public static class PageSearchHintTextKeys
{
    /// <summary>An ordinary teaching-data page (spec FR-003 a).</summary>
    public const string Ordinary = "ordinary";

    /// <summary>The course list, which has its own search field (spec FR-003 b).</summary>
    public const string CourseList = "course-list";

    /// <summary>A paged list without a search field: teachers, Meet codes (spec FR-003 c).</summary>
    public const string PagedList = "paged-list";

    public const string OrdinaryText = "PageSearchHint.Ordinary";

    public const string CourseListText = "PageSearchHint.CourseList";

    public const string PagedListText = "PageSearchHint.PagedList";

    /// <summary>The translation key of a variant.</summary>
    public static string TextKey(string variant) => variant switch
    {
        Ordinary => OrdinaryText,
        CourseList => CourseListText,
        PagedList => PagedListText,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
    };
}
