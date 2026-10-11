using System.Globalization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Synthetic data for the US-021 tests (TC-4): the card URL, the markup hooks the tests read, and every translation key
/// the card adds (test strategy §5, §6). Kept in one place so a rename shows up in one diff.
/// </summary>
public static class CourseCardTestData
{
    /// <summary>The course list, the card's parent path (api-design §2.1).</summary>
    public const string ListPath = CourseListTestData.Path;

    /// <summary>The endpoint's pattern as <see cref="HostEndpoint.Pattern"/> normalizes it (constraint stripped, lower case).</summary>
    public const string RoutePattern = "workspace/courses/{courseid}";

    /// <summary>The route constraint's quantifier, as it appears in the raw route template (api-design §2.2).</summary>
    public const string ConstraintQuantifier = "1,19";

    public static TimeZoneInfo Kyiv => JournalTestData.Kyiv;

    /// <summary>The card of a course: <c>/workspace/courses/{id}</c> in canonical decimal form.</summary>
    public static string Path(long courseId) => Path(Id(courseId));

    /// <summary>The card URL with a raw path value, for the malformed and unknown cases.</summary>
    public static string Path(string rawCourseId) => ListPath + "/" + rawCourseId;

    public static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    public static string JournalLink(long courseId) => CourseListTestData.JournalLink(courseId);

    public static string MeetReportsLink(long courseId) => CourseListTestData.MeetReportsLink(courseId);

    /// <summary>
    /// Malformed values that still match the route (1 … 19 ASCII digits) and are refused by the parse (spec VR-001,
    /// api-design §2.2): zero, zeros, and a 19-digit value above <see cref="long.MaxValue"/>.
    /// </summary>
    public static IReadOnlyList<string> MalformedMatchingTheRoute { get; } = ["0", "000", "9223372036854775808", "9999999999999999999"];

    /// <summary>Values the route does not match (api-design §2.2): the host's catch-all 404.</summary>
    public static IReadOnlyList<string> NotMatchingTheRoute { get; } = ["abc", "-1", "%2B1", "1.0", "1e3", "12345678901234567890", "zz-echo-zz"];

    /// <summary>The markup the tests read (test strategy §5). IMPLEMENTATION renders exactly these hooks.</summary>
    public static class Markup
    {
        /// <summary>
        /// Each shown detail value sits in its own element carrying <c>data-field="{field}"</c>, holding the value only
        /// (its label is a separate element). An omitted field has neither the element nor its label (spec FR-002).
        /// </summary>
        public const string FieldAttribute = "data-field";

        /// <summary>The three sections: <c>&lt;section id="…"&gt;</c>, in this order.</summary>
        public const string TeachersId = "course-card-teachers";

        public const string StudentsId = "course-card-students";

        public const string LeaversId = "course-card-leavers";

        /// <summary>Inside each section, one element <c>&lt;span data-count&gt;{n}&lt;/span&gt;</c> holding the count.</summary>
        public const string CountAttribute = "data-count";

        /// <summary>One <c>&lt;tr data-person&gt;</c> per person of the section, in section order.</summary>
        public const string RowAttribute = "data-person";

        /// <summary>Inside a teacher's row, the element carrying <c>data-owner</c> holds the translated "owner" mark.</summary>
        public const string OwnerAttribute = "data-owner";

        /// <summary>An empty section shows one element with <c>data-empty</c> holding its message, and no table.</summary>
        public const string EmptyAttribute = "data-empty";
    }

    /// <summary><c>data-field</c> values (spec FR-002, in display order).</summary>
    public static class Fields
    {
        public const string Name = "name";
        public const string Section = "section";
        public const string Status = "status";
        public const string Room = "room";
        public const string DescriptionHeading = "description-heading";
        public const string Description = "description";
        public const string CreatedAt = "created-at";
        public const string UpdatedAt = "updated-at";

        public static IReadOnlyList<string> Optional { get; } = [Section, Room, DescriptionHeading, Description, CreatedAt, UpdatedAt];
    }

    /// <summary>Every translation key the card adds (spec FR-016); each must exist in uk and en.</summary>
    public static class Keys
    {
        public const string Title = "Courses.Card.Title";
        public const string FieldSection = "Courses.Card.Field.Section";
        public const string FieldStatus = "Courses.Card.Field.Status";
        public const string FieldRoom = "Courses.Card.Field.Room";
        public const string FieldDescriptionHeading = "Courses.Card.Field.DescriptionHeading";
        public const string FieldDescription = "Courses.Card.Field.Description";
        public const string FieldCreatedAt = "Courses.Card.Field.CreatedAt";
        public const string FieldUpdatedAt = "Courses.Card.Field.UpdatedAt";
        public const string SectionTeachers = "Courses.Card.Section.Teachers";
        public const string SectionStudents = "Courses.Card.Section.Students";
        public const string SectionLeavers = "Courses.Card.Section.Leavers";
        public const string Count = "Courses.Card.Count";
        public const string ColumnName = "Courses.Card.Column.Name";
        public const string ColumnEmail = "Courses.Card.Column.Email";
        public const string ColumnRole = "Courses.Card.Column.Role";
        public const string ColumnLastSeen = "Courses.Card.Column.LastSeen";
        public const string Owner = "Courses.Card.Owner";
        public const string RoleTeacher = "Courses.Card.Role.Teacher";
        public const string RoleStudent = "Courses.Card.Role.Student";
        public const string NoName = "Courses.Card.NoName";
        public const string EmptyTeachers = "Courses.Card.Empty.Teachers";
        public const string EmptyStudents = "Courses.Card.Empty.Students";
        public const string EmptyLeavers = "Courses.Card.Empty.Leavers";
        public const string LinkJournal = "Courses.Card.Link.Journal";
        public const string LinkMeetReports = "Courses.Card.Link.MeetReports";
        public const string LinkCourses = "Courses.Card.Link.Courses";

        /// <summary>The labels of the optional fields, aligned with <see cref="Fields.Optional"/>.</summary>
        public static IReadOnlyList<string> OptionalFieldLabels { get; } =
        [
            FieldSection, FieldRoom, FieldDescriptionHeading, FieldDescription, FieldCreatedAt, FieldUpdatedAt,
        ];

        public static IReadOnlyList<string> All { get; } =
        [
            Title, FieldSection, FieldStatus, FieldRoom, FieldDescriptionHeading, FieldDescription, FieldCreatedAt,
            FieldUpdatedAt, SectionTeachers, SectionStudents, SectionLeavers, Count, ColumnName, ColumnEmail, ColumnRole,
            ColumnLastSeen, Owner, RoleTeacher, RoleStudent, NoName, EmptyTeachers, EmptyStudents, EmptyLeavers,
            LinkJournal, LinkMeetReports, LinkCourses,
        ];
    }
}
