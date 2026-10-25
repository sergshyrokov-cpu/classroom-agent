using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Keys = ClassroomAgent.Tests.TestInfrastructure.MeetReportsTestData.Keys;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Ms = ClassroomAgent.Tests.TestInfrastructure.MembershipStartHost;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-056 AC-003, AC-004 and the Meet part of AC-007 (spec FR-001 a/c, FR-003.3, FR-003.4, FR-003.5): Meet reports,
/// automatic linking and the order of the unassigned codes list use the start of a membership. The course and its
/// memberships come from real synchronization runs (TC-2), Google substituted (TC-4); meetings and links are seeded
/// by SQL, as the US-032 and US-033 page tests seed them.
/// </summary>
public sealed class MembershipStartMeetTests(PostgreSqlFixture database)
{
    private const string LinkedCode = "msa-0001-xyz";

    private const string AutoCode = "msa-0002-xyz";

    private const string CandidateCode = "msa-0003-xyz";

    private const string NoCandidateCode = "msa-0004-xyz";

    private static readonly string CourseGoogleId = CourseTestData.CourseId(1);

    /// <summary>T, the teacher of the first load.</summary>
    private static RosterEntry Teacher => Ms.Person(1);

    /// <summary>S, S2, S3: students of the first load.</summary>
    private static RosterEntry Student => Ms.Person(2);

    private static RosterEntry StudentTwo => Ms.Person(3);

    private static RosterEntry StudentThree => Ms.Person(4);

    /// <summary>N: joins the roster at the later run (AC-007).</summary>
    private static RosterEntry Newcomer => Ms.Person(5);

    /// <summary>A domain account with no membership and no teacher role anywhere: the organizer of a code with no candidate.</summary>
    private static readonly string Outsider = CourseTestData.Email("outsider.one");

    /// <summary>2025-10-15, 10:00 in Kyiv (UTC+3 before the October 2025 change).</summary>
    private static readonly DateTimeOffset October15th2025 = new(2025, 10, 15, 7, 0, 0, TimeSpan.Zero);

    /// <summary>2026-10-30, 10:00 in Kyiv (UTC+2 after the October 2026 change).</summary>
    private static readonly DateTimeOffset October30th2026 = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private static void CourseInGoogle(FakeClassroomReader reader, params RosterEntry[] students) =>
        reader.WithCourse(
            CourseGoogleId,
            CourseTestData.CourseName(1),
            teachers: [Teacher],
            students: students.Length == 0 ? [Student, StudentTwo, StudentThree] : students,
            creationTime: Ms.CourseCreated);

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>AC-003, FR-003.3: on the meeting of 2025-10-15, T is the organizer and a course teacher, S a course student.</summary>
    [Fact]
    public async Task AMeetingBeforeTheFirstRun_MarksTheFirstLoadsTeacherAndStudent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Ms.StartFirstRunAsync(database, r => CourseInGoogle(r), ct);
        var courseId = await host.CourseIdAsync(CourseGoogleId, ct);
        await MeetLinkRows.InsertLinkAsync(host, MeetLinkRows.LinkRow.Automatic(LinkedCode, courseId, Ms.FirstRun), ct);
        var meeting = await MeetReportsHostExtensions.InsertMeetingAsync(
            host, LinkedCode, Teacher.Email!, October15th2025, TimeSpan.FromMinutes(45), ct,
            new MeetReportsHostExtensions.Connection(Teacher.Email, 2700),
            new MeetReportsHostExtensions.Connection(Student.Email, 2400));
        var client = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);

        var page = await client.GetAsync(MeetReportsTestData.MeetingUrl(Id(meeting), courseId, "2025-10-01", "2025-10-31"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains(Teacher.FullName!, text, StringComparison.Ordinal);
        Assert.Contains(Student.FullName!, text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.MarkOrganizer, "uk"), text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Keys.MarkNotCourseTeacher, "uk"), text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.MarkCourseStudent, "uk"), text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Keys.MarkNotOnCourseList, "uk"), text, StringComparison.Ordinal);
    }

    /// <summary>AC-003, FR-003.3: report 3 for October 2025 lists S among the course's students.</summary>
    [Fact]
    public async Task TheReportBeforeTheFirstRun_ListsTheFirstLoadsStudent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Ms.StartFirstRunAsync(database, r => CourseInGoogle(r), ct);
        var courseId = await host.CourseIdAsync(CourseGoogleId, ct);
        await MeetLinkRows.InsertLinkAsync(host, MeetLinkRows.LinkRow.Automatic(LinkedCode, courseId, Ms.FirstRun), ct);
        await MeetReportsHostExtensions.InsertMeetingAsync(
            host, LinkedCode, Teacher.Email!, October15th2025, TimeSpan.FromMinutes(45), ct,
            new MeetReportsHostExtensions.Connection(Teacher.Email, 2700),
            new MeetReportsHostExtensions.Connection(Student.Email, 2400));
        var client = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);

        var page = await client.GetAsync(MeetReportsTestData.Url(courseId, "2025-10-01", "2025-10-31"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        // The control: the meeting is in the report, so an empty students list is not an empty report.
        Assert.DoesNotContain(host.Text(Keys.EmptyNoMeetings, "uk"), text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Keys.EmptyNoStudents, "uk"), text, StringComparison.Ordinal);
        Assert.Contains(Student.FullName!, text, StringComparison.Ordinal);
        Assert.Contains(StudentTwo.FullName!, text, StringComparison.Ordinal);
    }

    /// <summary>AC-004, FR-003.4: a code whose meetings all predate the first run, organized by T and attended by the course's students, is linked automatically.</summary>
    [Fact]
    public async Task ACodeOfMeetingsBeforeTheFirstRun_IsLinkedAutomatically()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Ms.StartFirstRunAsync(
            database,
            r => CourseInGoogle(r),
            ct,
            seed: async (h, token) =>
            {
                // Meetings are already stored when the first run starts; its linking step scores every unassigned code.
                await h.SeedMeetingAsync(AutoCode, Teacher.Email!, October15th2025, token, [Student.Email!, StudentTwo.Email!, StudentThree.Email!]);
                await h.SeedMeetingAsync(AutoCode, Teacher.Email!, October15th2025.AddDays(7), token, [Student.Email!, StudentTwo.Email!]);
            });
        var courseId = await host.CourseIdAsync(CourseGoogleId, ct);

        var link = Assert.Single(await MeetLinkingTestData.LinksAsync(host, ct));

        Assert.Equal(AutoCode, link.MeetingCode);
        Assert.Equal(courseId, link.CourseId);
        Assert.True(link.LinkedAutomatically);
    }

    /// <summary>
    /// AC-004 second paragraph, FR-003.5: T's code that stays unassigned (one student of five accounts, below the share)
    /// is in the group with a candidate, before a code with none, even though the other code met later.
    /// </summary>
    [Fact]
    public async Task AnUnassignedCodeOfTheFirstLoadsTeacher_ComesBeforeACodeWithNoCandidate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Ms.StartFirstRunAsync(
            database,
            r => CourseInGoogle(r),
            ct,
            seed: async (h, token) =>
            {
                await h.SeedMeetingAsync(
                    CandidateCode, Teacher.Email!, October15th2025, token,
                    [Student.Email!, CourseTestData.Email("guest.one"), CourseTestData.Email("guest.two"), CourseTestData.Email("guest.three"), CourseTestData.Email("guest.four")]);
                await h.SeedMeetingAsync(NoCandidateCode, Outsider, October15th2025.AddDays(30), token, [Student.Email!]);
            });
        var client = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);

        var page = await client.GetAsync(Mc.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Empty(await MeetLinkingTestData.LinksAsync(host, ct));
        var text = Mc.VisibleText(page.Body);
        var first = text.IndexOf(CandidateCode, StringComparison.Ordinal);
        var second = text.IndexOf(NoCandidateCode, StringComparison.Ordinal);
        Assert.True(first >= 0 && second >= 0, "Both codes must be listed.");
        Assert.True(first < second, "T's code has a candidate, so it must come before the code with none.");
        var noCandidates = host.Text(Mc.Keys.NoCandidates, "uk");
        Assert.Equal(1, Regex.Count(text, Regex.Escape(noCandidates)));
    }

    /// <summary>
    /// AC-007, FR-001 c: on a meeting of 2026-10-30, N — first seen by the run of 2026-11-05 — is not a course student,
    /// while S of the first load is. Green before implementation by design: S's first-seen day 2026-10-22 already
    /// precedes the meeting; this guards against counting a later arrival from the course's creation.
    /// </summary>
    [Fact]
    public async Task AMeetingBeforeALaterArrivalWasSeen_DoesNotCountThemAsACourseStudent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var first = await Ms.StartFirstRunAsync(database, r => CourseInGoogle(r, Student), ct);
        await using var later = await Ms.RunAgainAtAsync(first, Ms.LaterRun, r => CourseInGoogle(r, Student, Newcomer), ct);
        var courseId = await later.CourseIdAsync(CourseGoogleId, ct);
        await MeetLinkRows.InsertLinkAsync(later, MeetLinkRows.LinkRow.Automatic(LinkedCode, courseId, Ms.FirstRun), ct);
        var meeting = await MeetReportsHostExtensions.InsertMeetingAsync(
            later, LinkedCode, Teacher.Email!, October30th2026, TimeSpan.FromMinutes(45), ct,
            new MeetReportsHostExtensions.Connection(Teacher.Email, 2700),
            new MeetReportsHostExtensions.Connection(Student.Email, 2400),
            new MeetReportsHostExtensions.Connection(Newcomer.Email, 2400));
        var client = await JournalHostExtensions.SignInAsync(later, Actor.Dean, ct);

        var page = await client.GetAsync(MeetReportsTestData.MeetingUrl(Id(meeting), courseId, "2026-10-01", "2026-10-31"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains(Student.FullName!, text, StringComparison.Ordinal);
        Assert.Contains(Newcomer.FullName!, text, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(text, Regex.Escape(later.Text(Keys.MarkCourseStudent, "uk"))));
        Assert.Equal(1, Regex.Count(text, Regex.Escape(later.Text(Keys.MarkNotOnCourseList, "uk"))));
        var newcomerLine = text[text.IndexOf(Newcomer.FullName!, StringComparison.Ordinal)..];
        Assert.Contains(later.Text(Keys.MarkNotOnCourseList, "uk"), newcomerLine, StringComparison.Ordinal);
    }
}
