using System.Globalization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Synthetic data for the US-022 tests (TC-4): the page URL, the query parameter names, the markup hooks the tests
/// read, and every translation key the page uses (test strategy §4, §5). Kept in one place so a rename shows up in one
/// diff.
/// </summary>
public static class TeacherListTestData
{
    public const string Path = "/workspace/teachers";

    /// <summary>The endpoint's route pattern as the host enumerates it.</summary>
    public const string Pattern = "workspace/teachers";

    /// <summary>Openapi parameters (api-design §2.2).</summary>
    public static class Parameters
    {
        public const string Status = "status";

        public const string Page = "page";
    }

    /// <summary>The markup the tests read (test strategy §4). IMPLEMENTATION renders exactly these hooks.</summary>
    public static class Markup
    {
        /// <summary>The status form: <c>&lt;form id="teacher-list-form" method="get" action="/workspace/teachers"&gt;</c>.</summary>
        public const string FormId = "teacher-list-form";

        /// <summary>One <c>&lt;tr data-participant-id="{id}"&gt;</c> per listed teacher, in list order.</summary>
        public const string RowAttribute = "data-participant-id";

        /// <summary>The element holding the total number of listed teachers.</summary>
        public const string TotalId = "teacher-list-total";

        /// <summary>The <c>&lt;nav&gt;</c> holding the paging links.</summary>
        public const string PagingId = "teacher-list-paging";

        /// <summary>The element holding the empty message (spec FR-008); no table is drawn with it.</summary>
        public const string EmptyId = "teacher-list-empty";

        /// <summary>The home page's Google Workspace section (US-020 I-1).</summary>
        public const string WorkspaceSectionId = "workspace-section";
    }

    /// <summary>A teacher-list URL with the parameters in the canonical order, each given as raw text and percent-encoded.</summary>
    public static string Url(string? status = null, string? page = null)
    {
        var query = new List<string>();
        if (status is not null)
        {
            query.Add(Parameters.Status + "=" + Uri.EscapeDataString(status));
        }

        if (page is not null)
        {
            query.Add(Parameters.Page + "=" + Uri.EscapeDataString(page));
        }

        return Path + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));
    }

    /// <summary>
    /// Api-design §2.6: the row link to the course list — <c>status</c> (omitted for active) then <c>teacherId</c>,
    /// written out literally so the test does not trust the rule under test.
    /// </summary>
    public static string CoursesLink(long participantId, string? status = null) =>
        CourseListTestData.Path + "?" + (status is null ? string.Empty : "status=" + status + "&") + "teacherId="
        + participantId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Every translation key of the page (test strategy §5); each must exist in uk and en.</summary>
    public static class Keys
    {
        public const string NavigationEntry = "Teachers.NavigationEntry";
        public const string Title = "Teachers.Title";
        public const string ColumnName = "Teachers.Column.Name";
        public const string ColumnEmail = "Teachers.Column.Email";
        public const string ColumnCourses = "Teachers.Column.Courses";
        public const string ColumnLink = "Teachers.Column.Link";
        public const string CoursesLink = "Teachers.CoursesLink";
        public const string PagingTotal = "Teachers.Paging.Total";
        public const string Empty = "Teachers.Empty";

        /// <summary>The new keys of this Story.</summary>
        public static IReadOnlyList<string> New { get; } =
        [
            NavigationEntry, Title, ColumnName, ColumnEmail, ColumnCourses, ColumnLink, CoursesLink, PagingTotal, Empty,
        ];

        /// <summary>Reused course-list keys (api-design §2.8).</summary>
        public static IReadOnlyList<string> Reused { get; } =
        [
            CourseListTestData.Keys.FormStatus,
            CourseListTestData.Keys.FormSubmit,
            CourseListTestData.Keys.TeacherNoName,
            CourseListTestData.Keys.PagingPrevious,
            CourseListTestData.Keys.PagingNext,
            CourseListTestData.Keys.ValidationStatusMalformed,
            CourseListTestData.Keys.ValidationPageMalformed,
            .. CourseListTestData.Keys.Statuses,
        ];
    }
}
