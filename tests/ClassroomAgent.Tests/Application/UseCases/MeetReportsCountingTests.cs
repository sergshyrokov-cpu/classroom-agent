using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-033 AC-005, AC-006, AC-007 (spec FR-005, FR-008, I-4, I-8): who a connected person is in report 2 — organizer
/// (named, never counted), course teacher, course student on the meeting's date, personal account not on the list,
/// other participant — with the precedence of I-4, case-insensitive emails and the roster as it was on the date.
/// </summary>
public sealed class MeetReportsCountingTests
{
    private const string Code = "aaa-aaaa-aaa";

    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk");

    private static readonly DateTimeOffset Early = MeetReportsTestData.Period.Local(9, 1, 0);

    private static DateTimeOffset At(int month, int day, int hour, int minute = 0) =>
        MeetReportsTestData.Period.Local(month, day, hour, minute);

    private static GetMeetReportsQuery QueryOver(FakeMeetReportSource source) =>
        new(
            source.Courses,
            source,
            new GetLastSynchronizationQuery(new SyncWorld.StateRepository(), new SchoolTimeZone(MeetReportsTestData.Kyiv)),
            new SchoolTimeZone(MeetReportsTestData.Kyiv),
            new ManualTimeProvider(MeetReportsTestData.Now));

    private static async Task<MeetReports> OctoberAsync(FakeMeetReportSource source, long course)
    {
        var request = new MeetReportsRequest(
            [course.ToString(CultureInfo.InvariantCulture)],
            [MeetReportsTestData.Period.FromText],
            [MeetReportsTestData.Period.ToText]);

        var result = await QueryOver(source).ExecuteAsync(request, Uk, TestContext.Current.CancellationToken);

        Assert.Equal(MeetReportsPageOutcome.Shown, result.Outcome);
        Assert.NotNull(result.Page.Reports);
        return result.Page.Reports;
    }

    private static (FakeMeetReportSource Source, long Course) CourseWithCode()
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course");
        source.Link(Code, course);
        return (source, course);
    }

    private static long AddMember(
        FakeMeetReportSource source,
        long course,
        string email,
        string name,
        ClassroomRole role,
        DateTimeOffset? lastSeenAt = null,
        bool onRoster = true,
        DateTimeOffset? firstSeenAt = null)
    {
        var participant = source.AddParticipant(email, name);
        source.AddMembership(
            course, participant, role, firstSeenAt ?? Early, lastSeenAt ?? MeetReportsTestData.Now, onRoster);
        return participant;
    }

    /// <summary>
    /// AC-005: an organizer (a teacher), another teacher, two students, a personal account of another course, a class
    /// account (a domain email nobody owns) and an external guest (no email) — 1, 2, 1, 2 and the organizer counted nowhere.
    /// </summary>
    [Fact]
    public async Task ARichMeeting_IsCountedByRule()
    {
        var (source, course) = CourseWithCode();
        var other = source.AddCourse("Other course");
        var organizer = MeetReportsTestData.Email("organizer");
        AddMember(source, course, organizer, "Olena Organizer", ClassroomRole.Teacher);
        AddMember(source, course, MeetReportsTestData.Email("teacher"), "Taras Teacher", ClassroomRole.Teacher);
        AddMember(source, course, MeetReportsTestData.Email("student1"), "Stepan Student", ClassroomRole.Student);
        AddMember(source, course, MeetReportsTestData.Email("student2"), "Svitlana Student", ClassroomRole.Student);
        AddMember(source, other, MeetReportsTestData.Email("guest"), "Hanna Elsewhere", ClassroomRole.Student);
        source.AddMeeting(
            Code,
            organizer,
            At(10, 12, 10),
            At(10, 12, 10, 45),
            (organizer, 2400),
            (MeetReportsTestData.Email("teacher"), 2400),
            (MeetReportsTestData.Email("student1"), 2400),
            (MeetReportsTestData.Email("student2"), 2400),
            (MeetReportsTestData.Email("guest"), 600),
            (MeetReportsTestData.Email("class-9a"), 600),
            (null, 600));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Meetings);
        Assert.Equal(1, row.CourseTeachers);
        Assert.Equal(2, row.CourseStudents);
        Assert.Equal(1, row.NotOnCourseList);
        Assert.Equal(2, row.OtherParticipants);
        Assert.Equal(new PersonRef("Olena Organizer", organizer), row.Organizer);
        Assert.False(row.OrganizerNotCourseTeacher);
    }

    /// <summary>AC-005: a person connecting three times counts once; an email differing only in case matches the roster.</summary>
    [Fact]
    public async Task RepeatedConnections_AndEmailCase_DoNotChangeTheCounts()
    {
        var (source, course) = CourseWithCode();
        AddMember(source, course, MeetReportsTestData.Email("student1"), "Stepan Student", ClassroomRole.Student);
        AddMember(source, course, MeetReportsTestData.Email("teacher"), "Taras Teacher", ClassroomRole.Teacher);
        source.AddMeeting(
            Code,
            MeetReportsTestData.Email("organizer"),
            At(10, 12, 10),
            At(10, 12, 10, 45),
            (MeetReportsTestData.Email("student1"), 300),
            (MeetReportsTestData.Email("student1"), 300),
            (MeetReportsTestData.Email("STUDENT1"), 300),
            (MeetReportsTestData.Email("Teacher"), 300),
            (MeetReportsTestData.Email("teacher"), 300));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Meetings);
        Assert.Equal(1, row.CourseStudents);
        Assert.Equal(1, row.CourseTeachers);
        Assert.Equal(0, row.NotOnCourseList);
        Assert.Equal(0, row.OtherParticipants);
    }

    /// <summary>I-8: a guest without an email who reconnects counts twice, as the connections carry no identity.</summary>
    [Fact]
    public async Task AGuestWithoutEmail_ConnectingTwice_CountsTwice()
    {
        var (source, course) = CourseWithCode();
        source.AddMeeting(
            Code, MeetReportsTestData.Email("organizer"), At(10, 12, 10), At(10, 12, 10, 45), (null, 300), (null, 300));

        var reports = await OctoberAsync(source, course);

        Assert.Equal(2, Assert.Single(reports.Meetings).OtherParticipants);
    }

    /// <summary>I-8: one class account connecting twice counts once — distinct non-personal domain emails.</summary>
    [Fact]
    public async Task AClassAccount_ConnectingTwice_CountsOnce()
    {
        var (source, course) = CourseWithCode();
        source.AddMeeting(
            Code,
            MeetReportsTestData.Email("organizer"),
            At(10, 12, 10),
            At(10, 12, 10, 45),
            (MeetReportsTestData.Email("class-9a"), 300),
            (MeetReportsTestData.Email("class-9a"), 300));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Meetings);
        Assert.Equal(1, row.OtherParticipants);
        Assert.Equal(0, row.NotOnCourseList);
    }

    /// <summary>I-4: a person on the roster as teacher and as student on the date counts once, as a teacher.</summary>
    [Fact]
    public async Task ATeacherWhoIsAlsoAStudent_CountsAsTeacherOnly()
    {
        var (source, course) = CourseWithCode();
        var both = source.AddParticipant(MeetReportsTestData.Email("both"), "Both Roles");
        source.AddMembership(course, both, ClassroomRole.Teacher, Early, MeetReportsTestData.Now);
        source.AddMembership(course, both, ClassroomRole.Student, Early, MeetReportsTestData.Now);
        source.AddMeeting(
            Code,
            MeetReportsTestData.Email("organizer"),
            At(10, 12, 10),
            At(10, 12, 10, 45),
            (MeetReportsTestData.Email("both"), 600));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Meetings);
        Assert.Equal(1, row.CourseTeachers);
        Assert.Equal(0, row.CourseStudents);
    }

    /// <summary>I-4: an organizer who is also a student of the course is counted nowhere.</summary>
    [Fact]
    public async Task AnOrganizer_WhoIsAlsoAStudent_IsCountedNowhere()
    {
        var (source, course) = CourseWithCode();
        var organizer = MeetReportsTestData.Email("organizer");
        AddMember(source, course, organizer, "Olena Organizer", ClassroomRole.Student);
        source.AddMeeting(Code, organizer, At(10, 12, 10), At(10, 12, 10, 45), (organizer, 600));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Meetings);
        Assert.Equal(0, row.CourseTeachers);
        Assert.Equal(0, row.CourseStudents);
        Assert.Equal(0, row.NotOnCourseList);
        Assert.Equal(0, row.OtherParticipants);
    }

    /// <summary>
    /// AC-006: a student first seen after meeting X's date but on the roster at Y is "not on the list" at X and a course
    /// student at Y.
    /// </summary>
    [Fact]
    public async Task AStudent_JoiningLater_IsNotOnTheListAtTheEarlierMeeting()
    {
        var (source, course) = CourseWithCode();
        var email = MeetReportsTestData.Email("late");
        AddMember(source, course, email, "Late Joiner", ClassroomRole.Student, firstSeenAt: At(10, 8, 12));
        var x = source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 5, 10), At(10, 5, 10, 45), (email, 600));
        var y = source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 12, 10), At(10, 12, 10, 45), (email, 600));

        var reports = await OctoberAsync(source, course);

        var earlier = reports.Meetings.Single(m => m.MeetingId == x);
        var later = reports.Meetings.Single(m => m.MeetingId == y);
        Assert.Equal(0, earlier.CourseStudents);
        Assert.Equal(1, earlier.NotOnCourseList);
        Assert.Equal(1, later.CourseStudents);
        Assert.Equal(0, later.NotOnCourseList);
    }

    /// <summary>AC-006: a student who left before the meeting's date (off the roster, last seen earlier) is not on the list.</summary>
    [Fact]
    public async Task AStudent_WhoLeftBeforeTheMeeting_IsNotOnTheList()
    {
        var (source, course) = CourseWithCode();
        var email = MeetReportsTestData.Email("leaver");
        AddMember(source, course, email, "Early Leaver", ClassroomRole.Student, lastSeenAt: At(10, 3, 12), onRoster: false);
        source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 5, 10), At(10, 5, 10, 45), (email, 600));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Meetings);
        Assert.Equal(0, row.CourseStudents);
        Assert.Equal(1, row.NotOnCourseList);
    }

    /// <summary>
    /// AC-006: "on the roster on D" compares local dates — last seen at 00:30 Kyiv on D+1 (still D in UTC) keeps the
    /// student on the roster on D; first seen at 00:30 Kyiv on D (the previous day in UTC) is already on it.
    /// </summary>
    [Fact]
    public async Task RosterDates_AreComparedAsLocalDates()
    {
        var (source, course) = CourseWithCode();
        var leaver = MeetReportsTestData.Email("leaver");
        var joiner = MeetReportsTestData.Email("joiner");
        AddMember(source, course, leaver, "Leaver", ClassroomRole.Student, lastSeenAt: At(10, 6, 0, 30), onRoster: false);
        AddMember(source, course, joiner, "Joiner", ClassroomRole.Student, firstSeenAt: At(10, 5, 0, 30));
        source.AddMeeting(
            Code, MeetReportsTestData.Email("organizer"), At(10, 5, 10), At(10, 5, 10, 45), (leaver, 600), (joiner, 600));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Meetings);
        Assert.Equal(2, row.CourseStudents);
        Assert.Equal(0, row.NotOnCourseList);
    }

    /// <summary>AC-006: last seen on the evening before D (local) and off the roster — not on the roster on D.</summary>
    [Fact]
    public async Task ALeaver_LastSeenTheEveningBefore_IsNotOnTheRosterOnTheDate()
    {
        var (source, course) = CourseWithCode();
        var leaver = MeetReportsTestData.Email("leaver");
        AddMember(source, course, leaver, "Leaver", ClassroomRole.Student, lastSeenAt: At(10, 4, 23, 30), onRoster: false);
        source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 5, 10), At(10, 5, 10, 45), (leaver, 600));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Meetings);
        Assert.Equal(0, row.CourseStudents);
        Assert.Equal(1, row.NotOnCourseList);
    }

    /// <summary>AC-007: an organizer who teaches another course only is marked "not a teacher of this course".</summary>
    [Fact]
    public async Task AnOrganizer_WhoTeachesAnotherCourse_IsMarkedNotACourseTeacher()
    {
        var (source, course) = CourseWithCode();
        var other = source.AddCourse("Other course");
        var organizer = MeetReportsTestData.Email("organizer");
        AddMember(source, other, organizer, "Olena Organizer", ClassroomRole.Teacher);
        source.AddMeeting(Code, organizer, At(10, 12, 10), At(10, 12, 10, 45), (organizer, 600));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Meetings);
        Assert.True(row.OrganizerNotCourseTeacher);
        Assert.Equal(new PersonRef("Olena Organizer", organizer), row.Organizer);
        Assert.Equal(0, row.CourseTeachers);
    }

    /// <summary>AC-007: an organizer who is a teacher of the course on the date carries no mark.</summary>
    [Fact]
    public async Task AnOrganizer_WhoTeachesTheCourse_IsNotMarked()
    {
        var (source, course) = CourseWithCode();
        var organizer = MeetReportsTestData.Email("organizer");
        AddMember(source, course, organizer, "Olena Organizer", ClassroomRole.Teacher);
        source.AddMeeting(Code, organizer, At(10, 12, 10), At(10, 12, 10, 45), (organizer, 600));

        var reports = await OctoberAsync(source, course);

        Assert.False(Assert.Single(reports.Meetings).OrganizerNotCourseTeacher);
    }

    /// <summary>
    /// I-3: an organizer who never connected is still named in report 2 (by email alone when nobody owns the address) and
    /// is not a teacher of the course.
    /// </summary>
    [Fact]
    public async Task AnOrganizer_WhoNeverConnected_IsStillNamed()
    {
        var (source, course) = CourseWithCode();
        var organizer = MeetReportsTestData.Email("staff");
        source.AddMeeting(Code, organizer, At(10, 12, 10), At(10, 12, 10, 45));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Meetings);
        Assert.Equal(new PersonRef(null, organizer), row.Organizer);
        Assert.True(row.OrganizerNotCourseTeacher);
    }

    /// <summary>The "personal account" check asks the source only for the emails of the report's connections (spec §9).</summary>
    [Fact]
    public async Task ThePeopleLookup_IsBounded_ToTheEmailsOfTheReport()
    {
        var (source, course) = CourseWithCode();
        AddMember(source, course, MeetReportsTestData.Email("unrelated"), "Unrelated", ClassroomRole.Student);
        source.AddMeeting(
            Code,
            MeetReportsTestData.Email("organizer"),
            At(10, 12, 10),
            At(10, 12, 10, 45),
            (MeetReportsTestData.Email("Someone"), 300),
            (null, 300));

        await OctoberAsync(source, course);

        Assert.NotEmpty(source.PeopleRequests);
        var asked = source.PeopleRequests.SelectMany(r => r).ToList();
        Assert.DoesNotContain(MeetReportsTestData.Email("unrelated"), asked);
        Assert.All(asked, email => Assert.Equal(email.ToLowerInvariant(), email));
    }
}
