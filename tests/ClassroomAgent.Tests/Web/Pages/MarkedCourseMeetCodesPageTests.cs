using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-061 over HTTP, "unlinked meetings" and the course-choice page: a marked course is never a candidate, can still be
/// chosen by hand, and shows its mark next to its name on the choice page — spec FR-002, FR-003, FR-006, VR-001, VR-002;
/// AC-005, AC-006, AC-010. Rows are seeded by SQL; marks are the US-059 columns.
/// </summary>
public sealed class MarkedCourseMeetCodesPageTests(PostgreSqlFixture database)
{
    private const string Teacher = "mk.teacher@school-one.example.test";
    private const string Code = "mrk-0061-aaa";
    private const string MarkedName = "Test Course Marked Old";
    private const string FirstName = "Test Course Plain One";
    private const string SecondName = "Test Course Plain Two";
    private const string HiddenName = "Test Course Hidden Other";
    private const string OtherName = "Test Course Unmarked Other";

    private static readonly string[] Students = [Mc.Email("mk.s1"), Mc.Email("mk.s2"), Mc.Email("mk.s3")];

    private static readonly DateTimeOffset Meeting = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private sealed record World(long Marked, long First, long Second, long Hidden, long Other);

    /// <summary>
    /// Marked (all three students, 100 %, "not in Classroom" since D1), First and Second (two of three, 66 % each) are the
    /// teacher's; Hidden ("not visible" since D2) and Other are not; one meeting of <see cref="Code"/> with the three.
    /// </summary>
    private static async Task<World> SeedAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        var marked = await host.SeedRosterCourseAsync(MarkedName, [Teacher], Students, ct);
        var first = await host.SeedRosterCourseAsync(FirstName, [Teacher], Students[..2], ct);
        var second = await host.SeedRosterCourseAsync(SecondName, [Teacher], Students[1..], ct);
        var hidden = await host.SeedRosterCourseAsync(HiddenName, [Mc.Email("mk.teacher2")], [], ct);
        var other = await host.SeedRosterCourseAsync(OtherName, [Mc.Email("mk.teacher3")], [], ct);
        await host.SeedMeetingAsync(Code, Teacher, Meeting, ct, Students);
        await host.MarkCourseAsync(marked, "not_in_classroom", DataMarksWorld.D1, ct);
        await host.MarkCourseAsync(hidden, "not_visible_to_technical_account", DataMarksWorld.D2, ct);
        return new World(marked, first, second, hidden, other);
    }

    /// <summary>The markup of one course's choice: from its radio control to the next course control or the end of its section.</summary>
    private static string ChoiceOf(string html, long courseId)
    {
        var control = Regex.Match(html, $"<input[^>]*name=\"{Mc.Fields.CourseId}\"[^>]*value=\"{Id(courseId)}\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(control.Success, $"Course {courseId} is not offered.");
        var rest = html[(control.Index + control.Length)..];
        var end = Regex.Match(rest, $"name=\"{Mc.Fields.CourseId}\"|</fieldset>|</form>", RegexOptions.IgnoreCase);
        return Mc.VisibleText(end.Success ? rest[..end.Index] : rest);
    }

    private static string CurrentCourseOf(string html)
    {
        var start = html.IndexOf("id=\"meet-code-current-course\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The current course is not shown.");
        var end = html.IndexOf("<form", start, StringComparison.Ordinal);
        return Mc.VisibleText(html[start..end]);
    }

    /// <summary>AC-005, FR-002.1, VR-002: the code is listed with the two unmarked candidates and their shares, not the marked one.</summary>
    [Theory]
    [InlineData(Actor.Dean)]
    [InlineData(Actor.Admin)]
    public async Task UnlinkedMeetings_OfferTheUnmarkedCandidatesOnly(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Mc.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains(Code, text, StringComparison.Ordinal);
        Assert.Contains(FirstName, text, StringComparison.Ordinal);
        Assert.Contains(SecondName, text, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(text, @"(?<![\d.])66\s*%"));
        Assert.DoesNotContain(MarkedName, text, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"(?<![\d.])100\s*%", text);
        Assert.DoesNotContain(host.Text(Mc.Keys.NoCandidates, "uk"), text, StringComparison.Ordinal);
    }

    /// <summary>FR-002.2: a code whose only candidate is marked stays listed, with no candidate.</summary>
    [Fact]
    public async Task ACodeWhoseOnlyCandidateIsMarked_IsListedWithNoCandidate()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        var marked = await host.SeedRosterCourseAsync(MarkedName, [Teacher], Students, ct);
        await host.SeedMeetingAsync(Code, Teacher, Meeting, ct, Students);
        await host.MarkCourseAsync(marked, "not_visible_to_technical_account", DataMarksWorld.D2, ct);

        var page = await client.GetAsync(Mc.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains(Code, text, StringComparison.Ordinal);
        Assert.DoesNotContain(MarkedName, text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Mc.Keys.NoCandidates, "uk"), text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-010, FR-002.3, FR-003.1, FR-003.2: on the choice page the marked courses are offered without a share and with
    /// their mark and date; the candidates and the unmarked other course carry no mark; every course can be chosen.
    /// </summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheChoicePage_ShowsTheMarkNextToEachMarkedCourse_AndOffersEveryCourse(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await MeetReportsHostExtensions.StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        var texts = DataMarksTexts.Of(language);

        var page = await client.GetAsync(Mc.ChoicePath(Code), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(
            new[] { world.Marked, world.First, world.Second, world.Hidden, world.Other }.Order(),
            Mc.OfferedCourseIds(page.Body).Order());
        var marked = ChoiceOf(page.Body, world.Marked);
        Assert.Contains(texts.With(texts.CourseNotInClassroomSince, DataMarksWorld.D1), marked, StringComparison.Ordinal);
        Assert.DoesNotContain("%", marked, StringComparison.Ordinal);
        var hidden = ChoiceOf(page.Body, world.Hidden);
        Assert.Contains(texts.With(texts.CourseNotVisibleSince, DataMarksWorld.D2), hidden, StringComparison.Ordinal);
        foreach (var plain in new[] { world.First, world.Second, world.Other })
        {
            var choice = ChoiceOf(page.Body, plain);
            Assert.DoesNotContain(texts.Date(DataMarksWorld.D1), choice, StringComparison.Ordinal);
            Assert.DoesNotContain(texts.Date(DataMarksWorld.D2), choice, StringComparison.Ordinal);
        }

        Assert.Contains("66", ChoiceOf(page.Body, world.First), StringComparison.Ordinal);
        Assert.Contains("66", ChoiceOf(page.Body, world.Second), StringComparison.Ordinal);
    }

    /// <summary>AC-010, FR-003.2: a code linked to a marked course shows the mark next to the current course.</summary>
    [Fact]
    public async Task TheChoicePage_OfACodeLinkedToAMarkedCourse_ShowsTheMarkNextToTheCurrentCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.SeedAutoLinkAsync(Code, world.Marked, new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero), ct);

        var page = await client.GetAsync(Mc.ChoicePath(Code), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var current = CurrentCourseOf(page.Body);
        Assert.Contains(MarkedName, current, StringComparison.Ordinal);
        Assert.Contains(DataMarksTexts.Uk.With(DataMarksTexts.Uk.CourseNotInClassroomSince, DataMarksWorld.D1), current, StringComparison.Ordinal);
        Assert.DoesNotContain(world.Marked, Mc.OfferedCourseIds(page.Body));
    }

    /// <summary>AC-006, FR-003.3: choosing a marked course links the code to it as a person's link, with the usual audit.</summary>
    [Fact]
    public async Task ChoosingAMarkedCourse_LinksTheCode_AsAPersonsLink_WithTheUsualAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        var dean = await host.AccountIdAsync(Mc.EmailOf(Actor.Dean), ct);

        var response = await client.PostWithTokenAsync(
            Mc.LinkPath(Code),
            [Mc.Field(Mc.Fields.CourseId, world.Marked), Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned)],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var link = Assert.Single(await host.McLinkRowsAsync(ct));
        Assert.Equal(Code, link.MeetingCode);
        Assert.Equal(world.Marked, link.CourseId);
        Assert.False(link.LinkedAutomatically);
        Assert.Equal(dean, link.LinkedBy);
        var audit = Assert.Single(await host.McAuditRowsAsync(ct));
        Assert.Equal(Mc.Actions.Picked, audit.Action);
        Assert.Equal("succeeded", audit.Outcome);
        Assert.Equal(world.Marked, audit.TargetId);
        Assert.Equal(Code, audit.MeetCode);
    }
}
