using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the US-038 erasure pages (spec FR-018, AC-021). Every user-visible string comes from here;
/// names, emails and course names are shown as stored, never translated (NFR-073).
/// </summary>
public static class ErasureTextKeys
{
    public const string Title = "Erasure.Title";
    public const string NavigationEntry = "Erasure.NavigationEntry";
    public const string SearchLabel = "Erasure.Search.Label";
    public const string SearchButton = "Erasure.Search.Button";
    public const string NoneFound = "Erasure.Search.NoneFound";
    public const string Truncated = "Erasure.Search.Truncated";
    public const string NoName = "Erasure.Search.NoName";
    public const string NoEmail = "Erasure.Search.NoEmail";
    public const string TeacherCourses = "Erasure.Search.TeacherCourses";
    public const string StudentCourses = "Erasure.Search.StudentCourses";
    public const string LiftTitle = "Erasure.Lift.Title";
    public const string LiftLabel = "Erasure.Lift.Label";
    public const string LiftButton = "Erasure.Lift.Button";
    public const string NoExclusion = "Erasure.Lift.NoExclusion";
    public const string ReadOnlyNotice = "Erasure.ReadOnlyNotice";
    public const string ConfirmationTitle = "Erasure.Confirmation.Title";
    public const string Courses = "Erasure.Confirmation.Courses";
    public const string Submissions = "Erasure.Confirmation.Submissions";
    public const string Grades = "Erasure.Confirmation.Grades";
    public const string MeetParticipations = "Erasure.Confirmation.MeetParticipations";
    public const string OrganizedMeetings = "Erasure.Confirmation.OrganizedMeetings";
    public const string OwnedCourses = "Erasure.Confirmation.OwnedCourses";
    public const string ExportWarning = "Erasure.Confirmation.ExportWarning";
    public const string NoEmailNotice = "Erasure.Confirmation.NoEmailNotice";
    public const string Mark = "Erasure.Confirmation.Mark";
    public const string EraseButton = "Erasure.Confirmation.Button";
    public const string MarkRequired = "Erasure.Confirmation.MarkRequired";

    public static string Of(SearchFieldError error) => error switch
    {
        SearchFieldError.TermRequired => "Erasure.Search.Error.TermRequired",
        SearchFieldError.TermTooShort => "Erasure.Search.Error.TermTooShort",
        SearchFieldError.TermTooLong => "Erasure.Search.Error.TermTooLong",
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };

    public static string Of(LiftFieldError error) => error switch
    {
        LiftFieldError.EmailRequired => "Erasure.Lift.Error.EmailRequired",
        LiftFieldError.EmailTooLong => "Erasure.Lift.Error.EmailTooLong",
        LiftFieldError.EmailInvalid => "Erasure.Lift.Error.EmailInvalid",
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };

    public static string Of(ErasureMessageKey message) => message switch
    {
        ErasureMessageKey.Erased => "Erasure.Message.Erased",
        ErasureMessageKey.ExclusionLifted => "Erasure.Message.ExclusionLifted",
        _ => throw new ArgumentOutOfRangeException(nameof(message), message, null),
    };

    public static string Of(ErasureCourseRole role) => role switch
    {
        ErasureCourseRole.Teacher => "Erasure.Role.Teacher",
        ErasureCourseRole.Student => "Erasure.Role.Student",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}
