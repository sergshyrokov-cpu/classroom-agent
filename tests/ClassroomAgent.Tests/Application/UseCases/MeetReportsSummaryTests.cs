using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-033 AC-004, AC-012 (spec FR-007, FR-014, I-6): report 1 is computed from the same rows as report 2 — count,
/// total and average duration in seconds, the mean of the course-students column to one decimal rounded away from
/// zero — and every value is zero when the period has no meeting, while students are still listed with zeros.
/// </summary>
public sealed class MeetReportsSummaryTests
{
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

    private static string[] AddStudents(FakeMeetReportSource source, long course, int count)
    {
        var emails = new string[count];
        for (var i = 0; i < count; i++)
        {
            emails[i] = MeetReportsTestData.Email("student" + i.ToString(CultureInfo.InvariantCulture));
            var participant = source.AddParticipant(emails[i], "Student " + i.ToString(CultureInfo.InvariantCulture));
            source.AddMembership(course, participant, ClassroomRole.Student, Early, MeetReportsTestData.Now);
        }

        return emails;
    }

    /// <summary>AC-004: 45, 30 and 60 minutes with 2, 0 and 3 students — 3 meetings, 8100 s, 2700 s, 5/3 = 1.7.</summary>
    [Fact]
    public async Task Summary_IsDerivedFromTheMeetingRows()
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course");
        var students = AddStudents(source, course, 3);
        var organizer = MeetReportsTestData.Email("teacher");
        source.Link("aaa-aaaa-aaa", course);
        source.AddMeeting("aaa-aaaa-aaa", organizer, At(10, 5, 10), At(10, 5, 10, 45), (students[0], 600), (students[1], 600));
        source.AddMeeting("aaa-aaaa-aaa", organizer, At(10, 6, 10), At(10, 6, 10, 30));
        source.AddMeeting(
            "aaa-aaaa-aaa", organizer, At(10, 7, 10), At(10, 7, 11), (students[0], 600), (students[1], 600), (students[2], 600));

        var reports = await OctoberAsync(source, course);

        Assert.Equal(3, reports.Summary.MeetingCount);
        Assert.Equal(8100, reports.Summary.TotalDurationSeconds);
        Assert.Equal(2700, reports.Summary.AverageDurationSeconds);
        Assert.Equal(1.7m, reports.Summary.AverageStudentsPerMeeting);
        Assert.Equal(reports.Summary.MeetingCount, reports.Meetings.Count);
        Assert.Equal(reports.Summary.TotalDurationSeconds, reports.Meetings.Sum(m => m.DurationSeconds));
        Assert.Equal(new[] { 2, 0, 3 }, reports.Meetings.Select(m => m.CourseStudents));
    }

    /// <summary>I-6: the mean is rounded to one decimal away from zero — 1.25 becomes 1.3, not the banker's 1.2.</summary>
    [Fact]
    public async Task AverageStudents_RoundsAwayFromZero()
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course");
        var students = AddStudents(source, course, 2);
        var organizer = MeetReportsTestData.Email("teacher");
        source.Link("aaa-aaaa-aaa", course);
        source.AddMeeting("aaa-aaaa-aaa", organizer, At(10, 5, 10), At(10, 5, 11), (students[0], 600));
        source.AddMeeting("aaa-aaaa-aaa", organizer, At(10, 6, 10), At(10, 6, 11), (students[0], 600));
        source.AddMeeting("aaa-aaaa-aaa", organizer, At(10, 7, 10), At(10, 7, 11), (students[0], 600));
        source.AddMeeting("aaa-aaaa-aaa", organizer, At(10, 8, 10), At(10, 8, 11), (students[0], 600), (students[1], 600));

        var reports = await OctoberAsync(source, course);

        Assert.Equal(1.3m, reports.Summary.AverageStudentsPerMeeting);
    }

    /// <summary>AC-012: no meeting — every value zero, the list empty, the students still listed with zeros.</summary>
    [Fact]
    public async Task WithNoMeeting_SummaryIsZero_AndStudentsAreListedWithZeros()
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course");
        AddStudents(source, course, 2);

        var reports = await OctoberAsync(source, course);

        Assert.Equal(new MeetSummary(0, 0, 0, 0m), reports.Summary);
        Assert.Empty(reports.Meetings);
        Assert.Equal(2, reports.Students.Count);
        Assert.All(reports.Students, row =>
        {
            Assert.Equal(0, row.Meetings);
            Assert.Equal(0, row.TotalSeconds);
        });
    }

    /// <summary>AC-012: no student of the period — an empty student list (the page then shows the empty-state line).</summary>
    [Fact]
    public async Task WithNoStudents_ReportThreeIsEmpty()
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course");
        source.Link("aaa-aaaa-aaa", course);
        source.AddMeeting("aaa-aaaa-aaa", MeetReportsTestData.Email("teacher"), At(10, 5, 10), At(10, 5, 11));

        var reports = await OctoberAsync(source, course);

        Assert.Empty(reports.Students);
        Assert.Single(reports.Meetings);
    }
}
