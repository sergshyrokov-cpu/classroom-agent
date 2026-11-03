namespace ClassroomAgent.Web.Security;

/// <summary>
/// The page-search hint (US-046 spec FR-001, FR-003; US-070 FR-008, api-design D-10): its four variants, as rendered in
/// the hook's <c>data-variant</c>. A variant with a search field names that field in the hint; <c>{0}</c> comes from
/// the field's own label key (<see cref="FieldLabelKey"/>), so the two cannot differ. Every key exists in both the
/// Ukrainian and the English file.
/// </summary>
public static class PageSearchHintTextKeys
{
    /// <summary>An ordinary teaching-data page.</summary>
    public const string Ordinary = "ordinary";

    /// <summary>The course list, which has its own search field.</summary>
    public const string CourseList = "course-list";

    /// <summary>The teacher list, which has its own search field.</summary>
    public const string TeacherList = "teacher-list";

    /// <summary>The Meet codes page, which has its own search field.</summary>
    public const string MeetCodes = "meet-codes";

    public const string OrdinaryText = "PageSearchHint.Ordinary";

    public const string ListFieldText = "PageSearchHint.ListField";

    /// <summary>The label key of the variant's search field, or null for the ordinary variant.</summary>
    public static string? FieldLabelKey(string variant) => variant switch
    {
        Ordinary => null,
        CourseList => CoursesTextKeys.FormSearch,
        TeacherList => TeachersTextKeys.FormSearch,
        MeetCodes => MeetCodesTextKeys.FormSearch,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
    };
}
