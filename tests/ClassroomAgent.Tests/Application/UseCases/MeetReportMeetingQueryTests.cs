using System.Globalization;
using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-033 AC-007, AC-010, AC-013 (spec FR-010, FR-011, VR-003, I-1, I-3, I-11, api-design §2.4): report 4 — one meeting of
/// the course with its people in order, marks and capped seconds, the "other participants" line without identity, the
/// organizer named even without a connection, the back link, and the refusals (400 or 404) with no meeting read.
/// </summary>
public sealed class MeetReportMeetingQueryTests
{
    private const string Code = "aaa-aaaa-aaa";

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en");

    private static readonly DateTimeOffset Early = MeetReportsTestData.Period.Local(9, 1, 0);

    public static TheoryData<string> BadMeetingIds => new("0", "99999999999999999999");

    public static TheoryData<string> MalformedCourseIds => new("abc", "-1", "0", "1 ", "9223372036854775808");

    private static DateTimeOffset At(int month, int day, int hour, int minute = 0) =>
        MeetReportsTestData.Period.Local(month, day, hour, minute);

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static Task<MeetReportMeetingPageResult> RunAsync(FakeMeetReportSource source, MeetReportMeetingRequest request) =>
        new GetMeetReportMeetingQuery(source, new SchoolTimeZone(MeetReportsTestData.Kyiv))
            .ExecuteAsync(request, En, TestContext.Current.CancellationToken);

    private static async Task<MeetReportMeetingPageResult> ShownAsync(FakeMeetReportSource source, long course, long meeting)
    {
        var result = await RunAsync(source, new MeetReportMeetingRequest(Id(meeting), [Id(course)], [], []));

        Assert.Equal(MeetReportMeetingOutcome.Shown, result.Outcome);
        Assert.NotNull(result.Page.Meeting);
        return result;
    }

    private static (FakeMeetReportSource Source, long Course) CourseWithCode()
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course 9-A");
        source.Link(Code, course);
        return (source, course);
    }

    private static void AddMember(FakeMeetReportSource source, long course, string email, string name, ClassroomRole role)
    {
        var participant = source.AddParticipant(email, name);
        source.AddMembership(course, participant, role, Early, MeetReportsTestData.Now);
    }

    /// <summary>
    /// AC-010: organizer first, then course teachers, course students and not-on-list persons, each by name, with marks
    /// and capped seconds; the header carries course, code, local times and duration.
    /// </summary>
    [Fact]
    public async Task People_AreOrderedByGroup_ThenByName_WithMarks()
    {
        var (source, course) = CourseWithCode();
        var other = source.AddCourse("Other course");
        var organizer = MeetReportsTestData.Email("organizer");
        AddMember(source, course, organizer, "Olena Organizer", ClassroomRole.Teacher);
        AddMember(source, course, MeetReportsTestData.Email("t2"), "Zoya Teacher", ClassroomRole.Teacher);
        AddMember(source, course, MeetReportsTestData.Email("t1"), "Andrii Teacher", ClassroomRole.Teacher);
        AddMember(source, course, MeetReportsTestData.Email("s2"), "Yurii Student", ClassroomRole.Student);
        AddMember(source, course, MeetReportsTestData.Email("s1"), "Bohdan Student", ClassroomRole.Student);
        AddMember(source, other, MeetReportsTestData.Email("p"), "Petro Elsewhere", ClassroomRole.Student);
        var meeting = source.AddMeeting(
            Code,
            organizer,
            At(10, 12, 10),
            At(10, 12, 10, 45),
            (MeetReportsTestData.Email("p"), 300),
            (MeetReportsTestData.Email("s2"), 2000),
            (MeetReportsTestData.Email("s1"), 1000),
            (MeetReportsTestData.Email("t2"), 2400),
            (MeetReportsTestData.Email("t1"), 1800),
            (organizer, 2600));

        var result = await ShownAsync(source, course, meeting);

        var detail = result.Page.Meeting!;
        Assert.Equal("Course 9-A", detail.CourseName);
        Assert.Equal(Code, detail.MeetingCode);
        Assert.Equal(new DateTime(2026, 10, 12, 10, 0, 0), detail.StartedAt);
        Assert.Equal(new DateTime(2026, 10, 12, 10, 45, 0), detail.EndedAt);
        Assert.Equal(2700, detail.DurationSeconds);
        Assert.Equal(new PersonRef("Olena Organizer", organizer), detail.Organizer);
        Assert.False(detail.OrganizerNotCourseTeacher);
        Assert.Equal(
            new[]
            {
                new MeetingPersonLine(new PersonRef("Olena Organizer", organizer), MeetParticipantMark.Organizer, 2600),
                new MeetingPersonLine(
                    new PersonRef("Andrii Teacher", MeetReportsTestData.Email("t1")), MeetParticipantMark.CourseTeacher, 1800),
                new MeetingPersonLine(
                    new PersonRef("Zoya Teacher", MeetReportsTestData.Email("t2")), MeetParticipantMark.CourseTeacher, 2400),
                new MeetingPersonLine(
                    new PersonRef("Bohdan Student", MeetReportsTestData.Email("s1")), MeetParticipantMark.CourseStudent, 1000),
                new MeetingPersonLine(
                    new PersonRef("Yurii Student", MeetReportsTestData.Email("s2")), MeetParticipantMark.CourseStudent, 2000),
                new MeetingPersonLine(
                    new PersonRef("Petro Elsewhere", MeetReportsTestData.Email("p")), MeetParticipantMark.NotOnCourseList, 300),
            },
            detail.People);
    }

    /// <summary>
    /// AC-010, I-11: other participants are one line with the count (a class account once, each guest connection) and the
    /// total of their capped seconds; the class account's address is nowhere in the model.
    /// </summary>
    [Fact]
    public async Task OtherParticipants_AreOneLine_WithoutIdentity()
    {
        var (source, course) = CourseWithCode();
        var classAccount = MeetReportsTestData.Email("class-9a-secret");
        var meeting = source.AddMeeting(
            Code,
            MeetReportsTestData.Email("organizer"),
            At(10, 12, 10),
            At(10, 12, 10, 45),
            (classAccount, 3000),
            (classAccount, 100),
            (null, 500));

        var result = await ShownAsync(source, course, meeting);

        var detail = result.Page.Meeting!;
        Assert.Equal(new MeetOtherParticipants(2, 3300), detail.OtherParticipants);
        Assert.DoesNotContain(detail.People, p => p.Person.Email is not null && p.Person.Email.Contains("class-9a-secret", StringComparison.Ordinal));
        Assert.DoesNotContain("class-9a-secret", JsonSerializer.Serialize(result.Page), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>I-3: an organizer with no connection is named in the header and has no line in the people.</summary>
    [Fact]
    public async Task AnOrganizer_WithoutAConnection_IsNamedButHasNoLine()
    {
        var (source, course) = CourseWithCode();
        var organizer = MeetReportsTestData.Email("organizer");
        AddMember(source, course, organizer, "Olena Organizer", ClassroomRole.Teacher);
        AddMember(source, course, MeetReportsTestData.Email("s1"), "Bohdan Student", ClassroomRole.Student);
        var meeting = source.AddMeeting(
            Code, organizer, At(10, 12, 10), At(10, 12, 10, 45), (MeetReportsTestData.Email("s1"), 600));

        var result = await ShownAsync(source, course, meeting);

        var detail = result.Page.Meeting!;
        Assert.Equal(new PersonRef("Olena Organizer", organizer), detail.Organizer);
        Assert.DoesNotContain(detail.People, p => p.Mark == MeetParticipantMark.Organizer);
        Assert.Single(detail.People);
    }

    /// <summary>AC-007: an organizer who teaches only another course is marked "not a teacher of this course".</summary>
    [Fact]
    public async Task AnOrganizer_NotATeacherOfTheCourse_IsMarked()
    {
        var (source, course) = CourseWithCode();
        var other = source.AddCourse("Other course");
        var organizer = MeetReportsTestData.Email("organizer");
        AddMember(source, other, organizer, "Olena Organizer", ClassroomRole.Teacher);
        var meeting = source.AddMeeting(Code, organizer, At(10, 12, 10), At(10, 12, 10, 45), (organizer, 600));

        var result = await ShownAsync(source, course, meeting);

        Assert.True(result.Page.Meeting!.OrganizerNotCourseTeacher);
    }

    /// <summary>AC-007: a teacher organizer carries no mark.</summary>
    [Fact]
    public async Task AnOrganizer_WhoTeachesTheCourse_IsNotMarked()
    {
        var (source, course) = CourseWithCode();
        var organizer = MeetReportsTestData.Email("organizer");
        AddMember(source, course, organizer, "Olena Organizer", ClassroomRole.Teacher);
        var meeting = source.AddMeeting(Code, organizer, At(10, 12, 10), At(10, 12, 10, 45), (organizer, 600));

        var result = await ShownAsync(source, course, meeting);

        Assert.False(result.Page.Meeting!.OrganizerNotCourseTeacher);
    }

    /// <summary>OD-012, I-12: an organizer unknown to the roster is shown by email alone.</summary>
    [Fact]
    public async Task AnOrganizer_UnknownToTheRoster_IsShownByEmail()
    {
        var (source, course) = CourseWithCode();
        var organizer = MeetReportsTestData.Email("staff");
        var meeting = source.AddMeeting(Code, organizer, At(10, 12, 10), At(10, 12, 10, 45));

        var result = await ShownAsync(source, course, meeting);

        Assert.Equal(new PersonRef(null, organizer), result.Page.Meeting!.Organizer);
    }

    /// <summary>I-1: the meeting opens whatever the period, and the back link keeps a valid <c>from</c> and <c>to</c>.</summary>
    [Fact]
    public async Task AMeeting_OutsideTheGivenPeriod_Opens_AndTheBackLinkKeepsValidDates()
    {
        var (source, course) = CourseWithCode();
        var meeting = source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 12, 10), At(10, 12, 10, 45));

        var result = await RunAsync(
            source, new MeetReportMeetingRequest(Id(meeting), [Id(course)], ["2026-01-01"], ["2026-01-31"]));

        Assert.Equal(MeetReportMeetingOutcome.Shown, result.Outcome);
        Assert.NotNull(result.Page.Meeting);
        Assert.Null(result.Page.MessageKey);
        Assert.Equal(new MeetReportBackLink(course, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)), result.Page.BackLink);
    }

    /// <summary>api-design §2.4: invalid or absent <c>from</c>/<c>to</c> are dropped from the back link; a valid one stays.</summary>
    [Fact]
    public async Task TheBackLink_DropsInvalidDates()
    {
        var (source, course) = CourseWithCode();
        var meeting = source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 12, 10), At(10, 12, 10, 45));

        var badFrom = await RunAsync(
            source, new MeetReportMeetingRequest(Id(meeting), [Id(course)], ["2026-13-01"], ["2026-10-31"]));
        var badBoth = await RunAsync(
            source, new MeetReportMeetingRequest(Id(meeting), [Id(course)], ["01.10.2026"], ["2101-01-01"]));
        var absent = await RunAsync(source, new MeetReportMeetingRequest(Id(meeting), [Id(course)], [], []));

        Assert.Equal(new MeetReportBackLink(course, null, new DateOnly(2026, 10, 31)), badFrom.Page.BackLink);
        Assert.Equal(new MeetReportBackLink(course, null, null), badBoth.Page.BackLink);
        Assert.Equal(new MeetReportBackLink(course, null, null), absent.Page.BackLink);
        Assert.Equal(MeetReportMeetingOutcome.Shown, badFrom.Outcome);
        Assert.Equal(MeetReportMeetingOutcome.Shown, badBoth.Outcome);
        Assert.Equal(MeetReportMeetingOutcome.Shown, absent.Outcome);
    }

    /// <summary>AC-013: <c>courseId</c> is required on report 4 — absent is malformed, and nothing is read.</summary>
    [Fact]
    public async Task AbsentCourseId_IsInvalid()
    {
        var (source, _) = CourseWithCode();
        var meeting = source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 12, 10), At(10, 12, 10, 45));

        var result = await RunAsync(source, new MeetReportMeetingRequest(Id(meeting), [], [], []));

        Assert.Equal(MeetReportMeetingOutcome.Invalid, result.Outcome);
        Assert.Equal(MeetReportMeetingMessageKey.CourseMalformed, result.Page.MessageKey);
        Assert.Null(result.Page.Meeting);
        Assert.Equal(0, source.Calls);
    }

    /// <summary>AC-013: a malformed <c>courseId</c> is refused before anything is read.</summary>
    [Theory]
    [MemberData(nameof(MalformedCourseIds))]
    public async Task MalformedCourseId_IsInvalid(string value)
    {
        var (source, _) = CourseWithCode();
        var meeting = source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 12, 10), At(10, 12, 10, 45));

        var result = await RunAsync(source, new MeetReportMeetingRequest(Id(meeting), [value], [], []));

        Assert.Equal(MeetReportMeetingOutcome.Invalid, result.Outcome);
        Assert.Equal(MeetReportMeetingMessageKey.CourseMalformed, result.Page.MessageKey);
        Assert.Null(result.Page.Meeting);
        Assert.Equal(0, source.Calls);
    }

    /// <summary>AC-013: a repeated <c>courseId</c> is malformed.</summary>
    [Fact]
    public async Task RepeatedCourseId_IsInvalid()
    {
        var (source, course) = CourseWithCode();
        var meeting = source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 12, 10), At(10, 12, 10, 45));

        var result = await RunAsync(source, new MeetReportMeetingRequest(Id(meeting), [Id(course), Id(course)], [], []));

        Assert.Equal(MeetReportMeetingOutcome.Invalid, result.Outcome);
        Assert.Equal(MeetReportMeetingMessageKey.CourseMalformed, result.Page.MessageKey);
        Assert.Equal(0, source.Calls);
    }

    /// <summary>AC-013: a well-formed id of no stored course is "meeting not found" (404).</summary>
    [Fact]
    public async Task UnknownCourse_IsNotFound()
    {
        var (source, course) = CourseWithCode();
        var meeting = source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 12, 10), At(10, 12, 10, 45));

        var result = await RunAsync(source, new MeetReportMeetingRequest(Id(meeting), [Id(course + 1000)], [], []));

        Assert.Equal(MeetReportMeetingOutcome.NotFound, result.Outcome);
        Assert.Equal(MeetReportMeetingMessageKey.MeetingNotFound, result.Page.MessageKey);
        Assert.Null(result.Page.Meeting);
    }

    /// <summary>AC-013: a well-formed id of no stored meeting is not found.</summary>
    [Fact]
    public async Task UnknownMeeting_IsNotFound()
    {
        var (source, course) = CourseWithCode();
        var meeting = source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 12, 10), At(10, 12, 10, 45));

        var result = await RunAsync(source, new MeetReportMeetingRequest(Id(meeting + 1000), [Id(course)], [], []));

        Assert.Equal(MeetReportMeetingOutcome.NotFound, result.Outcome);
        Assert.Equal(MeetReportMeetingMessageKey.MeetingNotFound, result.Page.MessageKey);
        Assert.Null(result.Page.Meeting);
    }

    /// <summary>AC-013: a meeting of another course (or of a "not a course" code) is not found for this course.</summary>
    [Fact]
    public async Task AMeeting_OfAnotherCourse_IsNotFound()
    {
        var (source, course) = CourseWithCode();
        var other = source.AddCourse("Other course");
        source.Link("bbb-bbbb-bbb", other);
        source.MarkNotACourse("ccc-cccc-ccc");
        var organizer = MeetReportsTestData.Email("organizer");
        var ofOther = source.AddMeeting("bbb-bbbb-bbb", organizer, At(10, 12, 10), At(10, 12, 10, 45));
        var marked = source.AddMeeting("ccc-cccc-ccc", organizer, At(10, 13, 10), At(10, 13, 10, 45));

        var first = await RunAsync(source, new MeetReportMeetingRequest(Id(ofOther), [Id(course)], [], []));
        var second = await RunAsync(source, new MeetReportMeetingRequest(Id(marked), [Id(course)], [], []));

        Assert.Equal(MeetReportMeetingOutcome.NotFound, first.Outcome);
        Assert.Equal(MeetReportMeetingMessageKey.MeetingNotFound, first.Page.MessageKey);
        Assert.Equal(MeetReportMeetingOutcome.NotFound, second.Outcome);
        Assert.Null(second.Page.Meeting);
    }

    /// <summary>VR-003: a meeting id of zero or one that overflows 64 bits is answered as not found.</summary>
    [Theory]
    [MemberData(nameof(BadMeetingIds))]
    public async Task AnUnusableMeetingId_IsNotFound(string meetingId)
    {
        var (source, course) = CourseWithCode();
        source.AddMeeting(Code, MeetReportsTestData.Email("organizer"), At(10, 12, 10), At(10, 12, 10, 45));

        var result = await RunAsync(source, new MeetReportMeetingRequest(meetingId, [Id(course)], [], []));

        Assert.Equal(MeetReportMeetingOutcome.NotFound, result.Outcome);
        Assert.Equal(MeetReportMeetingMessageKey.MeetingNotFound, result.Page.MessageKey);
        Assert.Null(result.Page.Meeting);
    }
}
