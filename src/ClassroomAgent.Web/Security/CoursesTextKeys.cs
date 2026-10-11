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

    /// <summary>US-021 spec FR-016, test strategy §6: the course card.</summary>
    public const string CardTitle = "Courses.Card.Title";

    public const string CardFieldSection = "Courses.Card.Field.Section";

    public const string CardFieldStatus = "Courses.Card.Field.Status";

    public const string CardFieldRoom = "Courses.Card.Field.Room";

    public const string CardFieldDescriptionHeading = "Courses.Card.Field.DescriptionHeading";

    public const string CardFieldDescription = "Courses.Card.Field.Description";

    public const string CardFieldCreatedAt = "Courses.Card.Field.CreatedAt";

    public const string CardFieldUpdatedAt = "Courses.Card.Field.UpdatedAt";

    public const string CardSectionTeachers = "Courses.Card.Section.Teachers";

    public const string CardSectionStudents = "Courses.Card.Section.Students";

    public const string CardSectionLeavers = "Courses.Card.Section.Leavers";

    public const string CardCount = "Courses.Card.Count";

    public const string CardColumnName = "Courses.Card.Column.Name";

    public const string CardColumnEmail = "Courses.Card.Column.Email";

    public const string CardColumnRole = "Courses.Card.Column.Role";

    public const string CardColumnLastSeen = "Courses.Card.Column.LastSeen";

    public const string CardOwner = "Courses.Card.Owner";

    public const string CardNoName = "Courses.Card.NoName";

    public const string CardEmptyTeachers = "Courses.Card.Empty.Teachers";

    public const string CardEmptyStudents = "Courses.Card.Empty.Students";

    public const string CardEmptyLeavers = "Courses.Card.Empty.Leavers";

    public const string CardLinkJournal = "Courses.Card.Link.Journal";

    public const string CardLinkMeetReports = "Courses.Card.Link.MeetReports";

    public const string CardLinkCourses = "Courses.Card.Link.Courses";

    public static string Status(CourseStatusFilter status) => "Courses.Status." + status;

    public static string State(CourseState state) => "Courses.Status." + state;

    public static string Empty(CourseListEmptyState state) => "Courses.Empty." + state;

    public static string Validation(CourseListMessageKey key) => "Courses.Validation." + key;

    /// <summary>US-021 spec FR-003: the role of a leaver.</summary>
    public static string CardRole(ClassroomRole role) => "Courses.Card.Role." + role;
}
