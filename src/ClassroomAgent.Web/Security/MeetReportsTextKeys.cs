using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the Meet reports pages (US-033 spec FR-016; api-design §5). The openapi enums map to one key
/// family each. Every key exists in both the Ukrainian and the English file.
/// </summary>
public static class MeetReportsTextKeys
{
    public const string NavigationEntry = "MeetReports.NavigationEntry";

    public const string Title = "MeetReports.Title";

    public const string FormCourse = "MeetReports.Form.Course";

    public const string FormFrom = "MeetReports.Form.From";

    public const string FormTo = "MeetReports.Form.To";

    public const string FormSubmit = "MeetReports.Form.Submit";

    public const string NoCourses = "MeetReports.NoCourses";

    public const string DelayNotice = "MeetReports.DelayNotice";

    public const string DelayNoticeLoadedUpTo = "MeetReports.DelayNotice.LoadedUpTo";

    public const string DelayNoticeNotLoaded = "MeetReports.DelayNotice.NotLoaded";

    public const string SummaryTitle = "MeetReports.Summary.Title";

    public const string SummaryMeetingCount = "MeetReports.Summary.MeetingCount";

    public const string SummaryTotalDuration = "MeetReports.Summary.TotalDuration";

    public const string SummaryAverageDuration = "MeetReports.Summary.AverageDuration";

    public const string SummaryAverageStudents = "MeetReports.Summary.AverageStudents";

    public const string MeetingsTitle = "MeetReports.Meetings.Title";

    public const string MeetingsDate = "MeetReports.Meetings.Date";

    public const string MeetingsStart = "MeetReports.Meetings.Start";

    public const string MeetingsEnd = "MeetReports.Meetings.End";

    public const string MeetingsDuration = "MeetReports.Meetings.Duration";

    public const string MeetingsOrganizer = "MeetReports.Meetings.Organizer";

    public const string MeetingsCourseTeachers = "MeetReports.Meetings.CourseTeachers";

    public const string MeetingsCourseStudents = "MeetReports.Meetings.CourseStudents";

    public const string MeetingsNotOnCourseList = "MeetReports.Meetings.NotOnCourseList";

    public const string MeetingsOtherParticipants = "MeetReports.Meetings.OtherParticipants";

    public const string StudentsTitle = "MeetReports.Students.Title";

    public const string StudentsStudent = "MeetReports.Students.Student";

    public const string StudentsMeetings = "MeetReports.Students.Meetings";

    public const string StudentsMinutes = "MeetReports.Students.Minutes";

    public const string EmptyNoMeetings = "MeetReports.Empty.NoMeetings";

    public const string EmptyNoStudents = "MeetReports.Empty.NoStudents";

    public const string MarkNotCourseTeacher = "MeetReports.Mark.NotCourseTeacher";

    public const string PersonUnnamed = "MeetReports.Person.Unnamed";

    /// <summary>"N of M min" (spec FR-010): <c>{0}</c> the person's minutes, <c>{1}</c> the meeting's.</summary>
    public const string MinutesOf = "MeetReports.MinutesOf";

    public const string MeetingTitle = "MeetReports.Meeting.Title";

    public const string MeetingCode = "MeetReports.Meeting.Code";

    public const string MeetingOtherParticipants = "MeetReports.Meeting.OtherParticipants";

    public const string MeetingBack = "MeetReports.Meeting.Back";

    public static string Mark(MeetParticipantMark mark) => "MeetReports.Mark." + mark;

    public static string Validation(MeetReportsMessageKey key) => "MeetReports.Validation." + key;

    public static string Meeting(MeetReportMeetingMessageKey key) => "MeetReports.Meeting." + key;
}
