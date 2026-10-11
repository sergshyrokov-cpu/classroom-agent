using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-033 db-design §3 and §4, AC-002, AC-018 and R1–R6 over real PostgreSQL (TC-2): the course filter through link rows,
/// the "not a course" mark, the half-open period, every role of the roster, lower-case matching, the tie rule and the
/// personal-account flag. Rows are inserted by SQL.
/// </summary>
public sealed class MeetReportSourceTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset Start = MeetReportsTestData.Period.StartUtc;

    private static readonly DateTimeOffset EndExclusive = MeetReportsTestData.Period.EndUtc;

    /// <summary>R6: the name of an existing course, null for an id that names none.</summary>
    [Fact]
    public async Task GetCourseName_ReturnsTheName_OrNullForAnUnknownCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct, name: "Test Course Named");

        await host.WithMeetReportSourceAsync(async source =>
        {
            Assert.Equal("Test Course Named", await source.GetCourseNameAsync(course, ct));
            Assert.Null(await source.GetCourseNameAsync(course + 1000, ct));
        });
    }

    /// <summary>AC-002, R1: an automatic unconfirmed link and a confirmed one count; a "not a course" mark, an unlinked code and another course's code do not.</summary>
    [Fact]
    public async Task GetMeetings_ReturnsTheMeetingsOfTheLinkedCodesOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedWorldAsync(ct);

        await host.WithMeetReportSourceAsync(async source =>
        {
            var meetings = await source.GetMeetingsAsync(world.CourseId, Start, EndExclusive, ct);

            Assert.Equal(
                new[] { world.MeetingA, world.MeetingB }.Order().ToArray(),
                meetings.Select(m => m.Id).Order().ToArray());
            var other = await source.GetMeetingsAsync(world.OtherCourseId, Start, EndExclusive, ct);
            Assert.Equal([world.MeetingElsewhere], other.Select(m => m.Id).ToArray());
        });
    }

    /// <summary>R1, R2: a meeting carries its code, organizer, times and every connection with its stored email and duration.</summary>
    [Fact]
    public async Task GetMeetings_CarriesTheMeetingAndItsConnections()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedWorldAsync(ct);

        await host.WithMeetReportSourceAsync(async source =>
        {
            var meetings = await source.GetMeetingsAsync(world.CourseId, Start, EndExclusive, ct);

            var a = Assert.Single(meetings, m => m.Id == world.MeetingA);
            Assert.Equal(Mr.Codes.Linked, a.MeetingCode);
            Assert.Equal(Mr.TeacherEmail, a.OrganizerEmail, ignoreCase: true);
            Assert.Equal(MeetReportsTestData.Period.Local(10, 5, 10), a.StartedAt);
            Assert.Equal(MeetReportsTestData.Period.Local(10, 5, 10).AddMinutes(45), a.EndedAt);
            Assert.Equal(7, a.Connections.Count);
            Assert.Equal(2, a.Connections.Count(c => c.Email is not null && string.Equals(c.Email, Mr.StudentOneEmail, StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(4800, a.Connections.Where(c => string.Equals(c.Email, Mr.StudentOneEmail, StringComparison.OrdinalIgnoreCase)).Sum(c => c.DurationSeconds));
            var guest = Assert.Single(a.Connections, c => c.Email is null);
            Assert.Equal(300, guest.DurationSeconds);
            var b = Assert.Single(meetings, m => m.Id == world.MeetingB);
            Assert.Equal(Mr.OtherTeacherEmail, b.OrganizerEmail, ignoreCase: true);
            Assert.Single(b.Connections);
        });
    }

    /// <summary>AC-018, R1: the interval is half open — a start equal to the first instant is in, a start equal to the exclusive end is out.</summary>
    [Fact]
    public async Task GetMeetings_UsesAHalfOpenIntervalOnTheStart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await MeetLinkRows.InsertLinkAsync(
            host, MeetLinkRows.LinkRow.Automatic(Mr.Codes.Linked, course, Start), ct);
        var atStart = await Mr.InsertMeetingAsync(host, Mr.Codes.Linked, Mr.TeacherEmail, Start, TimeSpan.FromMinutes(10), ct);
        var beforeStart = await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, Start.AddTicks(-TimeSpan.TicksPerSecond), TimeSpan.FromMinutes(10), ct);
        var lastInside = await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, EndExclusive.AddSeconds(-1), TimeSpan.FromHours(2), ct);
        var atEnd = await Mr.InsertMeetingAsync(host, Mr.Codes.Linked, Mr.TeacherEmail, EndExclusive, TimeSpan.FromMinutes(10), ct);

        await host.WithMeetReportSourceAsync(async source =>
        {
            var meetings = await source.GetMeetingsAsync(course, Start, EndExclusive, ct);

            Assert.Equal(
                new[] { atStart, lastInside }.Order().ToArray(),
                meetings.Select(m => m.Id).Order().ToArray());
            Assert.DoesNotContain(meetings, m => m.Id == beforeStart || m.Id == atEnd);
        });
    }

    /// <summary>R1: a meeting that starts in the period and ends after it still belongs to it, wholly.</summary>
    [Fact]
    public async Task GetMeetings_KeepsAMeetingThatEndsAfterThePeriod_Whole()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await MeetLinkRows.InsertLinkAsync(host, MeetLinkRows.LinkRow.Automatic(Mr.Codes.Linked, course, Start), ct);
        var started = MeetReportsTestData.Period.Local(10, 31, 23, 30);
        var meeting = await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, started, TimeSpan.FromMinutes(70), ct, new Mr.Connection(Mr.StudentOneEmail, 4200));

        await host.WithMeetReportSourceAsync(async source =>
        {
            var meetings = await source.GetMeetingsAsync(course, Start, EndExclusive, ct);

            var found = Assert.Single(meetings);
            Assert.Equal(meeting, found.Id);
            Assert.Equal(started.AddMinutes(70), found.EndedAt);
            Assert.Equal(4200, Assert.Single(found.Connections).DurationSeconds);
        });
    }

    /// <summary>R3: a meeting of the course is returned with its connections; null for another course's, a marked code's, an unlinked code's and an unknown id.</summary>
    [Fact]
    public async Task GetMeetingOfCourse_ReturnsTheMeeting_OrNullWhenItIsNotOfTheCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedWorldAsync(ct);

        await host.WithMeetReportSourceAsync(async source =>
        {
            var found = await source.GetMeetingOfCourseAsync(world.CourseId, world.MeetingA, ct);
            Assert.NotNull(found);
            Assert.Equal(world.MeetingA, found.Id);
            Assert.Equal(7, found.Connections.Count);

            // Outside the period it is still a meeting of the course (spec I-1): R3 takes no interval.
            Assert.NotNull(await source.GetMeetingOfCourseAsync(world.CourseId, world.MeetingOutside, ct));

            Assert.Null(await source.GetMeetingOfCourseAsync(world.CourseId, world.MeetingElsewhere, ct));
            Assert.Null(await source.GetMeetingOfCourseAsync(world.CourseId, world.MeetingMarked, ct));
            Assert.Null(await source.GetMeetingOfCourseAsync(world.CourseId, world.MeetingUnlinked, ct));
            Assert.Null(await source.GetMeetingOfCourseAsync(world.CourseId, world.MeetingA + 1000, ct));
            Assert.Null(await source.GetMeetingOfCourseAsync(world.CourseId + 1000, world.MeetingA, ct));
        });
    }

    /// <summary>R4: every membership of the course in any role, with email, full name and the roster dates; nothing of another course.</summary>
    [Fact]
    public async Task GetCourseMembers_ReturnsEveryRoleOfTheCourse_WithEmailNameAndDates()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedWorldAsync(ct);
        var left = await Mr.AddMemberAsync(
            host, world.CourseId, 30, MeetReportsTestData.Email("student.left"), "Test Student Left", CourseTestData.Roles.Student, ct,
            onRoster: false,
            firstSeenAt: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            lastSeenAt: new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero));
        var noEmail = await Mr.AddMemberAsync(
            host, world.CourseId, 31, null, null, CourseTestData.Roles.Student, ct);

        await host.WithMeetReportSourceAsync(async source =>
        {
            var members = await source.GetCourseMembersAsync(world.CourseId, ct);

            Assert.Equal(6, members.Count);
            var teacher = Assert.Single(members, m => m.Role == ClassroomRole.Teacher);
            Assert.Equal(Mr.TeacherEmail, teacher.Email, ignoreCase: true);
            Assert.Equal(Mr.TeacherName, teacher.FullName);
            Assert.True(teacher.OnRoster);
            Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), teacher.FirstSeenAt);
            var leaver = Assert.Single(members, m => m.ParticipantId == left);
            Assert.False(leaver.OnRoster);
            Assert.Equal(ClassroomRole.Student, leaver.Role);
            Assert.Equal(new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero), leaver.LastSeenAt);
            var nameless = Assert.Single(members, m => m.ParticipantId == noEmail);
            Assert.Null(nameless.Email);
            Assert.Null(nameless.FullName);
            Assert.DoesNotContain(members, m => string.Equals(m.Email, Mr.ElsewhereEmail, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(members, m => string.Equals(m.Email, Mr.OtherTeacherEmail, StringComparison.OrdinalIgnoreCase));
            Assert.Empty(await source.GetCourseMembersAsync(world.CourseId + 1000, ct));
        });
    }

    /// <summary>R5, db-design §4: emails match case-insensitively; a stored mixed-case address is found by its lower-case form.</summary>
    [Fact]
    public async Task GetPeople_MatchesEmailsCaseInsensitively()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await Mr.AddMemberAsync(
            host, course, 1, "Mixed.Case@School.Example", "Test Mixed Case", CourseTestData.Roles.Student, ct);

        await host.WithMeetReportSourceAsync(async source =>
        {
            var people = await source.GetPeopleAsync(["mixed.case@school.example"], ct);

            var person = Assert.Single(people);
            Assert.Equal("mixed.case@school.example", person.LowerCaseEmail);
            Assert.Equal("Test Mixed Case", person.FullName);
            Assert.True(person.HasAnyMembership);
        });
    }

    /// <summary>R5: one record per requested email that matches; an unknown email and an unrequested participant are absent; an empty request is empty.</summary>
    [Fact]
    public async Task GetPeople_ReturnsOneRecordPerMatchingEmail_AndNothingElse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedWorldAsync(ct);

        await host.WithMeetReportSourceAsync(async source =>
        {
            var people = await source.GetPeopleAsync(
                [Mr.StudentOneEmail.ToLowerInvariant(), Mr.ElsewhereEmail.ToLowerInvariant(), "nobody.here@school.example"], ct);

            Assert.Equal(2, people.Count);
            Assert.Equal(Mr.StudentOneName, Assert.Single(people, p => p.LowerCaseEmail == Mr.StudentOneEmail.ToLowerInvariant()).FullName);
            var elsewhere = Assert.Single(people, p => p.LowerCaseEmail == Mr.ElsewhereEmail.ToLowerInvariant());
            Assert.True(elsewhere.HasAnyMembership, "A member of another course is a personal account.");
            Assert.Empty(await source.GetPeopleAsync([], ct));
            Assert.True(world.CourseId > 0);
        });
    }

    /// <summary>R5, db-design §4: a participant with no membership at all is not a personal account.</summary>
    [Fact]
    public async Task GetPeople_ReportsAParticipantWithoutAnyMembership_AsNotPersonal()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(40), email: "no.course@school.example", fullName: "Test Person No Course");

        await host.WithMeetReportSourceAsync(async source =>
        {
            var people = await source.GetPeopleAsync(["no.course@school.example"], ct);

            var person = Assert.Single(people);
            Assert.False(person.HasAnyMembership);
            Assert.Equal("Test Person No Course", person.FullName);
        });
    }

    /// <summary>R5, db-design §4: duplicates of one email — personal when any has a membership; the name is the lowest id's that has one.</summary>
    [Fact]
    public async Task GetPeople_AppliesTheTieRule_ToDuplicateEmails()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        const string email = "twin@school.example";

        // Lowest id without a name and without a membership; then two named ones, the second with the membership.
        await CourseRows.InsertParticipantAsync(host, ct, googleUserId: CourseTestData.UserId(41), email: email, fullName: null);
        await CourseRows.InsertParticipantAsync(host, ct, googleUserId: CourseTestData.UserId(42), email: email, fullName: "Test Twin First");
        var third = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(43), email: email.ToUpperInvariant(), fullName: "Test Twin Second");
        await CourseRows.InsertMembershipAsync(host, course, third, ct);

        await host.WithMeetReportSourceAsync(async source =>
        {
            var people = await source.GetPeopleAsync([email], ct);

            var person = Assert.Single(people);
            Assert.Equal("Test Twin First", person.FullName);
            Assert.True(person.HasAnyMembership);
        });
    }

    /// <summary>R5: a matching participant with no full name gives a null name.</summary>
    [Fact]
    public async Task GetPeople_ReturnsANullName_WhenNoMatchingParticipantHasOne()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await Mr.AddMemberAsync(host, course, 44, "unnamed@school.example", null, CourseTestData.Roles.Student, ct);

        await host.WithMeetReportSourceAsync(async source =>
        {
            var people = await source.GetPeopleAsync(["unnamed@school.example"], ct);

            var person = Assert.Single(people);
            Assert.Null(person.FullName);
            Assert.True(person.HasAnyMembership);
        });
    }
}
