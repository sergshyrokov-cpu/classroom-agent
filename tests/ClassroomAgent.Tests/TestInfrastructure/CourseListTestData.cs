using System.Globalization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Synthetic data for the US-020 tests (TC-4): the page URL, the query parameter names, the markup hooks the tests
/// read, and every translation key the page uses (test strategy §6). Kept in one place so a rename shows up in one
/// diff.
/// </summary>
public static class CourseListTestData
{
    public const string Path = "/workspace/courses";

    public const string JournalPath = "/workspace/journal";

    public const string MeetReportsPath = "/workspace/meet-reports";

    public static TimeZoneInfo Kyiv => JournalTestData.Kyiv;

    /// <summary>Openapi parameters (api-design §2.2).</summary>
    public static class Parameters
    {
        public const string Status = "status";

        public const string TeacherId = "teacherId";

        public const string Q = "q";

        public const string Page = "page";
    }

    /// <summary>The markup the tests read (test strategy §4). IMPLEMENTATION renders exactly these hooks.</summary>
    public static class Markup
    {
        /// <summary>The filter form: <c>&lt;form id="course-list-form" method="get" action="/workspace/courses"&gt;</c>.</summary>
        public const string FormId = "course-list-form";

        /// <summary>One <c>&lt;tr data-course-id="{id}"&gt;</c> per listed course, in list order.</summary>
        public const string RowAttribute = "data-course-id";

        /// <summary>The element holding the total number of matching courses.</summary>
        public const string TotalId = "course-list-total";

        /// <summary>The <c>&lt;nav&gt;</c> holding the paging links.</summary>
        public const string PagingId = "course-list-paging";

        /// <summary>The element holding an empty-state message; no table is drawn with it.</summary>
        public const string EmptyId = "course-list-empty";

        /// <summary>A validation message, as the journal and the Meet reports render theirs.</summary>
        public const string ErrorClass = "form-error";
    }

    /// <summary>
    /// A course-list URL with the parameters in the canonical order (openapi <c>CourseListUrl</c>), each given as raw text
    /// and percent-encoded as UTF-8.
    /// </summary>
    public static string Url(string? status = null, string? teacherId = null, string? q = null, string? page = null)
    {
        var query = new List<string>();
        Add(query, Parameters.Status, status);
        Add(query, Parameters.TeacherId, teacherId);
        Add(query, Parameters.Q, q);
        Add(query, Parameters.Page, page);
        return Path + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));
    }

    public static string Url(long teacherId) => Url(teacherId: Id(teacherId));

    public static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    public static string JournalLink(long courseId) => JournalPath + "?courseId=" + Id(courseId);

    public static string MeetReportsLink(long courseId) => MeetReportsPath + "?courseId=" + Id(courseId);

    private static void Add(List<string> query, string name, string? value)
    {
        if (value is not null)
        {
            query.Add(name + "=" + Uri.EscapeDataString(value));
        }
    }

    /// <summary>Every translation key of the page (test strategy §6); each must exist in uk and en.</summary>
    public static class Keys
    {
        public const string NavigationEntry = "Courses.NavigationEntry";
        public const string Title = "Courses.Title";
        public const string FormStatus = "Courses.Form.Status";
        public const string FormTeacher = "Courses.Form.Teacher";
        public const string FormAllTeachers = "Courses.Form.AllTeachers";
        public const string FormSearch = "Courses.Form.Search";
        public const string FormSubmit = "Courses.Form.Submit";
        public const string StatusActive = "Courses.Status.Active";
        public const string StatusArchived = "Courses.Status.Archived";
        public const string StatusProvisioned = "Courses.Status.Provisioned";
        public const string StatusDeclined = "Courses.Status.Declined";
        public const string StatusSuspended = "Courses.Status.Suspended";
        public const string StatusAll = "Courses.Status.All";
        public const string ColumnName = "Courses.Column.Name";
        public const string ColumnStatus = "Courses.Column.Status";
        public const string ColumnTeachers = "Courses.Column.Teachers";
        public const string ColumnStudents = "Courses.Column.Students";
        public const string ColumnUpdatedAt = "Courses.Column.UpdatedAt";
        public const string ColumnLinks = "Courses.Column.Links";
        public const string LinkJournal = "Courses.Link.Journal";
        public const string LinkMeetReports = "Courses.Link.MeetReports";
        public const string TeacherNoName = "Courses.Teacher.NoName";
        public const string EmptyNoCoursesStored = "Courses.Empty.NoCoursesStored";
        public const string EmptyNoMatch = "Courses.Empty.NoMatch";
        public const string PagingTotal = "Courses.Paging.Total";
        public const string PagingPrevious = "Courses.Paging.Previous";
        public const string PagingNext = "Courses.Paging.Next";
        public const string ValidationStatusMalformed = "Courses.Validation.StatusMalformed";
        public const string ValidationTeacherMalformed = "Courses.Validation.TeacherMalformed";
        public const string ValidationTeacherUnknown = "Courses.Validation.TeacherUnknown";
        public const string ValidationSearchRepeated = "Courses.Validation.SearchRepeated";
        public const string ValidationSearchTooLong = "Courses.Validation.SearchTooLong";
        public const string ValidationSearchControlCharacter = "Courses.Validation.SearchControlCharacter";
        public const string ValidationPageMalformed = "Courses.Validation.PageMalformed";

        /// <summary>The five states, in the display order of spec FR-004, then "all".</summary>
        public static IReadOnlyList<string> Statuses { get; } =
        [
            StatusActive, StatusArchived, StatusProvisioned, StatusDeclined, StatusSuspended, StatusAll,
        ];

        public static IReadOnlyList<string> All { get; } =
        [
            NavigationEntry, Title, FormStatus, FormTeacher, FormAllTeachers, FormSearch, FormSubmit,
            StatusActive, StatusArchived, StatusProvisioned, StatusDeclined, StatusSuspended, StatusAll,
            ColumnName, ColumnStatus, ColumnTeachers, ColumnStudents, ColumnUpdatedAt, ColumnLinks,
            LinkJournal, LinkMeetReports, TeacherNoName, EmptyNoCoursesStored, EmptyNoMatch,
            PagingTotal, PagingPrevious, PagingNext,
            ValidationStatusMalformed, ValidationTeacherMalformed, ValidationTeacherUnknown, ValidationSearchRepeated,
            ValidationSearchTooLong, ValidationSearchControlCharacter, ValidationPageMalformed,
        ];
    }
}
