using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-021 AC-001 … AC-006, AC-008 … AC-010, AC-014, AC-017 (spec FR-002 … FR-008, FR-010, FR-011, FR-014, FR-015,
/// VR-001; entity model §5): the parse of the path value, the outcome for a malformed and an unknown id, the details
/// with blank values dropped and times in the school's zone, the three sections in the port's order, the owner mark,
/// the leaver's last-seen date, the name rule, the return path and a fixed number of reads whatever the roster size.
/// The port is the in-memory <see cref="FakeCourseCardSource"/>; ordering inside the database is proved against
/// PostgreSQL in <c>CourseCardSourceTests</c>.
/// </summary>
public sealed class CourseCardQueryTests
{
    private static readonly DateTimeOffset Seen = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    public static TheoryData<string?> MalformedIds => new(
        null, string.Empty, "0", "000", "-1", "+1", " 1", "1 ", "1.0", "1e3", "1,000", "abc", "0x1F", "١٢", "１",
        "9223372036854775808", "99999999999999999999");

    public static TheoryData<string?> BlankValues => new(null, string.Empty, " ", "\t\n ");

    private static Task<CourseCardResult> RunAsync(FakeCourseCardSource source, string? courseId) =>
        new GetCourseCardQuery(source, new SchoolTimeZone(CourseCardTestData.Kyiv)).ExecuteAsync(
            new CourseCardRequest(courseId), TestContext.Current.CancellationToken);

    private static Task<CourseCardResult> RunAsync(FakeCourseCardSource source, long courseId) =>
        RunAsync(source, CourseCardTestData.Id(courseId));

    private static CourseCardPageModel Shown(CourseCardResult result)
    {
        Assert.Equal(CourseCardOutcome.Shown, result.Outcome);
        return Assert.IsType<CourseCardPageModel>(result.Page);
    }

    private static CourseListPersonName Profile(string text) => new(CourseListPersonNameKind.ProfileName, text);

    /// <summary>AC-008, VR-001, FR-011, S-05: anything but a positive Int64 of ASCII digits is malformed, and nothing is read.</summary>
    [Theory]
    [MemberData(nameof(MalformedIds))]
    public async Task AMalformedId_IsCourseMalformed_AndNothingIsRead(string? value)
    {
        var source = new FakeCourseCardSource();
        source.AddCourse();

        var result = await RunAsync(source, value);

        Assert.Equal(CourseCardOutcome.CourseMalformed, result.Outcome);
        Assert.Null(result.Page);
        Assert.Equal(0, source.TotalCalls);
    }

    /// <summary>AC-008, VR-001: Int64.MaxValue is well formed; no course has it, so it is unknown, and the members are not read.</summary>
    [Fact]
    public async Task TheLargestId_IsWellFormed_AndUnknown()
    {
        var source = new FakeCourseCardSource();
        source.AddCourse();

        var result = await RunAsync(source, "9223372036854775807");

        Assert.Equal(CourseCardOutcome.CourseUnknown, result.Outcome);
        Assert.Null(result.Page);
        Assert.Equal([long.MaxValue], source.CourseRequests);
        Assert.Empty(source.MemberRequests);
    }

    /// <summary>AC-008, FR-011, I-9: a well-formed id of no course is unknown; Q2 is not run.</summary>
    [Fact]
    public async Task AnUnknownId_IsCourseUnknown_AndTheMembersAreNotRead()
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();

        var result = await RunAsync(source, course + 1);

        Assert.Equal(CourseCardOutcome.CourseUnknown, result.Outcome);
        Assert.Null(result.Page);
        Assert.Equal([course + 1], source.CourseRequests);
        Assert.Empty(source.MemberRequests);
    }

    /// <summary>I-6, FR-010: leading zeros open the course; the return path is the canonical card URL of the parsed id.</summary>
    [Fact]
    public async Task LeadingZeros_OpenTheCourse_AndTheReturnPathIsCanonical()
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();

        var page = Shown(await RunAsync(source, "000" + CourseCardTestData.Id(course)));

        Assert.Equal(course, page.Course.CourseId);
        Assert.Equal(CourseCardTestData.Path(course), page.ReturnPath);
        Assert.Equal([course], source.CourseRequests);
    }

    /// <summary>FR-010: the return path is the card URL of the course, nothing else.</summary>
    [Fact]
    public async Task TheReturnPath_IsTheCardUrl()
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();

        var page = Shown(await RunAsync(source, course));

        Assert.Equal("/workspace/courses/" + CourseCardTestData.Id(course), page.ReturnPath);
    }

    /// <summary>
    /// AC-001, AC-014, FR-002: the details are the stored values, unchanged (never trimmed or re-cased); the times are in
    /// the school's zone to the minute — 22:30 UTC on 31 March 2026 is 01:30 on 1 April in Kyiv.
    /// </summary>
    [Fact]
    public async Task TheDetails_AreTheStoredValues_WithTheTimesInTheSchoolsZone()
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse(
            "фізика ІСТ ",
            "9-б ",
            CourseState.Archived,
            " Кабінет 214",
            "Про курс",
            "Рядок перший\nРядок другий",
            new DateTimeOffset(2025, 9, 1, 6, 15, 42, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 31, 22, 30, 59, TimeSpan.Zero));

        var page = Shown(await RunAsync(source, course));

        Assert.Equal(
            new CourseCardDetails(
                course,
                "фізика ІСТ ",
                "9-б ",
                CourseState.Archived,
                " Кабінет 214",
                "Про курс",
                "Рядок перший\nРядок другий",
                new DateTime(2025, 9, 1, 9, 15, 0),
                new DateTime(2026, 4, 1, 1, 30, 0)),
            page.Course);
        Assert.Equal(DateTimeKind.Unspecified, page.Course.CreatedAt!.Value.Kind);
    }

    /// <summary>AC-001, FR-002, I-1: a value Google did not give — null, empty or white space only — is null; no time is null.</summary>
    [Theory]
    [MemberData(nameof(BlankValues))]
    public async Task ABlankOptionalValue_IsNotGiven(string? blank)
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse("Фізика", blank, CourseState.Active, blank, blank, blank);

        var page = Shown(await RunAsync(source, course));

        Assert.Equal(
            new CourseCardDetails(course, "Фізика", null, CourseState.Active, null, null, null, null, null),
            page.Course);
    }

    /// <summary>
    /// AC-002, AC-004, FR-003: teachers and students on the roster land in their sections, every off-roster membership of
    /// either role under "Left the course", each exactly once, each section keeping the port's order.
    /// </summary>
    [Fact]
    public async Task TheMembers_AreSplitIntoTheThreeSections_InThePortsOrder()
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();
        var leftStudent = source.AddMember(course, "Яковенко", "Ольга", null, ClassroomRole.Student, onRoster: false);
        var teacherB = source.AddMember(course, "Ярема", "Іван", null, ClassroomRole.Teacher);
        var studentB = source.AddMember(course, "Бондар", "Анна", null);
        var leftTeacher = source.AddMember(course, "Антоненко", "Петро", null, ClassroomRole.Teacher, onRoster: false);
        var teacherA = source.AddMember(course, "Андрієнко", "Марія", null, ClassroomRole.Teacher);
        var studentA = source.AddMember(course, "Яценко", "Олег", null);

        var page = Shown(await RunAsync(source, course));

        Assert.Equal([teacherB, teacherA], page.Teachers.Select(t => t.ParticipantId));
        Assert.Equal([studentB, studentA], page.Students.Select(s => s.ParticipantId));
        Assert.Equal([leftStudent, leftTeacher], page.Leavers.Select(l => l.ParticipantId));
        Assert.Equal([ClassroomRole.Student, ClassroomRole.Teacher], page.Leavers.Select(l => l.Role));
    }

    /// <summary>AC-003, FR-004, I-7: the owner mark comes from the port and is kept only on a current teacher.</summary>
    [Fact]
    public async Task OnlyACurrentTeacher_CarriesTheOwnerMark()
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();
        var owner = source.AddMember(course, "Шевченко", "Тарас", null, ClassroomRole.Teacher, isOwner: true);
        var coTeacher = source.AddMember(course, "Українка", "Леся", null, ClassroomRole.Teacher);

        var page = Shown(await RunAsync(source, course));

        Assert.Equal([(owner, true), (coTeacher, false)], page.Teachers.Select(t => (t.ParticipantId, t.IsOwner)));
    }

    /// <summary>AC-003, FR-004: an owner who is a student or a leaver marks no one; the teachers stay unmarked.</summary>
    [Theory]
    [InlineData(ClassroomRole.Student, true)]
    [InlineData(ClassroomRole.Teacher, false)]
    [InlineData(ClassroomRole.Student, false)]
    public async Task AnOwnerWhoIsNotACurrentTeacher_MarksNoOne(ClassroomRole role, bool onRoster)
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();
        var owner = source.AddMember(course, "Шевченко", "Тарас", null, role, onRoster, isOwner: true);
        source.AddMember(course, "Українка", "Леся", null, ClassroomRole.Teacher);

        var page = Shown(await RunAsync(source, course));

        Assert.All(page.Teachers, t => Assert.False(t.IsOwner));
        Assert.DoesNotContain(owner, page.Teachers.Select(t => t.ParticipantId));
        Assert.Contains(owner, page.Students.Select(s => s.ParticipantId).Concat(page.Leavers.Select(l => l.ParticipantId)));
    }

    /// <summary>AC-004, AC-014, FR-006, I-5: a leaver's last seen is the school's local date — 22:30 UTC on 31 March is 1 April.</summary>
    [Fact]
    public async Task ALeaversLastSeen_IsTheSchoolsLocalDate()
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();
        source.AddMember(
            course, "Франко", "Іван", null, ClassroomRole.Teacher, onRoster: false,
            lastSeenAt: new DateTimeOffset(2026, 3, 31, 22, 30, 0, TimeSpan.Zero));
        source.AddMember(
            course, "Мельник", "Петро", null, ClassroomRole.Student, onRoster: false,
            lastSeenAt: new DateTimeOffset(2026, 2, 10, 10, 0, 0, TimeSpan.Zero));

        var page = Shown(await RunAsync(source, course));

        Assert.Equal([new DateOnly(2026, 4, 1), new DateOnly(2026, 2, 10)], page.Leavers.Select(l => l.LastSeenOn));
    }

    /// <summary>
    /// AC-004, FR-005, FR-006: a student created from submissions — no name, no email, off the roster — is a leaver with
    /// the "no name" kind, no email, the student role and that synchronization's day.
    /// </summary>
    [Fact]
    public async Task ASubmitterOnlyStudent_IsALeaverWithNoName()
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();
        var submitter = source.AddMember(
            course, null, null, null, ClassroomRole.Student, onRoster: false,
            lastSeenAt: new DateTimeOffset(2026, 3, 15, 8, 0, 0, TimeSpan.Zero));

        var page = Shown(await RunAsync(source, course));

        Assert.Empty(page.Students);
        Assert.Equal(
            new CourseCardLeaver(
                submitter,
                new CourseListPersonName(CourseListPersonNameKind.NoName, null),
                null,
                ClassroomRole.Student,
                new DateOnly(2026, 3, 15)),
            Assert.Single(page.Leavers));
    }

    /// <summary>AC-005, FR-005: "Прізвище Ім'я" as stored, one part alone, else the full email, else no name; the email beside it as stored.</summary>
    [Theory]
    [InlineData("Шевченко", "Тарас", "t.shevchenko@school.example", CourseListPersonNameKind.ProfileName, "Шевченко Тарас")]
    [InlineData("де ла Круз", "марія", null, CourseListPersonNameKind.ProfileName, "де ла Круз марія")]
    [InlineData("Шевченко", null, null, CourseListPersonNameKind.ProfileName, "Шевченко")]
    [InlineData(null, "Тарас", null, CourseListPersonNameKind.ProfileName, "Тарас")]
    [InlineData("", "Тарас", null, CourseListPersonNameKind.ProfileName, "Тарас")]
    [InlineData("Шевченко", "", null, CourseListPersonNameKind.ProfileName, "Шевченко")]
    [InlineData(null, null, "T.Shevchenko@School.example", CourseListPersonNameKind.Email, "T.Shevchenko@School.example")]
    [InlineData("", "", "t.shevchenko@school.example", CourseListPersonNameKind.Email, "t.shevchenko@school.example")]
    [InlineData(null, null, null, CourseListPersonNameKind.NoName, null)]
    [InlineData("", "", "", CourseListPersonNameKind.NoName, null)]
    public async Task TheName_FollowsTheRule_InEverySection(
        string? surname, string? givenName, string? email, CourseListPersonNameKind kind, string? text)
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();
        source.AddMember(course, surname, givenName, email, ClassroomRole.Teacher);
        source.AddMember(course, surname, givenName, email);
        source.AddMember(course, surname, givenName, email, ClassroomRole.Student, onRoster: false);

        var page = Shown(await RunAsync(source, course));

        var expected = new CourseListPersonName(kind, text);
        Assert.Equal(expected, Assert.Single(page.Teachers).Name);
        Assert.Equal(expected, Assert.Single(page.Students).Name);
        Assert.Equal(expected, Assert.Single(page.Leavers).Name);
        if (!string.IsNullOrEmpty(email))
        {
            Assert.Equal(email, page.Teachers[0].Email);
            Assert.Equal(email, page.Students[0].Email);
            Assert.Equal(email, page.Leavers[0].Email);
        }
    }

    /// <summary>FR-005: a person without an email has no email beside the name.</summary>
    [Fact]
    public async Task APersonWithoutAnEmail_HasNoEmail()
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();
        source.AddMember(course, "Шевченко", "Тарас", null, ClassroomRole.Teacher);

        var page = Shown(await RunAsync(source, course));

        Assert.Equal(
            new CourseCardTeacher(page.Teachers[0].ParticipantId, Profile("Шевченко Тарас"), null, false),
            Assert.Single(page.Teachers));
    }

    /// <summary>AC-009, FR-008: a course with nobody has three empty sections, and that is not an error.</summary>
    [Fact]
    public async Task ACourseWithNobody_HasThreeEmptySections()
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();

        var page = Shown(await RunAsync(source, course));

        Assert.Empty(page.Teachers);
        Assert.Empty(page.Students);
        Assert.Empty(page.Leavers);
    }

    /// <summary>AC-017, FR-015: a shown card is two reads — the course and its members — for 3 members and for 350 alike.</summary>
    [Theory]
    [InlineData(3, 0)]
    [InlineData(300, 50)]
    public async Task ACard_IsTwoReads_WhateverTheRosterSize(int members, int leavers)
    {
        var source = new FakeCourseCardSource();
        var course = source.AddCourse();
        for (var i = 0; i < members; i++)
        {
            source.AddMember(course, $"Учень{i:D3}", "Ім'я", null);
        }

        for (var i = 0; i < leavers; i++)
        {
            source.AddMember(course, $"Вибув{i:D3}", "Ім'я", null, ClassroomRole.Student, onRoster: false, lastSeenAt: Seen);
        }

        var page = Shown(await RunAsync(source, course));

        Assert.Equal(members, page.Students.Count);
        Assert.Equal(leavers, page.Leavers.Count);
        Assert.Equal([course], source.CourseRequests);
        Assert.Equal([course], source.MemberRequests);
    }

    /// <summary>
    /// AC-010, FR-014, S-04: the query depends on its read port and the school's time zone only — no read-only guard,
    /// no unit of work, no Google port, no secret store — so it reads the same in every mode and can write nothing.
    /// </summary>
    [Fact]
    public void TheQuery_DependsOnTheReadPortAndTheTimeZoneOnly()
    {
        var constructor = Assert.Single(typeof(GetCourseCardQuery).GetConstructors());

        Assert.Equal(
            [typeof(ICourseCardSource), typeof(SchoolTimeZone)],
            constructor.GetParameters().Select(p => p.ParameterType));
    }
}
