using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-032 over HTTP, GET side: the "Meet meetings" page (spec FR-003, FR-007, FR-015, FR-017; AC-003, AC-008, AC-010,
/// AC-011, AC-012, AC-013, AC-014, AC-015, AC-017; VR-004; TC-8; api-design §2.2, §2.7). Rows are seeded by SQL.
/// </summary>
public sealed class MeetCodesPageTests(PostgreSqlFixture database)
{
    private const string TeacherEmail = "mc.teacher1@school-one.example.test";
    private const string GuestOrganizer = "mc.guest@school-one.example.test";
    private const string CodeWithCandidate = "abc-0001-xyz";
    private const string CodeNoCandidate = "abc-0002-xyz";
    private const string AlphaName = "Test Course Alpha";
    private const string BetaName = "Test Course Beta";
    private const string AardvarkName = "Test Course Aardvark";

    private static readonly string[] Students = [Mc.Email("mc.s1"), Mc.Email("mc.s2"), Mc.Email("mc.s3")];

    private sealed record World(long Alpha, long Beta, long Aardvark);

    /// <summary>Alpha has the teacher and two of the three counted students; seven meetings of one code; one code without candidates.</summary>
    private static async Task<World> SeedAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        var alpha = await host.SeedRosterCourseAsync(AlphaName, [TeacherEmail], Students[..2], ct);
        var beta = await host.SeedRosterCourseAsync(BetaName, [Mc.Email("mc.teacher2")], [], ct);
        var aardvark = await host.SeedRosterCourseAsync(AardvarkName, [Mc.Email("mc.teacher3")], [], ct);
        for (var day = 4; day <= 10; day++)
        {
            await host.SeedMeetingAsync(
                CodeWithCandidate, TeacherEmail, new DateTimeOffset(2026, 9, day, 9, 0, 0, TimeSpan.Zero), ct, Students);
        }

        await host.SeedMeetingAsync(
            CodeNoCandidate, GuestOrganizer, new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero), ct);
        return new World(alpha, beta, aardvark);
    }

    /// <summary>AC-003, AC-013, FR-007: the Unassigned list shows the code, organizer, count, candidate and rounded-down share.</summary>
    [Theory]
    [InlineData(Actor.Dean)]
    [InlineData(Actor.Admin)]
    public async Task Unassigned_ShowsCodeOrganizerCountAndCandidateWithShareRoundedDown(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Mc.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains(CodeWithCandidate, text, StringComparison.Ordinal);
        Assert.Contains(TeacherEmail, text, StringComparison.Ordinal);
        Assert.Contains(AlphaName, text, StringComparison.Ordinal);
        Assert.Matches(@"(?<![\d.])66\s*%", text);
        Assert.DoesNotMatch(@"(?<![\d.])67\s*%", text);
        Assert.Matches(@"(?<![\w.\-:/])7(?![\w.\-:/%])", text);
        Assert.Contains(host.Text(Mc.Keys.Title, "uk"), text, StringComparison.Ordinal);
    }

    /// <summary>AC-013, FR-007: a code with no candidate comes after the codes with candidates, even with a later meeting.</summary>
    [Fact]
    public async Task ACodeWithNoCandidates_ComesAfterCodesWithCandidates_AndSaysSo()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Mc.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        var first = text.IndexOf(CodeWithCandidate, StringComparison.Ordinal);
        var second = text.IndexOf(CodeNoCandidate, StringComparison.Ordinal);
        Assert.True(first >= 0 && second >= 0, "Both codes must be listed.");
        Assert.True(first < second, "The code with candidates must come first.");
        var noCandidates = host.Text(Mc.Keys.NoCandidates, "uk");
        Assert.Equal(1, Regex.Count(text, Regex.Escape(noCandidates)));
        Assert.True(text.IndexOf(noCandidates, StringComparison.Ordinal) > first);
    }

    /// <summary>FR-007: the Linked list — course, how and by whom, confirmer; confirm control only for an automatic unconfirmed link.</summary>
    [Fact]
    public async Task Linked_ShowsCourseMakerConfirmerAndTheConfirmFormOnlyWhereOffered()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.InsertAppUserAsync(ct, email: Mc.Email("mc.maker"));
        await host.InsertAppUserAsync(ct, email: Mc.Email("mc.confirmer"));
        var maker = await host.AccountIdAsync(Mc.Email("mc.maker"), ct);
        var confirmer = await host.AccountIdAsync(Mc.Email("mc.confirmer"), ct);
        var at = new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
        await host.SeedAutoLinkAsync("lnk-0001-aaa", world.Alpha, at, ct);
        await host.SeedPersonLinkAsync("lnk-0002-bbb", world.Beta, maker, at, ct);
        await host.SeedAutoLinkAsync("lnk-0003-ccc", world.Aardvark, at, ct, confirmer, at.AddHours(1));
        await host.SeedPersonLinkAsync("lnk-0004-ddd", world.Alpha, 987654, at, ct);

        var page = await client.GetAsync(Mc.Url(Mc.Lists.Linked), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        foreach (var code in new[] { "lnk-0001-aaa", "lnk-0002-bbb", "lnk-0003-ccc", "lnk-0004-ddd" })
        {
            Assert.Contains(code, text, StringComparison.Ordinal);
        }

        Assert.Contains(host.Text(Mc.Keys.Automatically, "uk"), text, StringComparison.Ordinal);
        Assert.Contains(Mc.Email("mc.maker"), text, StringComparison.Ordinal);
        Assert.Contains(Mc.Email("mc.confirmer"), text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Mc.Keys.DeletedAccount, "uk"), text, StringComparison.Ordinal);
        Assert.DoesNotContain(CodeWithCandidate, text, StringComparison.Ordinal);
        var confirmations = Mc.ConfirmationActions(page.Body);
        var only = Assert.Single(confirmations);
        Assert.Contains("/lnk-0001-aaa/confirmation", only, StringComparison.Ordinal);
    }

    /// <summary>FR-007: the Not a course list shows the marker's email.</summary>
    [Fact]
    public async Task NotACourse_ShowsTheMarkersEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);
        await host.InsertAppUserAsync(ct, email: Mc.Email("mc.marker"));
        var marker = await host.AccountIdAsync(Mc.Email("mc.marker"), ct);
        await host.SeedMarkAsync("mrk-0001-aaa", marker, new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero), ct);

        var page = await client.GetAsync(Mc.Url(Mc.Lists.NotACourse), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains("mrk-0001-aaa", text, StringComparison.Ordinal);
        Assert.Contains(Mc.Email("mc.marker"), text, StringComparison.Ordinal);
    }

    /// <summary>API-8, VR-004: 25 unassigned codes — 20 on page 0, 5 on page 1, size=5 gives 5.</summary>
    [Theory]
    [InlineData(null, null, 20)]
    [InlineData("1", null, 5)]
    [InlineData(null, "5", 5)]
    public async Task Unassigned_IsPaginated(string? page, string? size, int expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        await host.ExecuteAsync(
            """
            INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
            SELECT 'pg-conf-' || g, 'pg-' || lpad(g::text, 4, '0') || '-aaa', 'mc.pager@school-one.example.test',
                   @base + (g || ' hours')::interval, @base + (g || ' hours')::interval + interval '1 hour', @base, @base
            FROM generate_series(1, 25) AS g
            """,
            ct,
            ("base", new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));

        var response = await client.GetAsync(Mc.Url(page: page, size: size), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var codes = Regex.Matches(Mc.VisibleText(response.Body), @"pg-\d{4}-aaa").Select(m => m.Value).Distinct().ToList();
        Assert.Equal(expected, codes.Count);
        if (page == "1")
        {
            Assert.Equal(Enumerable.Range(1, 5).Select(i => $"pg-{i:D4}-aaa").Order(StringComparer.Ordinal), codes.Order(StringComparer.Ordinal));
        }
    }

    /// <summary>VR-004: an invalid or repeated query is 400, and the body is the page with the QueryInvalid message.</summary>
    [Theory]
    [InlineData("list=bogus")]
    [InlineData("page=-1")]
    [InlineData("page=abc")]
    [InlineData("size=0")]
    [InlineData("size=101")]
    [InlineData("list=linked&list=unassigned")]
    public async Task AnInvalidQuery_Is400_WithThePageAndTheMessage(string query)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Mc.Path + "?" + query, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(host.Text(Mc.Keys.MessageQueryInvalid, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(Mc.Keys.Title, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>TC-8, FR-002: first seen 21:30 UTC and the meeting 21:45 UTC fall on the same Kyiv day, so the teacher is a candidate.</summary>
    [Fact]
    public async Task AtTheKyivDayBoundary_TheTeacherIsStillACandidate()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        await host.SeedRosterCourseAsync(
            "Test Course Boundary", ["mc.boundary@school-one.example.test"], [], ct,
            firstSeenAt: new DateTimeOffset(2026, 9, 10, 21, 30, 0, TimeSpan.Zero));
        await host.SeedMeetingAsync(
            "bnd-0001-aaa", "mc.boundary@school-one.example.test", new DateTimeOffset(2026, 9, 10, 21, 45, 0, TimeSpan.Zero), ct);

        var page = await client.GetAsync(Mc.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        Assert.Contains("bnd-0001-aaa", text, StringComparison.Ordinal);
        Assert.Contains("Test Course Boundary", text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(Mc.Keys.NoCandidates, "uk"), text, StringComparison.Ordinal);
    }

    /// <summary>FR-015, AC-015: viewing works in every read-only cause.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheListsAreStillShown(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct, cause);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.SeedAutoLinkAsync("lnk-0001-aaa", world.Alpha, new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero), ct);

        var unassigned = await client.GetAsync(Mc.Path, ct);
        var linked = await client.GetAsync(Mc.Url(Mc.Lists.Linked), ct);

        Assert.Equal(HttpStatusCode.OK, unassigned.Status);
        Assert.Contains(CodeWithCandidate, unassigned.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, linked.Status);
        Assert.Contains("lnk-0001-aaa", linked.Text, StringComparison.Ordinal);
    }

    /// <summary>FR-007, AC-014: the landing page links to the Meet meetings page for both roles.</summary>
    [Theory]
    [InlineData(Actor.Dean)]
    [InlineData(Actor.Admin)]
    public async Task TheLandingPage_LinksToTheMeetCodesPage(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, landing.Status);
        Assert.Contains($"href=\"{Mc.Path}\"", landing.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text(Mc.Keys.NavigationEntry, "uk"), landing.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-008, FR-008: candidates first with their share, then the other courses by name.</summary>
    [Fact]
    public async Task TheChoiceForm_ListsCandidatesFirst_ThenOtherCoursesByName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);

        var page = await client.GetAsync(Mc.ChoicePath(CodeWithCandidate, 3), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var text = Mc.VisibleText(page.Body);
        var alpha = text.IndexOf(AlphaName, StringComparison.Ordinal);
        var aardvark = text.IndexOf(AardvarkName, StringComparison.Ordinal);
        var beta = text.IndexOf(BetaName, StringComparison.Ordinal);
        Assert.True(alpha >= 0 && aardvark > alpha && beta > aardvark, "Alpha (candidate), then Aardvark, then Beta.");
        Assert.Matches(@"(?<![\d.])66\s*%", text);
        Assert.Equal(new[] { world.Alpha, world.Aardvark, world.Beta }.Order(), Mc.OfferedCourseIds(page.Body).Order());
        Assert.Equal(Mc.States.Unassigned, Html.InputValue(page.Body, Mc.Fields.ExpectedState));
        Assert.Null(Html.InputValue(page.Body, Mc.Fields.ExpectedCourseId));
        Assert.Equal("3", Html.InputValue(page.Body, Mc.Fields.ReturnPage));
    }

    /// <summary>AC-010: for a linked code the current course is not offered and the expected state is carried.</summary>
    [Fact]
    public async Task TheChoiceForm_OfALinkedCode_OmitsTheCurrentCourse_AndCarriesTheExpectedState()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.SeedAutoLinkAsync("lnk-0001-aaa", world.Alpha, new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero), ct);

        var page = await client.GetAsync(Mc.ChoicePath("lnk-0001-aaa"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var offered = Mc.OfferedCourseIds(page.Body);
        Assert.DoesNotContain(world.Alpha, offered);
        Assert.Contains(world.Beta, offered);
        Assert.Equal(Mc.States.Linked, Html.InputValue(page.Body, Mc.Fields.ExpectedState));
        Assert.Equal(world.Alpha.ToString(System.Globalization.CultureInfo.InvariantCulture), Html.InputValue(page.Body, Mc.Fields.ExpectedCourseId));
    }

    /// <summary>AC-012: a marked code's form carries expectedState marked.</summary>
    [Fact]
    public async Task TheChoiceForm_OfAMarkedCode_CarriesTheMarkedState()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await host.InsertAppUserAsync(ct, email: Mc.Email("mc.marker"));
        var marker = await host.AccountIdAsync(Mc.Email("mc.marker"), ct);
        await host.SeedMarkAsync("mrk-0001-aaa", marker, new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero), ct);

        var page = await client.GetAsync(Mc.ChoicePath("mrk-0001-aaa"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Mc.States.Marked, Html.InputValue(page.Body, Mc.Fields.ExpectedState));
        Assert.Contains(world.Alpha, Mc.OfferedCourseIds(page.Body));
    }

    /// <summary>api-design §2.7: the choice form of an unknown code is 404, the page with CodeNotFound.</summary>
    [Fact]
    public async Task TheChoiceForm_OfAnUnknownCode_Is404_WithTheCodeNotFoundMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Mc.ChoicePath("zzz-9999-nnn"), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Contains(host.Text(Mc.Keys.MessageCodeNotFound, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>FR-006, FR-017: a run that failed in the linking step is named in the Admin's Last synchronization block.</summary>
    [Fact]
    public async Task AFailedLinkingStep_IsNamedInTheAdminsLastSynchronizationBlock()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, "uk", ct);
        await using var _host = host;
        using var _admin = admin;
        await host.ExecuteAsync(
            """
            INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count,
                                    last_error, last_successful_run_at, meet_loaded_up_to, failed_step, created_at, updated_at)
            VALUES (true, 'failed', @runId, @startedAt, @finishedAt, 0, 'Unexpected', NULL, NULL, 'linking', @stamp, @stamp)
            """,
            ct,
            ("runId", Guid.NewGuid()),
            ("startedAt", LastSynchronizationTestData.Started),
            ("finishedAt", LastSynchronizationTestData.Finished),
            ("stamp", host.Time.GetUtcNow()));

        var page = await admin.OpenSettingsAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(Mc.Keys.StepLinking, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(MeetTestData.Keys.StepMeet, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(MeetTestData.Keys.StepClassroom, "uk"), page.Text, StringComparison.Ordinal);
    }
}
