using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Help = ClassroomAgent.Tests.TestInfrastructure.PageHelpTestData;
using Hint = ClassroomAgent.Tests.TestInfrastructure.PageSearchHintTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-076: a «?» beside the title opens short help on the journal, the Meet codes, the Meet report list, the one-meeting page
/// and the report template list — a native disclosure, collapsed on every load, with the approved texts from the translation
/// files, the same for Admin and Dean and in read-only mode, writing and storing nothing; and on no other page of the
/// installation or the Control Plane (spec v2 FR-001 … FR-009, AC-001 … AC-006; api-design §3).
/// </summary>
public sealed class PageHelpTests(PostgreSqlFixture database)
{
    /// <summary>The scripts the five views already load (journal and Meet report pickers, Meet export); the help adds none.</summary>
    private static readonly string[] ExistingScripts = ["/js/picker-narrow.js", "/js/meet-export.js"];

    public static TheoryData<Actor, string> RolesAndLanguages => new()
    {
        { Actor.Admin, "uk" },
        { Actor.Admin, "en" },
        { Actor.Dean, "uk" },
        { Actor.Dean, "en" },
    };

    public static TheoryData<ReadOnlyModeHost.Cause> ReadOnlyCauses => new(
        ReadOnlyModeHost.Cause.Suspended,
        ReadOnlyModeHost.Cause.GracePeriodExpired,
        ReadOnlyModeHost.Cause.NeverConfirmed);

    private static string PathOf(string page, Mr.SeededWorld world) => page switch
    {
        Help.Pages.Journal => JournalTestData.Path,
        Help.Pages.MeetCodes => Mc.Path,
        Help.Pages.MeetReports => MeetReportsTestData.Url(),
        Help.Pages.Meeting => MeetReportsTestData.MeetingUrl(
            world.MeetingA.ToString(CultureInfo.InvariantCulture),
            world.CourseId,
            MeetReportsTestData.Period.FromText,
            MeetReportsTestData.Period.ToText),
        Help.Pages.ReportTemplates => ReportTemplateTestData.ListPath,
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
    };

    /// <summary>
    /// Asserts the page carries exactly one help disclosure for <paramref name="pageId"/> in <paramref name="language"/>:
    /// collapsed (AC-001, AC-003), beside the title with nothing between (FR-002, FR-004), its summary the first child, showing
    /// «?» with the accessible name (AC-001), its expandable part holding exactly the approved lines as paragraphs in order and
    /// nothing else (AC-004, FR-005, FR-009), with no link, form, script or style (FR-008, SC-15); and, where the page has the
    /// page-search hint, the hint first after the title block (AC-002, NFR-075).
    /// </summary>
    private static void AssertHelp(PageResponse page, string pageId, string language, string what)
    {
        Assert.True(page.Status == HttpStatusCode.OK || page.Status == HttpStatusCode.BadRequest || page.Status == HttpStatusCode.NotFound,
            $"{what}: unexpected status {page.Status}.");
        var disclosures = Help.Disclosures(page.Body);
        Assert.True(disclosures.Count == 1, $"{what}: {disclosures.Count} <details> elements instead of exactly one help control.");
        var help = disclosures[0];

        Assert.False(help.IsOpen, $"{what}: the help is rendered open.");
        Assert.True(help.SummaryAttributes is not null, $"{what}: the help has no <summary> control.");
        Assert.True(Help.IsOnlyWrapperMarkup(help.BeforeSummary), $"{what}: the <summary> is not the first child of the help.");

        var name = Help.ControlName(language);
        var visible = help.SummaryText;
        var named = help.AriaLabel == name || visible.Contains(name, StringComparison.Ordinal);
        Assert.True(named, $"{what}: the control's accessible name is not «{name}» (aria-label «{help.AriaLabel}», text «{visible}»).");
        Assert.Equal("?", visible.Replace(name, string.Empty, StringComparison.Ordinal).Trim());

        Assert.Equal(Help.Lines(pageId, language), help.Paragraphs);
        Assert.True(help.BodyLeftover.Length == 0, $"{what}: the help holds more than its lines: «{help.BodyLeftover}».");
        Assert.Empty(Help.ForbiddenMarkup(help.Attributes + " " + help.SummaryAttributes + " " + help.SummaryHtml + help.BodyHtml));

        var between = Help.BetweenTitleAndHelp(page.Body, help);
        Assert.True(between is not null, $"{what}: the help does not follow the page title.");
        Assert.True(Help.IsOnlyWrapperMarkup(between!), $"{what}: something stands between the title and the help: «{between}».");

        if (Help.Pages.WithSearchHint.Contains(pageId))
        {
            Assert.True(
                Hint.StartsWithHint(Help.AfterHelp(page.Body, help)),
                $"{what}: the page-search hint is not the first element after the title block.");
            Assert.True(Hint.StartsWithHint(Hint.AfterTitle(page.Body, 1)), $"{what}: the hint helper does not see the hint after the title block.");
        }
    }

    private async Task<(InstallationTestHost Host, FormClient Client, Mr.SeededWorld World)> StartAsync(
        Actor actor,
        CancellationToken ct,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct, cause);
        var world = await host.SeedWorldAsync(ct);
        return (host, client, world);
    }

    /// <summary>AC-001, AC-002, AC-004 (FR-001 … FR-006, FR-008): each of the five pages, for Admin and Dean, in both languages.</summary>
    [Theory]
    [MemberData(nameof(RolesAndLanguages))]
    public async Task EachOfTheFivePages_ShowsItsHelp_Collapsed_BesideTheTitle(Actor actor, string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartAsync(actor, ct);
        await using var _host = host;
        if (language == UiLanguageTestData.English)
        {
            await client.ChooseLanguageAsync(JournalTestData.Path, UiLanguageTestData.English, ct);
        }

        foreach (var pageId in Help.Pages.All)
        {
            var page = await client.GetAsync(PathOf(pageId, world), ct);

            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
            AssertHelp(page, pageId, language, $"{pageId} ({actor}, {language})");
        }
    }

    /// <summary>AC-001, FR-008, BR-026: read-only mode, of every cause, shows the very same help.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyCauses))]
    public async Task InReadOnlyMode_TheHelpIsTheSame(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartAsync(Actor.Dean, ct, cause);
        await using var _host = host;

        foreach (var pageId in Help.Pages.All)
        {
            var page = await client.GetAsync(PathOf(pageId, world), ct);

            Assert.Equal(HttpStatusCode.OK, page.Status);
            AssertHelp(page, pageId, "uk", $"{pageId} in read-only mode ({cause})");
        }
    }

    /// <summary>FR-001, spec §8: a page that renders its own view with nothing in it — no course chosen, no rows, no codes — shows the help.</summary>
    [Theory]
    [InlineData(Help.Pages.Journal)]
    [InlineData(Help.Pages.MeetCodes)]
    [InlineData(Help.Pages.MeetReports)]
    [InlineData(Help.Pages.ReportTemplates)]
    public async Task WithNoRowsAtAll_TheHelpIsShown(string pageId)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var noWorld = new Mr.SeededWorld(0, 0, 0, 0, 0, 0, 0, 0);

        var page = await client.GetAsync(PathOf(pageId, noWorld), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertHelp(page, pageId, "uk", $"{pageId} with no rows");
    }

    /// <summary>FR-001, spec §8: the journal with a validation message on its own view (400, 404) still shows the help.</summary>
    [Theory]
    [InlineData("?courseId=abc", HttpStatusCode.BadRequest)]
    [InlineData("?courseId=987654", HttpStatusCode.NotFound)]
    public async Task TheJournalWithAValidationMessage_StillShowsTheHelp(string query, HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.Path + query, ct);

        Assert.Equal(status, page.Status);
        Assert.True(Html.HasInput(page.Body, JournalTestData.Parameters.From), "The journal form is not rendered.");
        AssertHelp(page, Help.Pages.Journal, "uk", "the journal with a message");
    }

    /// <summary>
    /// AC-003, AC-006 (FR-003, FR-008, SC-15): every load of the five pages — twice, and again after a language change —
    /// renders the help collapsed; the help sets no cookie, writes no teaching row and no audit event, adds no script, and the
    /// browser security headers stay exactly as SC-15 fixes them.
    /// </summary>
    [Fact]
    public async Task EveryLoad_IsCollapsed_NothingIsStoredOrWritten_AndTheHeadersAreUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartAsync(Actor.Admin, ct);
        await using var _host = host;
        var teachingBefore = await host.TeachingFingerprintAsync(ct);
        var auditBefore = (await host.AuditRowsAsync(ct)).Count;
        // The session and antiforgery cookies; the TempData cookie some pages already use for one-time messages. None is the help's.
        var allowedCookies = new[]
        {
            SignInTestData.SessionCookieName, SignInTestData.AntiforgeryCookieName, ".AspNetCore.Mvc.CookieTempDataProvider",
        };

        async Task VisitAllAsync(string language, string round)
        {
            foreach (var pageId in Help.Pages.All)
            {
                var page = await client.GetAsync(PathOf(pageId, world), ct);

                AssertHelp(page, pageId, language, $"{pageId} ({round})");
                Assert.All(InstallationSignInExtensions.SetCookieNames(page), cookie => Assert.Contains(cookie, allowedCookies));
                Assert.All(Help.ScriptSources(page.Body), src => Assert.Contains(src, ExistingScripts));
                Assert.Empty(SecurityHeaders.Problems(SecurityHeaders.Of(page), SecurityHeaders.InstallationCsp));
            }
        }

        await VisitAllAsync("uk", "visit 1");
        await VisitAllAsync("uk", "visit 2");
        await client.ChooseLanguageAsync(JournalTestData.Path, UiLanguageTestData.English, ct);
        await VisitAllAsync("en", "after choosing English");

        Assert.Equal(teachingBefore, await host.TeachingFingerprintAsync(ct));
        Assert.Equal(auditBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    /// <summary>AC-004, FR-006, api-design §3: the 18 keys hold exactly the approved texts in both translation files, and no other <c>PageHelp.</c> key exists.</summary>
    [Fact]
    public async Task TheTranslationFiles_HoldExactlyTheApprovedTexts()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, _client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _c = _client;

        foreach (var language in new[] { "uk", "en" })
        {
            foreach (var pageId in Help.Pages.All)
            {
                var keys = Help.LineKeys(pageId);
                var expected = Help.Lines(pageId, language);
                for (var i = 0; i < keys.Count; i++)
                {
                    Assert.Equal(expected[i], host.Text(keys[i], language));
                }
            }

            Assert.Equal(Help.ControlName(language), host.Text(Help.ControlNameKey, language));
            var defined = host.AllTexts(language).Keys.Where(k => k.StartsWith("PageHelp.", StringComparison.Ordinal)).Order(StringComparer.Ordinal);
            Assert.Equal(Help.AllKeys.Order(StringComparer.Ordinal), defined);
        }
    }

    /// <summary>
    /// AC-004, FR-006: no help text is written in a view, partial, stylesheet, script or C# file of either host — measured on
    /// the translation files, which must hold every fragment, so the scan is not vacuous.
    /// </summary>
    [Fact]
    public void NoSourceFile_HoldsAHelpText()
    {
        var src = Path.Combine(StaticFiles.RepositoryRoot(), "src");
        var resx = Directory.EnumerateFiles(src, "SharedResource.*.resx", SearchOption.AllDirectories)
            .Where(f => f.Contains(Path.Combine("ClassroomAgent.Application", "Localization"), StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .Aggregate(string.Empty, (all, text) => all + System.Net.WebUtility.HtmlDecode(text));
        Assert.All(Help.LiteralFragments, f => Assert.Contains(f, resx, StringComparison.Ordinal));

        var offenders = Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f =>
            {
                var text = File.ReadAllText(f);
                return Help.LiteralFragments.Any(fragment => text.Contains(fragment, StringComparison.Ordinal));
            })
            .Select(f => Path.GetRelativePath(src, f))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// AC-005, FR-001, Story OD-001: no other installation page — the sub-pages of the five, the other teaching-data pages,
    /// settings, accounts, the landing page, error pages, sign-in — carries the help; measured on a host whose journal does
    /// carry it, so the absence is not an accident of a page that renders nothing.
    /// </summary>
    [Fact]
    public async Task NoOtherInstallationPage_HasTheHelp()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin, world) = await StartAsync(Actor.Admin, ct);
        await using var _host = host;
        using var _admin = admin;
        var templateId = (await SeedTemplateAsync(host, ct)).ToString(CultureInfo.InvariantCulture);
        var dean = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);
        using var _dean = dean;
        var anonymous = host.CreateClient();
        using var _anonymous = anonymous;

        var adminPages = new[]
        {
            SignInTestData.LandingPath,
            CourseListTestData.Path,
            CourseCardTestData.Path(world.CourseId),
            TeacherListTestData.Path,
            SearchTestData.Url("Курс"),
            ReportTemplateTestData.SeptemberReportUrl(world.CourseId),
            ReportTemplateTestData.NewPath,
            ReportTemplateTestData.EditPath(templateId),
            ReportTemplateTestData.DeletionPath(templateId),
            Mc.ChoicePath(Mr.Codes.Linked),
            DatabaseStatisticsTestData.Path,
            "/workspace/data-health",
            DeanAccountTestData.Paths.Deans,
            WorkspaceConnectionTestData.Path,
            ConnectionInstructionTestData.Path,
            AccessCheckTestData.Path,
            ErasureTestData.PagePath,
        };
        foreach (var path in adminPages)
        {
            var page = await admin.GetAsync(path, ct);
            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.False(Help.HasAnyHelp(page.Body), $"The help is on {path}.");
        }

        var deanPages = new[] { SignInTestData.LandingPath, DeanAccountTestData.Paths.OwnPassword, CourseListTestData.Path, "/workspace/data-health" };
        foreach (var path in deanPages)
        {
            var page = await dean.GetAsync(path, ct);
            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.False(Help.HasAnyHelp(page.Body), $"The help is on the Dean's {path}.");
        }

        var errors = new[]
        {
            await admin.GetAsync(CourseCardTestData.Path(987654), ct),
            await admin.GetAsync(SignInTestData.ErrorPath(404), ct),
            await admin.GetAsync(SignInTestData.ErrorPath(500), ct),
            await anonymous.GetAsync(SignInTestData.ErrorPath(404), ct),
        };
        Assert.Equal(HttpStatusCode.NotFound, errors[0].Status);
        Assert.All(errors, page => Assert.False(Help.HasAnyHelp(page.Body), "The help is on an error page."));

        var signIn = await anonymous.GetAsync(SignInTestData.SignInPath, ct);
        Assert.Equal(HttpStatusCode.OK, signIn.Status);
        Assert.False(Help.HasAnyHelp(signIn.Body), "The help is on the sign-in page.");

        var control = await admin.GetAsync(JournalTestData.Path, ct);
        Assert.Single(Help.Disclosures(control.Body));
    }

    /// <summary>AC-005, FR-001: the forced password change of a Dean with a temporary password has no help.</summary>
    [Fact]
    public async Task TheForcedPasswordChange_HasNoHelp()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;

        var page = await client.GetAsync(DeanAccountTestData.Paths.ForcedChange, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.False(Help.HasAnyHelp(page.Body), "The help is on the forced password change.");
    }

    /// <summary>AC-005, FR-001, NFR-079: no Control Plane page — setup, sign-in, the Owner's pages — carries the help.</summary>
    [Fact]
    public async Task NoControlPlanePage_HasTheHelp()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var controlPlane = await ControlPlaneTestHost.StartAsync(database, ct);
        var anonymous = controlPlane.CreateClient();
        using var _anonymous = anonymous;

        var setup = await anonymous.GetAsync("/setup", ct);
        var owner = await controlPlane.CreateOwnerAsync(ct);
        using var _owner = owner;
        var home = await owner.GetAsync("/", ct);
        var installations = await owner.GetAsync("/installations", ct);
        var visitor = controlPlane.CreateClient();
        using var _visitor = visitor;
        var signIn = await visitor.GetAsync("/sign-in", ct);

        foreach (var page in new[] { setup, home, installations, signIn })
        {
            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.False(Help.HasAnyHelp(page.Body), "The help is on a Control Plane page.");
        }
    }

    /// <summary>The reader is not vacuous: it finds a collapsed help, sees an open one, and rejects extra content and links.</summary>
    [Fact]
    public void TheReader_TellsAProperHelpFromABrokenOne()
    {
        const string proper =
            "<main><h1>Журнал</h1> <details class=\"x\"><summary aria-label=\"Довідка про сторінку\">?</summary>"
            + "<div><p>A.</p><p>B &amp; C.</p></div></details>\n<p id=\"page-search-hint\" class=\"page-search-hint\">h</p></main>";
        var help = Assert.Single(Help.Disclosures(proper));
        Assert.False(help.IsOpen);
        Assert.Equal("Довідка про сторінку", help.AriaLabel);
        Assert.Equal("?", help.SummaryText);
        Assert.Equal(["A.", "B & C."], help.Paragraphs);
        Assert.Empty(help.BodyLeftover);
        Assert.True(Help.IsOnlyWrapperMarkup(Help.BetweenTitleAndHelp(proper, help)!));
        Assert.True(Hint.StartsWithHint(Help.AfterHelp(proper, help)));
        Assert.True(Hint.StartsWithHint(Hint.AfterTitle(proper, 1)));
        Assert.True(Help.HasAnyHelp(proper));
        var outside = Help.WithoutHelp(proper);
        Assert.Equal(
            "<main><h1>Журнал</h1> \n<p id=\"page-search-hint\" class=\"page-search-hint\">h</p></main>",
            outside);
        Assert.False(Help.HasAnyHelp(outside));
        Assert.Equal(outside, Help.WithoutHelp(outside));

        var open = proper.Replace("<details class=\"x\">", "<details open class=\"x\">", StringComparison.Ordinal);
        Assert.True(Assert.Single(Help.Disclosures(open)).IsOpen);

        var extra = proper.Replace("<p>A.</p>", "<p>A.</p><table><tr><td>row</td></tr></table>", StringComparison.Ordinal);
        Assert.NotEmpty(Assert.Single(Help.Disclosures(extra)).BodyLeftover);

        var link = proper.Replace("<p>A.</p>", "<p><a href=\"/x\">A.</a></p>", StringComparison.Ordinal);
        var linked = Assert.Single(Help.Disclosures(link));
        Assert.NotEmpty(Help.ForbiddenMarkup(linked.BodyHtml));

        var below = "<main><h1>Журнал</h1><p>text</p><details><summary>?</summary></details></main>";
        Assert.False(Help.IsOnlyWrapperMarkup(Help.BetweenTitleAndHelp(below, Assert.Single(Help.Disclosures(below)))!));

        Assert.False(Help.HasAnyHelp("<main><h1>Курси</h1><p>nothing</p></main>"));
    }

    private static async Task<long> SeedTemplateAsync(InstallationTestHost host, CancellationToken ct)
    {
        const string author = "author.one@school-one.example.test";
        await host.InsertAppUserAsync(ct, email: author);
        var authorId = await host.AccountIdAsync(author, ct);
        return await host.InsertTemplateAsync(ct, "Test Template Help", authorId);
    }
}
