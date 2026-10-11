using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-033 AC-009 (spec FR-006, I-5): a person's seconds in one meeting are the sum of their connections capped at the
/// meeting's duration; report 3 adds capped seconds (never rounded minutes) and the DTOs carry seconds, so rounding
/// happens once, in the view.
/// </summary>
public sealed class MeetReportsMinutesTests
{
    private const string Code = "aaa-aaaa-aaa";

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en");

    private static readonly DateTimeOffset Early = MeetReportsTestData.Period.Local(9, 1, 0);

    private static DateTimeOffset At(int month, int day, int hour, int minute = 0) =>
        MeetReportsTestData.Period.Local(month, day, hour, minute);

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static async Task<MeetReports> OctoberAsync(FakeMeetReportSource source, long course)
    {
        var query = new GetMeetReportsQuery(
            source.Courses,
            source,
            new GetLastSynchronizationQuery(new SyncWorld.StateRepository(), new SchoolTimeZone(MeetReportsTestData.Kyiv)),
            new SchoolTimeZone(MeetReportsTestData.Kyiv),
            new ManualTimeProvider(MeetReportsTestData.Now));
        var request = new MeetReportsRequest([Id(course)], [MeetReportsTestData.Period.FromText], [MeetReportsTestData.Period.ToText]);

        var result = await query.ExecuteAsync(request, En, TestContext.Current.CancellationToken);

        Assert.Equal(MeetReportsPageOutcome.Shown, result.Outcome);
        Assert.NotNull(result.Page.Reports);
        return result.Page.Reports;
    }

    private static async Task<MeetingDetail> DetailAsync(FakeMeetReportSource source, long course, long meeting)
    {
        var query = new GetMeetReportMeetingQuery(source, new SchoolTimeZone(MeetReportsTestData.Kyiv));
        var request = new MeetReportMeetingRequest(Id(meeting), [Id(course)], [], []);

        var result = await query.ExecuteAsync(request, En, TestContext.Current.CancellationToken);

        Assert.Equal(MeetReportMeetingOutcome.Shown, result.Outcome);
        Assert.NotNull(result.Page.Meeting);
        return result.Page.Meeting;
    }

    private static (FakeMeetReportSource Source, long Course, string Student) CourseWithStudent()
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course");
        source.Link(Code, course);
        var email = MeetReportsTestData.Email("student");
        var participant = source.AddParticipant(email, "Stepan Student");
        source.AddMembership(course, participant, ClassroomRole.Student, Early, MeetReportsTestData.Now);
        return (source, course, email);
    }

    /// <summary>AC-009: two devices of 40 minutes in a 45-minute meeting give 2700 s in report 4 and add 2700 to report 3.</summary>
    [Fact]
    public async Task TwoDevices_AreCappedAtTheMeetingsDuration()
    {
        var (source, course, student) = CourseWithStudent();
        var meeting = source.AddMeeting(
            Code, MeetReportsTestData.Email("organizer"), At(10, 12, 10), At(10, 12, 10, 45), (student, 2400), (student, 2400));

        var detail = await DetailAsync(source, course, meeting);
        var reports = await OctoberAsync(source, course);

        var line = Assert.Single(detail.People, p => p.Mark == MeetParticipantMark.CourseStudent);
        Assert.Equal(2700, line.Seconds);
        Assert.Equal(2700, detail.DurationSeconds);
        var row = Assert.Single(reports.Students);
        Assert.Equal(1, row.Meetings);
        Assert.Equal(2700, row.TotalSeconds);
    }

    /// <summary>I-5: two meetings with 30 seconds each — report 3 carries 60 seconds, not two rounded minutes.</summary>
    [Fact]
    public async Task ReportThree_AddsSeconds_BeforeRounding()
    {
        var (source, course, student) = CourseWithStudent();
        source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 5, 10), At(10, 5, 10, 45), (student, 30));
        source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 6, 10), At(10, 6, 10, 45), (student, 30));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Students);
        Assert.Equal(2, row.Meetings);
        Assert.Equal(60, row.TotalSeconds);
    }

    /// <summary>I-5: a single 90-second connection stays 90 seconds in both reports (the view rounds it half up to 2).</summary>
    [Fact]
    public async Task ASingleShortConnection_StaysInSeconds()
    {
        var (source, course, student) = CourseWithStudent();
        var meeting = source.AddMeeting(
            Code, MeetReportsTestData.Email("organizer"), At(10, 5, 10), At(10, 5, 10, 45), (student, 90));

        var detail = await DetailAsync(source, course, meeting);
        var reports = await OctoberAsync(source, course);

        Assert.Equal(90, Assert.Single(detail.People, p => p.Mark == MeetParticipantMark.CourseStudent).Seconds);
        Assert.Equal(90, Assert.Single(reports.Students).TotalSeconds);
    }

    /// <summary>FR-006, FR-009: the cap applies per meeting — 3000 s in a 2700 s meeting and 600 s in a 1800 s one give 3300.</summary>
    [Fact]
    public async Task TheCap_AppliesPerMeeting_BeforeSummingOverMeetings()
    {
        var (source, course, student) = CourseWithStudent();
        source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 5, 10), At(10, 5, 10, 45), (student, 3000));
        source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 6, 10), At(10, 6, 10, 30), (student, 600));

        var reports = await OctoberAsync(source, course);

        Assert.Equal(3300, Assert.Single(reports.Students).TotalSeconds);
    }

    /// <summary>FR-006: connections of a person in one meeting are summed when they are below the cap.</summary>
    [Fact]
    public async Task Connections_BelowTheCap_AreSummed()
    {
        var (source, course, student) = CourseWithStudent();
        var meeting = source.AddMeeting(
            Code, MeetReportsTestData.Email("organizer"), At(10, 5, 10), At(10, 5, 10, 45), (student, 600), (student, 900));

        var detail = await DetailAsync(source, course, meeting);

        Assert.Equal(1500, Assert.Single(detail.People, p => p.Mark == MeetParticipantMark.CourseStudent).Seconds);
    }
}
