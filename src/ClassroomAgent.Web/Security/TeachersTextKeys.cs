using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the teacher list page (US-022 spec FR-015, test strategy §5). Every key exists in both the
/// Ukrainian and the English file; the status names, form labels, paging labels, "no name" and the validation messages
/// are the course list's (api-design §2.8).
/// </summary>
public static class TeachersTextKeys
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

    /// <summary>Api-design §2.8: rendered with the course list's texts for the same problem.</summary>
    public static string Validation(TeacherListMessageKey key) => "Courses.Validation." + key;
}
