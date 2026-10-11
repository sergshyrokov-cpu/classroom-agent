using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the course list page (US-020 spec FR-017). Every key exists in both the Ukrainian and the
/// English file.
/// </summary>
public static class CoursesTextKeys
{
    public const string NavigationEntry = "Courses.NavigationEntry";

    public const string Title = "Courses.Title";

    public const string FormStatus = "Courses.Form.Status";

    public const string FormTeacher = "Courses.Form.Teacher";

    public const string FormAllTeachers = "Courses.Form.AllTeachers";

    public const string FormSearch = "Courses.Form.Search";

    public const string FormSubmit = "Courses.Form.Submit";

    public const string ColumnName = "Courses.Column.Name";

    public const string ColumnStatus = "Courses.Column.Status";

    public const string ColumnTeachers = "Courses.Column.Teachers";

    public const string ColumnStudents = "Courses.Column.Students";

    public const string ColumnUpdatedAt = "Courses.Column.UpdatedAt";

    public const string ColumnLinks = "Courses.Column.Links";

    public const string LinkJournal = "Courses.Link.Journal";

    public const string LinkMeetReports = "Courses.Link.MeetReports";

    public const string TeacherNoName = "Courses.Teacher.NoName";

    public const string PagingTotal = "Courses.Paging.Total";

    public const string PagingPrevious = "Courses.Paging.Previous";

    public const string PagingNext = "Courses.Paging.Next";

    public static string Status(CourseStatusFilter status) => "Courses.Status." + status;

    public static string State(CourseState state) => "Courses.Status." + state;

    public static string Empty(CourseListEmptyState state) => "Courses.Empty." + state;

    public static string Validation(CourseListMessageKey key) => "Courses.Validation." + key;
}
