using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Keys = ClassroomAgent.Tests.TestInfrastructure.MeetReportsTestData.Keys;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-033 over HTTP, reports page: the form and its default period, reports 1–3 in order, the meetings of the course
/// only, the links to report 4, the delay notice, empty states, OD-012, HTML encoding, the 400 and 404 pages, the
/// home page entry, read-only mode, no writes and no Google call (AC-001, AC-002, AC-003, AC-008, AC-010, AC-011,
/// AC-012, AC-013, AC-015, AC-017, AC-018; api-design §2–§5). Rows are seeded by SQL.
/// </summary>
public sealed class MeetReportsPageTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset LoadedUpTo = new(2026, 9, 23, 21, 30, 0, TimeSpan.Zero);

    private static readonly DateTime LoadedUpToLocal = new(2026, 9, 24, 0, 30, 0);

    private static string MeetingLink(long meetingId, long courseId) =>
        $"/workspace/meet-reports/meetings/{meetingId}?courseId={courseId}"
        + $"&from={MeetReportsTestData.Period.FromText}&to={MeetReportsTestData.Period.ToText}";

    /// <summary>AC-001: no course id gives the form only, with the host clock's month and every stored course.</summary>
    [Fact]
    public async Task WithoutACourse_TheFormIsShown_WithTheCurrentMonth_AndEveryCourseIncludingArchived()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.Url(), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal("2026-09-01", Html.InputValue(page.Body, JournalTestData.Parameters.From));
        Assert.Equal("2026-09-30", Html.InputValue(page.Body, JournalTestData.Parameters.To));
        Assert.Contains(Mr.CourseName, page.Text, StringComparison.Ordinal);
        Assert.Contains(Mr.ArchivedCourseName, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Keys.SummaryTitle, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Keys.DelayNotice, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Mr.StudentOneName, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-001: one absent date is filled on its own.</summary>
    [Fact]
    public async Task OnlyFromGiven_ToIsTheLastDayOfTheCurrentMonth()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.Url(world.CourseId, from: "2026-09-10"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal("2026-09-10", Html.InputValue(page.Body, JournalTestData.Parameters.From));
        Assert.Equal("2026-09-30", Html.InputValue(page.Body, JournalTestData.Parameters.To));
    }

    /// <summary>AC-001, FR-001, FR-002: the form, then the delay notice, then reports 1, 2 and 3 in that order.</summary>
    [Fact]
    public async Task TheReports_AreRenderedInOrder_AfterTheFormAndTheNotice()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        NoStore.Assert(page, "the reports page");
        Assert.Equal(MeetReportsTestData.Period.FromText, Html.InputValue(page.Body, JournalTestData.Parameters.From));
        Assert.Equal(MeetReportsTestData.Period.ToText, Html.InputValue(page.Body, JournalTestData.Parameters.To));
        var text = Mc.VisibleText(page.Body);
        var positions = new[] { Keys.FormSubmit, Keys.DelayNotice, Keys.SummaryTitle, Keys.MeetingsTitle, Keys.StudentsTitle }
            .Select(key => (Key: key, Index: text.IndexOf(host.Text(key, "uk"), StringComparison.Ordinal)))
            .ToList();
        Assert.All(positions, p => Assert.True(p.Index >= 0, $"'{p.Key}' is not on the page."));
        Assert.Equal(positions.Select(p => p.Index).Order().ToList(), positions.Select(p => p.Index).ToList());
    }

    /// <summary>AC-002, AC-003, AC-018, AC-010: only the meetings of the linked and confirmed codes, each linking to report 4.</summary>
    [Fact]
    public async Task OnlyTheMeetingsOfTheCourseInThePeriod_AreListed_EachLinkingToReport4()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(MeetingLink(world.MeetingA, world.CourseId), page.Text, StringComparison.Ordinal);
        Assert.Contains(MeetingLink(world.MeetingB, world.CourseId), page.Text, StringComparison.Ordinal);
        foreach (var excluded in new[] { world.MeetingMarked, world.MeetingUnlinked, world.MeetingOutside, world.MeetingElsewhere })
        {
            Assert.DoesNotContain($"/workspace/meet-reports/meetings/{excluded}?", page.Text, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-018: a meeting that starts at 00:30 Kyiv on 2026-11-01 is out, one at 00:30 on 2026-10-01 is in.</summary>
    [Fact]
    public async Task AtTheKyivMidnightOfThePeriod_TheMeetingIsInOrOutByItsStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        var first = await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, MeetReportsTestData.Period.Local(10, 1, 0, 30), TimeSpan.FromMinutes(20), ct);
        var after = await Mr.InsertMeetingAsync(
            host, Mr.Codes.Linked, Mr.TeacherEmail, MeetReportsTestData.Period.Local(11, 1, 0, 30), TimeSpan.FromMinutes(20), ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(MeetingLink(first, world.CourseId), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain($"/workspace/meet-reports/meetings/{after}?", page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-005, AC-007, AC-010: the organizer is shown with name and email; the mark appears for a non-teacher organizer only.</summary>
    [Fact]
    public async Task TheOrganizer_IsShownWithNameAndEmail_AndTheMarkOnlyWhenNotATeacherOfTheCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        var markText = host.Text(Keys.MarkNotCourseTeacher, "uk");

        var both = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);
        var onlyFirst = await client.GetAsync(
            MeetReportsTestData.Url(world.CourseId, "2026-10-01", "2026-10-10"), ct);

        Assert.Equal(HttpStatusCode.OK, both.Status);
        Assert.Contains(Mr.TeacherName, both.Text, StringComparison.Ordinal);
        Assert.Contains(Mr.TeacherEmail, both.Text, StringComparison.Ordinal);
        Assert.Contains(Mr.OtherTeacherName, both.Text, StringComparison.Ordinal);
        Assert.Contains(markText, both.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, onlyFirst.Status);
        Assert.Contains(Mr.TeacherName, onlyFirst.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Mr.OtherTeacherName, onlyFirst.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(markText, onlyFirst.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-008, I-10: every student of the roster is a row by name; a teacher, a person of another course and a class account are not.</summary>
    [Fact]
    public async Task Report3_ListsTheStudentsByName_AndNobodyElse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        var section = text[text.IndexOf(host.Text(Keys.StudentsTitle, "uk"), StringComparison.Ordinal)..];
        var one = section.IndexOf(Mr.StudentOneName, StringComparison.Ordinal);
        var three = section.IndexOf(Mr.StudentThreeName, StringComparison.Ordinal);
        var two = section.IndexOf(Mr.StudentTwoName, StringComparison.Ordinal);
        Assert.True(one >= 0 && three > one && two > three, "Student One, Student Three, Student Two — by name.");
        Assert.Contains(Mr.StudentOneEmail, section, StringComparison.Ordinal);
        Assert.DoesNotContain(Mr.ElsewhereName, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Mr.ElsewhereEmail, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Mr.ClassAccountEmail, text, StringComparison.Ordinal);
    }

    /// <summary>OD-012: a student without an email is a row; without a name too, the unnamed label is shown.</summary>
    [Fact]
    public async Task AStudentWithoutAnEmail_IsARow_AndWithoutANameEither_TheUnnamedLabelIsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await Mr.AddMemberAsync(host, world.CourseId, 20, null, "Test Student Nameless Mail", CourseTestData.Roles.Student, ct);
        await Mr.AddMemberAsync(host, world.CourseId, 21, null, null, CourseTestData.Roles.Student, ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("Test Student Nameless Mail", page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.PersonUnnamed, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-016, VR-004: names from Google are HTML-encoded, never rendered as markup.</summary>
    [Fact]
    public async Task ANameFromGoogle_IsHtmlEncoded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await Mr.AddMemberAsync(
            host, world.CourseId, 22, Mr.StudentOneEmail.Replace("one", "bold", StringComparison.Ordinal),
            "<b>Test Bold Student</b>", CourseTestData.Roles.Student, ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("&lt;b&gt;Test Bold Student&lt;/b&gt;", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Test Bold Student</b>", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-012: no meeting in the period — the empty line replaces report 2, the students are still listed.</summary>
    [Fact]
    public async Task WithoutMeetings_ReportTwoIsTheEmptyLine_AndTheStudentsAreStillListed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.Url(world.CourseId, "2026-11-02", "2026-11-30"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(Keys.EmptyNoMeetings, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(Mr.StudentOneName, page.Text, StringComparison.Ordinal);
        Assert.Contains(Mr.StudentTwoName, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Keys.EmptyNoStudents, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-012: no student in the period — the empty line replaces report 3.</summary>
    [Fact]
    public async Task WithoutStudents_ReportThreeIsTheEmptyLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedWorldAsync(ct);
        var empty = await CourseRows.InsertCourseAsync(
            host, ct, googleId: CourseTestData.CourseId(9), name: "Test Course Without Students");

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(empty), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(Keys.EmptyNoStudents, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.EmptyNoMeetings, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>FR-002: with no course stored a message replaces the drop-down.</summary>
    [Fact]
    public async Task WithNoCourseStored_TheMessageReplacesTheDropDown()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(MeetReportsTestData.Url(), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(Keys.NoCourses, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-011: the notice shows the watermark in the school's time zone, formatted for the UI language.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheDelayNotice_ShowsTheWatermarkInTheSchoolTimeZone(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await host.SeedWatermarkAsync(LoadedUpTo, ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Contains(host.Text(Keys.DelayNotice, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(MeetTestData.FormatLocal(LoadedUpToLocal, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(LastSynchronizationTestData.Format(LoadedUpTo, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Keys.DelayNoticeNotLoaded, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-011, OD-008: no sync state, or a null watermark, says the Meet meetings are not loaded yet.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithoutAWatermark_TheNoticeSaysNotLoadedYet(bool withSyncStateRow)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        if (withSyncStateRow)
        {
            await host.SeedWatermarkAsync(null, ct);
        }

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(Keys.DelayNotice, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.DelayNoticeNotLoaded, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-013: a malformed course id is 400 with the form and the message; nothing is read and the value is not echoed.</summary>
    [Theory]
    [InlineData("zz-bad-zz")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("1%20")]
    [InlineData("99999999999999999999")]
    [InlineData("1&courseId=2")]
    public async Task AMalformedCourseId_Is400_WithTheFormAndTheMessage(string courseId)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedWorldAsync(ct);

        var page = await client.GetAsync("/workspace/meet-reports?courseId=" + courseId, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.True(Html.HasInput(page.Body, JournalTestData.Parameters.From));
        Assert.Contains(host.Text(Keys.ValidationCourseMalformed, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Keys.SummaryTitle, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("zz-bad-zz", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-013: a malformed, out-of-range, repeated or inverted date is 400 with its own message; the value is not echoed.</summary>
    [Theory]
    [InlineData("from=2026-13-01", Keys.ValidationFromMalformed)]
    [InlineData("from=2026-02-30", Keys.ValidationFromMalformed)]
    [InlineData("from=1999-12-31", Keys.ValidationFromMalformed)]
    [InlineData("from=2101-01-01", Keys.ValidationFromMalformed)]
    [InlineData("from=01.10.2026", Keys.ValidationFromMalformed)]
    [InlineData("from=2026-10-01&from=2026-10-02", Keys.ValidationFromMalformed)]
    [InlineData("from=zz-not-a-date-zz", Keys.ValidationFromMalformed)]
    [InlineData("to=2026-13-01", Keys.ValidationToMalformed)]
    [InlineData("to=2101-01-01", Keys.ValidationToMalformed)]
    [InlineData("to=zz-not-a-date-zz", Keys.ValidationToMalformed)]
    [InlineData("from=2026-10-31&to=2026-10-01", Keys.ValidationPeriodInverted)]
    public async Task AMalformedOrInvertedDate_Is400_WithTheMessage_AndTheValueIsNotEchoed(string dates, string messageKey)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync($"/workspace/meet-reports?courseId={world.CourseId}&{dates}", ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.True(Html.HasInput(page.Body, JournalTestData.Parameters.From));
        Assert.Contains(host.Text(messageKey, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Keys.SummaryTitle, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("zz-not-a-date-zz", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("01.10.2026", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-013: a one-day period, and the years 2000 and 2100, are valid.</summary>
    [Theory]
    [InlineData("2026-10-05", "2026-10-05")]
    [InlineData("2000-01-01", "2000-01-01")]
    [InlineData("2100-12-31", "2100-12-31")]
    public async Task AOneDayPeriod_AndTheLimitYears_AreValid(string from, string to)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.Url(world.CourseId, from, to), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(from, Html.InputValue(page.Body, JournalTestData.Parameters.From));
        Assert.Equal(to, Html.InputValue(page.Body, JournalTestData.Parameters.To));
        Assert.Contains(host.Text(Keys.SummaryTitle, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-013: a well-formed id of no stored course is 404, still this page with the form.</summary>
    [Fact]
    public async Task AnUnknownCourse_Is404_WithTheFormAndTheMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(987654), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.True(Html.HasInput(page.Body, JournalTestData.Parameters.From));
        Assert.Contains(host.Text(Keys.ValidationCourseUnknown, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Keys.SummaryTitle, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>An unknown parameter is ignored and not echoed (query strings carry only ids and dates).</summary>
    [Fact]
    public async Task AnUnknownParameter_IsIgnored_AndNotEchoed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId) + "&marker=qq-unknown-qq", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(Mr.StudentOneName, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("qq-unknown-qq", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-001, FR-001: the home page links to the reports for both roles, in normal and read-only mode.</summary>
    [Theory]
    [InlineData(Actor.Admin, ReadOnlyModeHost.Cause.NotReadOnly)]
    [InlineData(Actor.Dean, ReadOnlyModeHost.Cause.NotReadOnly)]
    [InlineData(Actor.Admin, ReadOnlyModeHost.Cause.Suspended)]
    [InlineData(Actor.Dean, ReadOnlyModeHost.Cause.NeverConfirmed)]
    [InlineData(Actor.Dean, ReadOnlyModeHost.Cause.GracePeriodExpired)]
    public async Task TheHomePage_LinksToTheMeetReports(Actor actor, ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct, cause);
        await using var _host = host;

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("href=\"/workspace/meet-reports\"", page.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.NavigationEntry, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-015: in each BR-025 cause the reports are still shown, with the notice and the links.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheReportsAreStillShown(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct, cause);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await host.SeedWatermarkAsync(LoadedUpTo, ct);

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(Keys.DelayNotice, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.StudentsTitle, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(Mr.StudentOneName, page.Text, StringComparison.Ordinal);
        Assert.Contains(MeetingLink(world.MeetingA, world.CourseId), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-015, FR-015: a page request writes nothing — no audit row, no sync state, no link, no Meet row.</summary>
    [Theory]
    [InlineData(ReadOnlyModeHost.Cause.NotReadOnly)]
    [InlineData(ReadOnlyModeHost.Cause.Suspended)]
    public async Task APageRequest_WritesNothing(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct, cause);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        var before = await host.WriteFingerprintAsync(ct);

        var reports = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);
        var invalid = await client.GetAsync(MeetReportsTestData.Url(world.CourseId, "zz-not-a-date-zz"), ct);
        var meeting = await client.GetAsync(
            MeetReportsTestData.MeetingUrl(world.MeetingA.ToString(System.Globalization.CultureInfo.InvariantCulture), world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, reports.Status);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
        Assert.Equal(HttpStatusCode.OK, meeting.Status);
        Assert.Equal(before, await host.WriteFingerprintAsync(ct));
    }

    /// <summary>AC-017: neither page calls a Google port (the substituted readers record no call).</summary>
    [Fact]
    public async Task NeitherPage_CallsGoogle()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        var before = host.GoogleCallCount();

        var reports = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);
        var meeting = await client.GetAsync(
            MeetReportsTestData.MeetingUrl(world.MeetingA.ToString(System.Globalization.CultureInfo.InvariantCulture), world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, reports.Status);
        Assert.Equal(HttpStatusCode.OK, meeting.Status);
        Assert.Equal(before, host.GoogleCallCount());
    }
}
