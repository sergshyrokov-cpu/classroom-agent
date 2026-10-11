using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-033 AC-002, AC-003, AC-018 (spec FR-003, FR-004, TC-8): which stored meetings are the meetings of the report —
/// those whose code is linked to the course (automatic or confirmed alike, never a "not a course" mark or an unlinked
/// code) and whose start lies in the half-open interval of the school's local days, in <c>Europe/Kyiv</c>.
/// </summary>
public sealed class MeetReportsMeetingsOfCourseTests
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk");

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

    /// <summary>AC-002: codes A (automatic) and B (confirmed) of the course, C marked "not a course", D unlinked.</summary>
    [Fact]
    public async Task OnlyMeetings_OfCodesLinkedToTheCourse_AreListed()
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course");
        var other = source.AddCourse("Other course");
        var organizer = MeetReportsTestData.Email("teacher");
        source.Link("aaa-aaaa-aaa", course);
        source.Link("bbb-bbbb-bbb", course);
        source.Link("eee-eeee-eee", other);
        source.MarkNotACourse("ccc-cccc-ccc");
        var a = source.AddMeeting("aaa-aaaa-aaa", organizer, At(10, 5, 10), At(10, 5, 11));
        var b = source.AddMeeting("bbb-bbbb-bbb", organizer, At(10, 6, 10), At(10, 6, 11));
        source.AddMeeting("ccc-cccc-ccc", organizer, At(10, 7, 10), At(10, 7, 11));
        source.AddMeeting("ddd-dddd-ddd", organizer, At(10, 8, 10), At(10, 8, 11));
        source.AddMeeting("eee-eeee-eee", organizer, At(10, 9, 10), At(10, 9, 11));

        var reports = await OctoberAsync(source, course);

        Assert.Equal(new[] { a, b }, reports.Meetings.Select(m => m.MeetingId));
        Assert.Equal(2, reports.Summary.MeetingCount);
    }

    /// <summary>
    /// AC-003: a meeting starting 23:30 on the last day ends after midnight yet is in wholly (its full 70 minutes count
    /// in the summary); one starting 00:10 on the next day is out.
    /// </summary>
    [Fact]
    public async Task AMeeting_StartingOnTheLastDay_CountsWholly_AndOneStartingAfterMidnightIsOut()
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course");
        var organizer = MeetReportsTestData.Email("teacher");
        source.Link("aaa-aaaa-aaa", course);
        source.Link("bbb-bbbb-bbb", course);
        var inside = source.AddMeeting("aaa-aaaa-aaa", organizer, At(10, 31, 23, 30), At(11, 1, 0, 40));
        source.AddMeeting("bbb-bbbb-bbb", organizer, At(11, 1, 0, 10), At(11, 1, 1, 0));

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Meetings);
        Assert.Equal(inside, row.MeetingId);
        Assert.Equal(70 * 60, row.DurationSeconds);
        Assert.Equal(70 * 60, reports.Summary.TotalDurationSeconds);
    }

    /// <summary>
    /// AC-018: 00:30 Kyiv on 2026-11-01 is 22:30 UTC on 10-31 — still "to" in UTC but outside the report; 00:30 Kyiv on
    /// 2026-10-01 is 21:30 UTC on 09-30 — inside. The source is asked for exactly <c>[StartUtc, EndUtc)</c>.
    /// </summary>
    [Fact]
    public async Task PeriodBoundaries_FollowTheSchoolsLocalDays_NotUtc()
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course");
        var organizer = MeetReportsTestData.Email("teacher");
        source.Link("aaa-aaaa-aaa", course);
        source.Link("bbb-bbbb-bbb", course);
        var inside = source.AddMeeting("aaa-aaaa-aaa", organizer, At(10, 1, 0, 30), At(10, 1, 1, 15));
        source.AddMeeting("bbb-bbbb-bbb", organizer, At(11, 1, 0, 30), At(11, 1, 1, 15));

        var reports = await OctoberAsync(source, course);

        Assert.Equal(new[] { inside }, reports.Meetings.Select(m => m.MeetingId));
        var bounds = Assert.Single(source.Bounds);
        Assert.Equal(MeetReportsTestData.Period.StartUtc, bounds.Start);
        Assert.Equal(MeetReportsTestData.Period.EndUtc, bounds.EndExclusive);
    }

    /// <summary>FR-008: rows are ordered by start ascending, then by id; the dates carried are local Kyiv times.</summary>
    [Fact]
    public async Task Meetings_AreOrderedByStart_AndCarryLocalTimes()
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course");
        var organizer = MeetReportsTestData.Email("teacher");
        source.Link("aaa-aaaa-aaa", course);
        var later = source.AddMeeting("aaa-aaaa-aaa", organizer, At(10, 12, 10), At(10, 12, 10, 45));
        var earlier = source.AddMeeting("aaa-aaaa-aaa", organizer, At(10, 3, 9), At(10, 3, 9, 30));

        var reports = await OctoberAsync(source, course);

        Assert.Equal(new[] { earlier, later }, reports.Meetings.Select(m => m.MeetingId));
        Assert.Equal(new DateTime(2026, 10, 3, 9, 0, 0), reports.Meetings[0].StartedAt);
        Assert.Equal(new DateTime(2026, 10, 3, 9, 30, 0), reports.Meetings[0].EndedAt);
        Assert.Equal(30 * 60, reports.Meetings[0].DurationSeconds);
    }
}
