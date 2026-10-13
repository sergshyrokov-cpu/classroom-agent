using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Hint = ClassroomAgent.Tests.TestInfrastructure.PageSearchHintTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;
using Variants = ClassroomAgent.Tests.TestInfrastructure.PageSearchHintTestData.Variants;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-046: the page-search hint — one line under the title of every teaching-data page, of the variant spec FR-001
/// assigns, in the user's language, in every state the page renders, writing nothing; and on no other page of the
/// installation or the Control Plane (spec FR-001 … FR-007; api-design §3 markup hook and placement; OD-001 a,
/// OD-002 a).
/// </summary>
public sealed class PageSearchHintTests(PostgreSqlFixture database)
{
    /// <summary>The nine teaching-data pages of spec FR-001, by name.</summary>
    private static readonly string[] TeachingPageNames =
        ["course-list", "course-card", "teacher-list", "journal", "report", "meet-reports", "meeting", "meet-codes", "database"];

    public static TheoryData<string> TeachingPages => new(TeachingPageNames);

    public static TheoryData<string, Actor> TeachingPagesByRole
    {
        get
        {
            var data = new TheoryData<string, Actor>();
            foreach (var page in TeachingPageNames)
            {
                data.Add(page, Actor.Admin);
                data.Add(page, Actor.Dean);
            }

            return data;
        }
    }

    public static TheoryData<string> Languages => new("uk", "en");

    /// <summary>Spec FR-001: the variant of each page.</summary>
    private static string VariantOf(string page) => page switch
    {
        "course-list" => Variants.CourseList,
        "teacher-list" or "meet-codes" => Variants.PagedList,
        _ => Variants.Ordinary,
    };

    /// <summary>Api-design §3: the hint follows the layout title, on Meet codes the view's own second title (OD-002 a).</summary>
    private static int TitleOrdinalOf(string page) => page == "meet-codes" ? 2 : 1;

    private static string PathOf(string page, Mr.SeededWorld world) => page switch
    {
        "course-list" => CourseListTestData.Path,
        "course-card" => CourseCardTestData.Path(world.CourseId),
        "teacher-list" => TeacherListTestData.Path,
        "journal" => JournalTestData.Path,
        "report" => ReportTemplateTestData.SeptemberReportUrl(world.CourseId),
        "meet-reports" => MeetReportsTestData.Url(),
        "meeting" => MeetReportsTestData.MeetingUrl(
            world.MeetingA.ToString(CultureInfo.InvariantCulture),
            world.CourseId,
            MeetReportsTestData.Period.FromText,
            MeetReportsTestData.Period.ToText),
        "meet-codes" => Mc.Path,
        "database" => DatabaseStatisticsTestData.Path,
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
    };

    /// <summary>
    /// Asserts the page carries exactly one hint (AC-008), of the given variant and text (AC-001 … AC-003), as a
    /// leaf <c>&lt;p&gt;</c> with the hook id and class — no child element, so no close control and no script
    /// (AC-004) — placed first after the title (AC-009).
    /// </summary>
    private static void AssertHint(PageResponse page, string variant, string language, int titleOrdinal, string what)
    {
        var hint = Assert.Single(Hint.Hints(page.Body));
        Assert.Equal("p", hint.Tag);
        Assert.Contains($"id=\"{Hint.Hook}\"", page.Body, StringComparison.Ordinal);
        Assert.Equal(variant, hint.Variant);
        Assert.DoesNotContain("<", hint.InnerHtml, StringComparison.Ordinal);
        Assert.Equal(Hint.Expected(variant, language), hint.Text);
        Assert.True(
            Hint.StartsWithHint(Hint.AfterTitle(page.Body, titleOrdinal)),
            $"On {what} the hint is not the first element after title #{titleOrdinal}.");
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

    /// <summary>AC-001, AC-002, AC-003, AC-004 (both roles), AC-008, AC-009: the variant of the page, once, under the title.</summary>
    [Theory]
    [MemberData(nameof(TeachingPagesByRole))]
    public async Task EveryTeachingDataPage_ShowsItsHintOnce_DirectlyUnderTheTitle(string name, Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartAsync(actor, ct);
        await using var _host = host;

        var page = await client.GetAsync(PathOf(name, world), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertHint(page, VariantOf(name), "uk", TitleOrdinalOf(name), name);
    }

    /// <summary>AC-001 … AC-003, AC-006: after choosing English every teaching-data page shows the English text.</summary>
    [Theory]
    [MemberData(nameof(TeachingPages))]
    public async Task InEnglish_EveryTeachingDataPage_ShowsTheEnglishText(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartAsync(Actor.Admin, ct);
        await using var _host = host;
        var path = PathOf(name, world);
        await client.ChooseLanguageAsync(path, UiLanguageTestData.English, ct);

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(UiLanguageTestData.English, UiLanguageTestData.PageLanguage(page.Body));
        AssertHint(page, VariantOf(name), "en", TitleOrdinalOf(name), name);
    }

    /// <summary>AC-002, FR-004: the course-list hint quotes the search field's label of the same language.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheCourseListHint_QuotesTheSearchFieldLabel(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);
        if (language == UiLanguageTestData.English)
        {
            await client.ChooseLanguageAsync(CourseListTestData.Path, UiLanguageTestData.English, ct);
        }

        var page = await client.GetAsync(CourseListTestData.Path, ct);

        var label = host.Text("Courses.Form.Search", language);
        var quoted = language == "uk" ? $"«{label}»" : $"\"{label}\"";
        var hint = Assert.Single(Hint.Hints(page.Body));
        Assert.Contains(quoted, hint.Text, StringComparison.Ordinal);
        var afterHint = WebUtility.HtmlDecode(page.Body[(hint.Index + hint.InnerHtml.Length)..]);
        Assert.Contains(label, afterHint, StringComparison.Ordinal);
    }

    /// <summary>AC-004, FR-006: read-only mode changes nothing about the hint.</summary>
    [Theory]
    [MemberData(nameof(TeachingPages))]
    public async Task InReadOnlyMode_TheHintIsUnchanged(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartAsync(Actor.Dean, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;

        var page = await client.GetAsync(PathOf(name, world), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertHint(page, VariantOf(name), "uk", TitleOrdinalOf(name), name);
    }

    /// <summary>
    /// AC-004, FR-006: with no rows at all — a school with nothing synchronized — every teaching-data page that
    /// needs no course still shows its hint.
    /// </summary>
    [Theory]
    [InlineData("course-list")]
    [InlineData("teacher-list")]
    [InlineData("journal")]
    [InlineData("meet-reports")]
    [InlineData("meet-codes")]
    [InlineData("database")]
    public async Task WithNoRowsAtAll_TheHintIsShown(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var noWorld = new Mr.SeededWorld(0, 0, 0, 0, 0, 0, 0, 0);

        var page = await client.GetAsync(PathOf(name, noWorld), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertHint(page, VariantOf(name), "uk", TitleOrdinalOf(name), name);
    }

    /// <summary>AC-004, FR-006: the paged-list text is the same on one page and on any page of many.</summary>
    [Fact]
    public async Task ThePagedListHint_IsTheSame_OnOnePage_AndOnEveryPageOfMany()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var one = await client.GetAsync(TeacherListTestData.Path, ct);
        await host.SeedNumberedTeachersAsync(120, ct);

        var first = await client.GetAsync(TeacherListTestData.Path, ct);
        var last = await client.GetAsync(TeacherListTestData.Url(page: "3"), ct);

        foreach (var page in new[] { one, first, last })
        {
            Assert.Equal(HttpStatusCode.OK, page.Status);
            AssertHint(page, Variants.PagedList, "uk", 1, "the teacher list");
        }
    }

    /// <summary>
    /// AC-004, FR-006, spec §8: a teaching-data page that renders validation messages instead of data — the journal
    /// with a malformed course (400) or an unknown course (404) — still shows the hint.
    /// </summary>
    [Theory]
    [InlineData("?courseId=abc", HttpStatusCode.BadRequest)]
    [InlineData("?courseId=987654", HttpStatusCode.NotFound)]
    public async Task TheJournalWithAValidationMessage_StillShowsTheHint(string query, HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.Path + query, ct);

        Assert.Equal(status, page.Status);
        Assert.True(Html.HasInput(page.Body, JournalTestData.Parameters.From), "The journal form is not rendered.");
        AssertHint(page, Variants.Ordinary, "uk", 1, "the journal with a message");
    }

    /// <summary>AC-004, FR-007: the device is not detected — a phone browser gets the very same line.</summary>
    [Theory]
    [MemberData(nameof(TeachingPages))]
    public async Task APhoneBrowser_GetsTheSameHint(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartAsync(Actor.Admin, ct);
        await using var _host = host;
        var path = PathOf(name, world);
        var phone = new Dictionary<string, string>
        {
            ["User-Agent"] =
                "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) "
                + "Version/17.0 Mobile/15E148 Safari/604.1",
        };
        var computer = new Dictionary<string, string>
        {
            ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36",
        };

        var onPhone = await client.GetAsync(path, ct, phone);
        var onComputer = await client.GetAsync(path, ct, computer);

        AssertHint(onPhone, VariantOf(name), "uk", TitleOrdinalOf(name), name + " on a phone");
        Assert.Equal(Assert.Single(Hint.Hints(onComputer.Body)).InnerHtml, Assert.Single(Hint.Hints(onPhone.Body)).InnerHtml);
    }

    /// <summary>
    /// AC-004, FR-007, S-04: showing the hint on every teaching-data page, twice, writes no teaching row and no audit
    /// event, sets no cookie beyond the session and antiforgery cookies, and is shown again — nothing is remembered.
    /// </summary>
    [Fact]
    public async Task ShowingTheHint_WritesNothing_SetsNoNewCookie_AndIsNotRemembered()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartAsync(Actor.Admin, ct);
        await using var _host = host;
        var teachingBefore = await host.TeachingFingerprintAsync(ct);
        var auditBefore = (await host.AuditRowsAsync(ct)).Count;
        // The session and antiforgery cookies, and the TempData cookie the Database page already uses for its one-time
        // synchronization message (US-019, US-024) — none of them comes from the hint (Owner OK at IMPLEMENTATION).
        var allowedCookies = new[]
        {
            SignInTestData.SessionCookieName, SignInTestData.AntiforgeryCookieName, ".AspNetCore.Mvc.CookieTempDataProvider",
        };

        foreach (var round in new[] { 1, 2 })
        {
            foreach (var name in TeachingPageNames)
            {
                var response = await client.GetAsync(PathOf(name, world), ct);

                AssertHint(response, VariantOf(name), "uk", TitleOrdinalOf(name), $"{name} (visit {round})");
                Assert.All(
                    InstallationSignInExtensions.SetCookieNames(response),
                    cookie => Assert.Contains(cookie, allowedCookies));
            }
        }

        Assert.Equal(teachingBefore, await host.TeachingFingerprintAsync(ct));
        Assert.Equal(auditBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    /// <summary>
    /// AC-005, FR-005, OD-001 a: no other installation page carries the hint — measured on a host whose course list
    /// does carry it, so the absence is not an accident of a page that renders nothing.
    /// </summary>
    [Fact]
    public async Task NoOtherInstallationPage_HasTheHint()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin, world) = await StartAsync(Actor.Admin, ct);
        await using var _host = host;
        using var _admin = admin;
        var templateId = await SeedTemplateAsync(host, ct);
        var dean = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);
        using var _dean = dean;
        var anonymous = host.CreateClient();
        using var _anonymous = anonymous;

        var adminPages = new[]
        {
            SignInTestData.LandingPath,
            DeanAccountTestData.Paths.Deans,
            WorkspaceConnectionTestData.Path,
            ConnectionInstructionTestData.Path,
            AccessCheckTestData.Path,
            ReportTemplateTestData.ListPath,
            ReportTemplateTestData.NewPath,
            ReportTemplateTestData.EditPath(templateId.ToString(CultureInfo.InvariantCulture)),
            ReportTemplateTestData.DeletionPath(templateId.ToString(CultureInfo.InvariantCulture)),
            Mc.ChoicePath(Mr.Codes.Linked),
        };
        foreach (var path in adminPages)
        {
            var page = await admin.GetAsync(path, ct);
            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.False(Hint.HasAnyHint(page.Body), $"The hint is on {path}.");
        }

        var deanPages = new[] { SignInTestData.LandingPath, DeanAccountTestData.Paths.OwnPassword };
        foreach (var path in deanPages)
        {
            var page = await dean.GetAsync(path, ct);
            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.False(Hint.HasAnyHint(page.Body), $"The hint is on the Dean's {path}.");
        }

        var errors = new[]
        {
            await admin.GetAsync(CourseCardTestData.Path(987654), ct),
            await admin.GetAsync(SignInTestData.ErrorPath(404), ct),
            await admin.GetAsync(SignInTestData.ErrorPath(500), ct),
            await anonymous.GetAsync(SignInTestData.ErrorPath(404), ct),
        };
        Assert.Equal(HttpStatusCode.NotFound, errors[0].Status);
        Assert.All(errors, page => Assert.False(Hint.HasAnyHint(page.Body), "The hint is on the error page."));

        var signIn = await anonymous.GetAsync(SignInTestData.SignInPath, ct);
        Assert.Equal(HttpStatusCode.OK, signIn.Status);
        Assert.False(Hint.HasAnyHint(signIn.Body), "The hint is on the sign-in page.");

        var control = await admin.GetAsync(CourseListTestData.Path, ct);
        Assert.Single(Hint.Hints(control.Body));
    }

    /// <summary>AC-005, FR-005: the forced password change of a Dean with a temporary password has no hint.</summary>
    [Fact]
    public async Task TheForcedPasswordChange_HasNoHint()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;

        var page = await client.GetAsync(DeanAccountTestData.Paths.ForcedChange, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.False(Hint.HasAnyHint(page.Body), "The hint is on the forced password change.");
    }

    /// <summary>AC-005, FR-005: no Control Plane page — setup, sign-in, the Owner's pages — carries the hint.</summary>
    [Fact]
    public async Task NoControlPlanePage_HasTheHint()
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
        }

        foreach (var page in new[] { setup, home, installations, signIn })
        {
            Assert.False(Hint.HasAnyHint(page.Body), "The hint is on a Control Plane page.");
        }
    }

    private static async Task<long> SeedTemplateAsync(InstallationTestHost host, CancellationToken ct)
    {
        const string author = "author.one@school-one.example.test";
        await host.InsertAppUserAsync(ct, email: author);
        var authorId = await host.AccountIdAsync(author, ct);
        return await host.InsertTemplateAsync(ct, "Test Template Hint", authorId);
    }
}
