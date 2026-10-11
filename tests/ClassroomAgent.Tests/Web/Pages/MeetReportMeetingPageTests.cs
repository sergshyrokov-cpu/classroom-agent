using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Keys = ClassroomAgent.Tests.TestInfrastructure.MeetReportsTestData.Keys;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-033 over HTTP, report 4: the meeting of the course in detail, the lines and their marks, "N of M min" with
/// capping and half-up rounding, the other-participants line without identity, the link back, and the 400 and 404
/// pages (AC-005, AC-007, AC-009, AC-010, AC-013, AC-015; api-design §2.4, §3, §5). Rows are seeded by SQL.
/// </summary>
public sealed class MeetReportMeetingPageTests(PostgreSqlFixture database)
{
    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static string OctoberMeeting(Mr.SeededWorld world, long meetingId) =>
        MeetReportsTestData.MeetingUrl(
            Id(meetingId), world.CourseId, MeetReportsTestData.Period.FromText, MeetReportsTestData.Period.ToText);

    /// <summary>AC-010, FR-010: the header names the course, the code and the organizer; the lines come in the stated order with their marks.</summary>
    [Fact]
    public async Task TheMeeting_ShowsTheHeader_AndTheLinesInOrderWithTheirMarks()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(OctoberMeeting(world, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        NoStore.Assert(page, "the meeting page");
        var text = Mc.VisibleText(page.Body);
        Assert.Contains(Mr.CourseName, text, StringComparison.Ordinal);
        Assert.Contains(Mr.Codes.Linked, text, StringComparison.Ordinal);
        Assert.Contains(Mr.TeacherName, text, StringComparison.Ordinal);
        Assert.Contains(Mr.TeacherEmail, text, StringComparison.Ordinal);
        var lines = text[text.LastIndexOf(Mr.TeacherName, StringComparison.Ordinal)..];
        var studentOne = lines.IndexOf(Mr.StudentOneName, StringComparison.Ordinal);
        var studentTwo = lines.IndexOf(Mr.StudentTwoName, StringComparison.Ordinal);
        var elsewhere = lines.IndexOf(Mr.ElsewhereName, StringComparison.Ordinal);
        Assert.True(
            studentOne > 0 && studentTwo > studentOne && elsewhere > studentTwo,
            "Organizer, then students by name, then the person not on the list.");
        Assert.Contains(host.Text(Keys.MarkOrganizer, "uk"), text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.MarkCourseStudent, "uk"), text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.MarkNotOnCourseList, "uk"), text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Keys.MarkNotCourseTeacher, "uk"), text, StringComparison.Ordinal);
    }

    /// <summary>AC-010, I-11: the other participants are one line; no class account email and no guest appears.</summary>
    [Fact]
    public async Task TheOtherParticipants_AreOneLine_WithoutAnyAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(OctoberMeeting(world, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var other = host.Text(Keys.MeetingOtherParticipants, "uk");
        Assert.Equal(1, Regex.Count(Mc.VisibleText(page.Body), Regex.Escape(other)));
        Assert.DoesNotContain(Mr.ClassAccountEmail, page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class.five", page.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-007, AC-010: an organizer who is not a teacher of the course gets the mark.</summary>
    [Fact]
    public async Task AnOrganizerWhoIsNotATeacherOfTheCourse_GetsTheMark()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(OctoberMeeting(world, world.MeetingB), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(Mr.OtherTeacherName, page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.MarkNotCourseTeacher, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-010, I-3: an organizer with no connection is named in the header and has no line.</summary>
    [Fact]
    public async Task AnOrganizerWithNoConnection_IsInTheHeaderOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        var meeting = await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, MeetReportsTestData.Period.Local(10, 12, 10), TimeSpan.FromMinutes(45), ct,
            new Mr.Connection(Mr.StudentOneEmail, 600));

        var page = await client.GetAsync(OctoberMeeting(world, meeting), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        Assert.Equal(1, Regex.Count(text, Regex.Escape(Mr.TeacherName)));
        Assert.DoesNotContain(host.Text(Keys.MarkOrganizer, "uk"), text, StringComparison.Ordinal);
        Assert.Contains(Mr.StudentOneName, text, StringComparison.Ordinal);
    }

    /// <summary>AC-009: a person on two devices is capped at the meeting's duration — "45 of 45 min" (en).</summary>
    [Fact]
    public async Task APersonOnTwoDevices_IsCappedAtTheMeetingDuration_InEnglish()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await MeetReportsHostExtensions.StartAsync(database, Actor.Admin, "en", ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(OctoberMeeting(world, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal("en", UiLanguageTestData.PageLanguage(page.Body));
        var text = Mc.VisibleText(page.Body);
        Assert.Contains("45 of 45 min", text, StringComparison.Ordinal);
        Assert.Contains("20 of 45 min", text, StringComparison.Ordinal);
    }

    /// <summary>AC-009, I-5: seconds are rounded to a whole minute, half up, when rendered.</summary>
    [Fact]
    public async Task Minutes_AreRoundedHalfUp_WhenRendered()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await MeetReportsHostExtensions.StartAsync(database, Actor.Admin, "en", ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        var meeting = await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, MeetReportsTestData.Period.Local(10, 13, 10), TimeSpan.FromMinutes(45), ct,
            new Mr.Connection(Mr.StudentOneEmail, 90),
            new Mr.Connection(Mr.StudentTwoEmail, 30),
            new Mr.Connection(Mr.StudentThreeEmail, 29));

        var page = await client.GetAsync(OctoberMeeting(world, meeting), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        Assert.Matches(new Regex(@"(?<![\d.,])2 of 45 min"), text);
        Assert.Matches(new Regex(@"(?<![\d.,])1 of 45 min"), text);
        Assert.Matches(new Regex(@"(?<![\d.,])0 of 45 min"), text);
    }

    /// <summary>AC-010, I-1: a meeting of the course opens from any period; the link back keeps the course and a valid period.</summary>
    [Fact]
    public async Task AMeetingOutsideThePeriod_Opens_AndTheLinkBackKeepsTheCourseAndThePeriod()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(OctoberMeeting(world, world.MeetingOutside), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(Mr.CourseName, page.Text, StringComparison.Ordinal);
        Assert.Contains(
            $"href=\"/workspace/meet-reports?courseId={world.CourseId}&from={MeetReportsTestData.Period.FromText}&to={MeetReportsTestData.Period.ToText}\"",
            page.Text,
            StringComparison.Ordinal);
    }

    /// <summary>AC-010: an invalid or absent period is dropped from the link back and is not echoed.</summary>
    [Theory]
    [InlineData("from=zz-bad-zz&to=2026-10-31", "&to=2026-10-31")]
    [InlineData("from=2026-10-01&to=2026-02-30", "&from=2026-10-01")]
    [InlineData("", "")]
    public async Task AnInvalidPeriod_IsDroppedFromTheLinkBack(string period, string kept)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        var query = $"?courseId={world.CourseId}" + (period.Length == 0 ? string.Empty : "&" + period);

        var page = await client.GetAsync($"/workspace/meet-reports/meetings/{world.MeetingA}{query}", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains($"href=\"/workspace/meet-reports?courseId={world.CourseId}{kept}\"", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("zz-bad-zz", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-02-30", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-013: no course id, or a malformed one, is 400 with the course-malformed message and a way back; nothing is echoed.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("?courseId=zz-bad-zz")]
    [InlineData("?courseId=0")]
    [InlineData("?courseId=-1")]
    [InlineData("?courseId=99999999999999999999")]
    [InlineData("?courseId=1&courseId=2")]
    public async Task AMissingOrMalformedCourseId_Is400_WithTheMessage(string query)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync($"/workspace/meet-reports/meetings/{world.MeetingA}{query}", ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(host.Text(Keys.MeetingCourseMalformed, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains("href=\"/workspace/meet-reports", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Mr.StudentOneName, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("zz-bad-zz", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-013, api-design §2.4: an unknown course, an unknown meeting, a meeting of another course, of a marked or an unlinked code — all the same 404.</summary>
    [Theory]
    [InlineData("unknown-course")]
    [InlineData("unknown-meeting")]
    [InlineData("zero")]
    [InlineData("overflow")]
    [InlineData("other-course")]
    [InlineData("marked-code")]
    [InlineData("unlinked-code")]
    public async Task ACourseOrMeetingNotFound_Is404_WithOneMessage(string which)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        var (courseId, meetingId) = which switch
        {
            "unknown-course" => (987654L, Id(world.MeetingA)),
            "unknown-meeting" => (world.CourseId, "987654"),
            "zero" => (world.CourseId, "0"),
            "overflow" => (world.CourseId, "99999999999999999999"),
            "other-course" => (world.CourseId, Id(world.MeetingElsewhere)),
            "marked-code" => (world.CourseId, Id(world.MeetingMarked)),
            "unlinked-code" => (world.CourseId, Id(world.MeetingUnlinked)),
            _ => throw new ArgumentOutOfRangeException(nameof(which), which, null),
        };

        var page = await client.GetAsync(MeetReportsTestData.MeetingUrl(meetingId, courseId), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Contains(host.Text(Keys.MeetingNotFound, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Mr.StudentOneName, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Mr.Codes.Elsewhere, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-013: a non-digit meeting id never reaches the action — the host's catch-all 404, not the page.</summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("1x")]
    public async Task ANonDigitMeetingId_MeetsTheCatchAll404(string meetingId)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.MeetingUrl(meetingId, world.CourseId), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.DoesNotContain(host.Text(Keys.MeetingNotFound, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Mr.CourseName, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-015: in each BR-025 cause the meeting page answers 200 with its lines.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheMeetingIsStillShown(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct, cause);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(OctoberMeeting(world, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(Mr.CourseName, page.Text, StringComparison.Ordinal);
        Assert.Contains(Mr.StudentOneName, page.Text, StringComparison.Ordinal);
        Assert.Contains(Mr.ElsewhereName, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-016, VR-004: the course name and a person's name from Google are HTML-encoded on the meeting page.</summary>
    [Fact]
    public async Task NamesFromGoogle_AreHtmlEncoded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await host.ExecuteAsync(
            "UPDATE classroom_participant SET full_name = @name WHERE email = @email",
            ct,
            ("name", "<i>Test Bold Student</i>"),
            ("email", Mr.StudentTwoEmail));

        var page = await client.GetAsync(OctoberMeeting(world, world.MeetingA), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("&lt;i&gt;Test Bold Student&lt;/i&gt;", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("<i>Test Bold Student</i>", page.Body, StringComparison.Ordinal);
    }
}
