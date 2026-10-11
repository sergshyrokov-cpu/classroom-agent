using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-033 AC-008, AC-012 (spec FR-009, I-10, OD-012): report 3 — the students of the period (on the roster for at
/// least part of it), the meetings they attended while on the roster and their capped seconds, a zero row for a
/// student who attended nothing, ordering by name in the UI language's collation and people without an email or name.
/// </summary>
public sealed class MeetReportsStudentsTests
{
    private const string Code = "aaa-aaaa-aaa";

    private static readonly DateTimeOffset Early = MeetReportsTestData.Period.Local(9, 1, 0);

    private static DateTimeOffset At(int month, int day, int hour, int minute = 0) =>
        MeetReportsTestData.Period.Local(month, day, hour, minute);

    private static async Task<MeetReports> OctoberAsync(FakeMeetReportSource source, long course, string culture = "uk")
    {
        var query = new GetMeetReportsQuery(
            source.Courses,
            source,
            new GetLastSynchronizationQuery(new SyncWorld.StateRepository(), new SchoolTimeZone(MeetReportsTestData.Kyiv)),
            new SchoolTimeZone(MeetReportsTestData.Kyiv),
            new ManualTimeProvider(MeetReportsTestData.Now));
        var request = new MeetReportsRequest(
            [course.ToString(CultureInfo.InvariantCulture)],
            [MeetReportsTestData.Period.FromText],
            [MeetReportsTestData.Period.ToText]);

        var result = await query.ExecuteAsync(request, CultureInfo.GetCultureInfo(culture), TestContext.Current.CancellationToken);

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

    private static void AddStudent(
        FakeMeetReportSource source,
        long course,
        string? email,
        string? name,
        DateTimeOffset? firstSeenAt = null,
        DateTimeOffset? lastSeenAt = null,
        bool onRoster = true,
        ClassroomRole role = ClassroomRole.Student)
    {
        var participant = source.AddParticipant(email, name);
        source.AddMembership(course, participant, role, firstSeenAt ?? Early, lastSeenAt ?? MeetReportsTestData.Now, onRoster);
    }

    /// <summary>
    /// AC-008: S1 attended two meetings, S2 none, S3 left in the middle of the period and attended one meeting before and
    /// one after leaving — only the on-roster meeting counts for S3, with its seconds.
    /// </summary>
    [Fact]
    public async Task Students_AreCountedWhileOnTheRoster()
    {
        var (source, course) = CourseWithCode();
        var s1 = MeetReportsTestData.Email("s1");
        var s2 = MeetReportsTestData.Email("s2");
        var s3 = MeetReportsTestData.Email("s3");
        AddStudent(source, course, s1, "Anna One");
        AddStudent(source, course, s2, "Boris Two");
        AddStudent(source, course, s3, "Clara Three", lastSeenAt: At(10, 10, 12), onRoster: false);
        var organizer = MeetReportsTestData.Email("organizer");
        source.AddMeeting(Code, organizer, At(10, 8, 10), At(10, 8, 10, 45), (s1, 1200), (s3, 900));
        source.AddMeeting(Code, organizer, At(10, 15, 10), At(10, 15, 10, 45), (s1, 600), (s3, 1500));

        var reports = await OctoberAsync(source, course);

        Assert.Equal(
            new[]
            {
                new StudentParticipationRow(new PersonRef("Anna One", s1), 2, 1800),
                new StudentParticipationRow(new PersonRef("Boris Two", s2), 0, 0),
                new StudentParticipationRow(new PersonRef("Clara Three", s3), 1, 900),
            },
            reports.Students);
    }

    /// <summary>AC-008: a student who left before the period, one first seen after it, and a teacher are not rows.</summary>
    [Fact]
    public async Task OnlyStudentsOfThePeriod_AreRows()
    {
        var (source, course) = CourseWithCode();
        AddStudent(source, course, MeetReportsTestData.Email("inside"), "Inside Student");
        AddStudent(
            source,
            course,
            MeetReportsTestData.Email("gone"),
            "Gone Student",
            lastSeenAt: At(9, 20, 12),
            onRoster: false);
        AddStudent(
            source,
            course,
            MeetReportsTestData.Email("future"),
            "Future Student",
            firstSeenAt: At(11, 3, 12));
        AddStudent(source, course, MeetReportsTestData.Email("teacher"), "Some Teacher", role: ClassroomRole.Teacher);

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Students);
        Assert.Equal("Inside Student", row.Student.FullName);
    }

    /// <summary>I-10: Ukrainian collation puts Андрій, Євген, Іван, Ярослав in that order — not by code point.</summary>
    [Fact]
    public async Task Students_AreOrderedByName_InUkrainianCollation()
    {
        var (source, course) = CourseWithCode();
        AddStudent(source, course, MeetReportsTestData.Email("y"), "Ярослав");
        AddStudent(source, course, MeetReportsTestData.Email("i"), "Іван");
        AddStudent(source, course, MeetReportsTestData.Email("ye"), "Євген");
        AddStudent(source, course, MeetReportsTestData.Email("a"), "Андрій");

        var reports = await OctoberAsync(source, course, "uk");

        Assert.Equal(
            new[] { "Андрій", "Євген", "Іван", "Ярослав" },
            reports.Students.Select(s => s.Student.FullName));
    }

    /// <summary>I-10: English collation ignores case differences in the first letter; a tie on name is broken by email.</summary>
    [Fact]
    public async Task Students_AreOrderedByName_InEnglish_AndTiesByEmail()
    {
        var (source, course) = CourseWithCode();
        AddStudent(source, course, MeetReportsTestData.Email("c"), "Charlie");
        AddStudent(source, course, MeetReportsTestData.Email("b2"), "Same Name");
        AddStudent(source, course, MeetReportsTestData.Email("a"), "alice");
        AddStudent(source, course, MeetReportsTestData.Email("b1"), "Same Name");

        var reports = await OctoberAsync(source, course, "en");

        Assert.Equal(
            new[]
            {
                new PersonRef("alice", MeetReportsTestData.Email("a")),
                new PersonRef("Charlie", MeetReportsTestData.Email("c")),
                new PersonRef("Same Name", MeetReportsTestData.Email("b1")),
                new PersonRef("Same Name", MeetReportsTestData.Email("b2")),
            },
            reports.Students.Select(s => s.Student));
    }

    /// <summary>OD-012: a student without an email is a row with a null email and zeros.</summary>
    [Fact]
    public async Task AStudent_WithoutEmail_IsARowWithNullEmail()
    {
        var (source, course) = CourseWithCode();
        AddStudent(source, course, null, "Nameless Mail");

        var reports = await OctoberAsync(source, course);

        var row = Assert.Single(reports.Students);
        Assert.Equal(new PersonRef("Nameless Mail", null), row.Student);
        Assert.Equal(0, row.Meetings);
        Assert.Equal(0, row.TotalSeconds);
    }

    /// <summary>OD-012: a student with neither email nor name is <c>PersonRef(null, null)</c>, the page shows "unnamed".</summary>
    [Fact]
    public async Task AStudent_WithoutEmailAndName_IsAnEmptyPersonRef()
    {
        var (source, course) = CourseWithCode();
        AddStudent(source, course, null, null);

        var reports = await OctoberAsync(source, course);

        Assert.Equal(new PersonRef(null, null), Assert.Single(reports.Students).Student);
    }

    /// <summary>FR-011, I-12: a student with an email but no full name is shown by email alone.</summary>
    [Fact]
    public async Task AStudent_WithoutName_IsShownByEmail()
    {
        var (source, course) = CourseWithCode();
        var email = MeetReportsTestData.Email("anon");
        AddStudent(source, course, email, null);

        var reports = await OctoberAsync(source, course);

        Assert.Equal(new PersonRef(null, email), Assert.Single(reports.Students).Student);
    }
}
