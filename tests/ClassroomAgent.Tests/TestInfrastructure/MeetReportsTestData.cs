using System.Globalization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Synthetic data for the US-033 tests (TC-4): a school domain, an October period in <c>Europe/Kyiv</c> that crosses
/// the end of summer time (TC-8), the page URLs, and every translation key the two pages use (spec FR-016,
/// test strategy §6). Kept in one place so a rename shows up in one diff.
/// </summary>
public static class MeetReportsTestData
{
    public const string Domain = "school.example";

    public static string Email(string local) => $"{local}@{Domain}";

    public static TimeZoneInfo Kyiv => JournalTestData.Kyiv;

    /// <summary>"Now" for the default period: 2026-10-15 12:00 UTC — inside October in Kyiv.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    public static class Period
    {
        public static readonly DateOnly From = new(2026, 10, 1);

        public static readonly DateOnly To = new(2026, 10, 31);

        public static string FromText => From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static string ToText => To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>2026-10-01 00:00 Kyiv (UTC+3) — the start of the UTC interval.</summary>
        public static readonly DateTimeOffset StartUtc = new(2026, 9, 30, 21, 0, 0, TimeSpan.Zero);

        /// <summary>2026-11-01 00:00 Kyiv (UTC+2, after 2026-10-25) — the exclusive end.</summary>
        public static readonly DateTimeOffset EndUtc = new(2026, 10, 31, 22, 0, 0, TimeSpan.Zero);

        /// <summary>A Kyiv local time as a UTC instant, for meeting starts inside or around the period.</summary>
        public static DateTimeOffset Local(int month, int day, int hour, int minute = 0) =>
            new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(
                    new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified), Kyiv),
                TimeSpan.Zero);
    }

    public static string Url(long? courseId = null, string? from = null, string? to = null)
    {
        var query = new List<string>();
        if (courseId is { } id)
        {
            query.Add("courseId=" + id.ToString(CultureInfo.InvariantCulture));
        }

        if (from is not null)
        {
            query.Add("from=" + Uri.EscapeDataString(from));
        }

        if (to is not null)
        {
            query.Add("to=" + Uri.EscapeDataString(to));
        }

        return "/workspace/meet-reports" + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));
    }

    public static string OctoberUrl(long courseId) => Url(courseId, Period.FromText, Period.ToText);

    public static string MeetingUrl(string meetingId, long? courseId, string? from = null, string? to = null)
    {
        var query = Url(courseId, from, to);
        var queryPart = query.Contains('?', StringComparison.Ordinal) ? query[query.IndexOf('?', StringComparison.Ordinal)..] : string.Empty;
        return "/workspace/meet-reports/meetings/" + meetingId + queryPart;
    }

    /// <summary>Every translation key of the two pages (test strategy §6); each must exist in uk and en.</summary>
    public static class Keys
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
        public const string MarkOrganizer = "MeetReports.Mark.Organizer";
        public const string MarkCourseTeacher = "MeetReports.Mark.CourseTeacher";
        public const string MarkCourseStudent = "MeetReports.Mark.CourseStudent";
        public const string MarkNotOnCourseList = "MeetReports.Mark.NotOnCourseList";
        public const string MarkNotCourseTeacher = "MeetReports.Mark.NotCourseTeacher";
        public const string PersonUnnamed = "MeetReports.Person.Unnamed";
        public const string MinutesOf = "MeetReports.MinutesOf";
        public const string MeetingTitle = "MeetReports.Meeting.Title";
        public const string MeetingCode = "MeetReports.Meeting.Code";
        public const string MeetingOtherParticipants = "MeetReports.Meeting.OtherParticipants";
        public const string MeetingBack = "MeetReports.Meeting.Back";
        public const string MeetingCourseMalformed = "MeetReports.Meeting.CourseMalformed";
        public const string MeetingNotFound = "MeetReports.Meeting.MeetingNotFound";
        public const string ValidationCourseMalformed = "MeetReports.Validation.CourseMalformed";
        public const string ValidationCourseUnknown = "MeetReports.Validation.CourseUnknown";
        public const string ValidationFromMalformed = "MeetReports.Validation.FromMalformed";
        public const string ValidationToMalformed = "MeetReports.Validation.ToMalformed";
        public const string ValidationPeriodInverted = "MeetReports.Validation.PeriodInverted";

        public static IReadOnlyList<string> All { get; } =
        [
            NavigationEntry, Title, FormCourse, FormFrom, FormTo, FormSubmit, NoCourses, DelayNotice,
            DelayNoticeLoadedUpTo, DelayNoticeNotLoaded, SummaryTitle, SummaryMeetingCount, SummaryTotalDuration,
            SummaryAverageDuration, SummaryAverageStudents, MeetingsTitle, MeetingsDate, MeetingsStart, MeetingsEnd,
            MeetingsDuration, MeetingsOrganizer, MeetingsCourseTeachers, MeetingsCourseStudents, MeetingsNotOnCourseList,
            MeetingsOtherParticipants, StudentsTitle, StudentsStudent, StudentsMeetings, StudentsMinutes,
            EmptyNoMeetings, EmptyNoStudents, MarkOrganizer, MarkCourseTeacher, MarkCourseStudent, MarkNotOnCourseList,
            MarkNotCourseTeacher, PersonUnnamed, MinutesOf, MeetingTitle, MeetingCode, MeetingOtherParticipants,
            MeetingBack, MeetingCourseMalformed, MeetingNotFound, ValidationCourseMalformed, ValidationCourseUnknown,
            ValidationFromMalformed, ValidationToMalformed, ValidationPeriodInverted,
        ];
    }
}
