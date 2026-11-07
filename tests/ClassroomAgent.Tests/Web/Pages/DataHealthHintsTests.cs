using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.DataHealthHostExtensions;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cause = ClassroomAgent.Tests.TestInfrastructure.ReadOnlyModeHost.Cause;
using Kind = ClassroomAgent.Tests.TestInfrastructure.DataHealthHintTexts.TechnicalKind;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-075 FR-002 … FR-006, FR-009 over HTTP and PostgreSQL (TC-2): the hint under each kind of problem on «Стан даних» —
/// the teaching hints for both roles, the technical hints for the Admin only, the Dean's X4 title without the Admin's
/// instruction (OD-001 a), none in the archived section, none for X5 in read-only mode, in both languages (NFR-073), with
/// no script, inline style, Google call or audit event. The markup it reads is fixed by the test strategy §3. Data is
/// synthetic (TC-4).
/// </summary>
public sealed class DataHealthHintsTests(PostgreSqlFixture database)
{
    private const string ConnectionLink = "href=\"/settings/workspace-connection\"";

    private static readonly DateTimeOffset D1 = new(2026, 9, 10, 21, 30, 0, TimeSpan.Zero);

    public static TheoryData<Actor, string> RolesAndLanguages => new()
    {
        { Actor.Dean, "uk" },
        { Actor.Dean, "en" },
        { Actor.Admin, "uk" },
        { Actor.Admin, "en" },
    };

    public static TheoryData<Kind, string> KindsAndLanguages
    {
        get
        {
            var data = new TheoryData<Kind, string>();
            foreach (var kind in Enum.GetValues<Kind>())
            {
                data.Add(kind, "uk");
                data.Add(kind, "en");
            }

            return data;
        }
    }

    private static async Task<(InstallationTestHost Host, FormClient Client)> StartAsync(
        PostgreSqlFixture database, Actor actor, string language, CancellationToken ct, Cause cause = Cause.NotReadOnly)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct, cause);
        if (language != "uk")
        {
            var chosen = await client.ChooseLanguageAsync("/", language, ct);
            Assert.Equal(HttpStatusCode.Redirect, chosen.Status);
        }

        await host.ReadyToSeedAsync(ct);
        return (host, client);
    }

    private static async Task<string> PageAsync(FormClient client, CancellationToken ct)
    {
        var page = await client.GetAsync(PagePath, ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        return page.Body;
    }

    /// <summary>One technical kind, alone: X2a 25 hours since success, X2b no run ever, X3 a skip, X4 an empty list, X5 a mark.</summary>
    private static async Task SeedAsync(InstallationTestHost host, Kind kind, CancellationToken ct)
    {
        switch (kind)
        {
            case Kind.NoRecentSuccess:
                await host.RecentSuccessAsync(ct, TimeSpan.FromHours(25));
                break;
            case Kind.NoSuccessYet:
                break;
            case Kind.UnknownCourseState:
                await host.RecentSuccessAsync(ct);
                await host.SkipAsync(CourseTestData.CourseId(7), "Test Unknown State Course", UnknownStateReason, D1, ct);
                break;
            case Kind.CourseListEmpty:
                var success = host.Time.GetUtcNow().AddHours(-1);
                await host.SyncStateAsync(ct, startedAt: success.AddMinutes(-5), lastSuccessfulRunAt: success, emptySince: D1);
                break;
            case Kind.CoursesNotVisible:
                await host.RecentSuccessAsync(ct);
                var hidden = await host.CourseAsync(1, "Test Hidden Course", ct);
                await host.MarkCourseAsync(hidden, "not_visible_to_technical_account", D1, ct);
                break;
        }
    }

    /// <summary>The title of a technical kind as the Admin reads it (US-062 FR-007, X4 with its date).</summary>
    private static string AdminTitle(Kind kind, DataHealthTexts texts) => kind switch
    {
        Kind.NoRecentSuccess => texts.NoRecentSuccess,
        Kind.NoSuccessYet => texts.NoSuccessYet,
        Kind.UnknownCourseState => texts.UnknownCourseState,
        Kind.CourseListEmpty => texts.With(texts.CourseListEmptySince, D1),
        Kind.CoursesNotVisible => texts.CoursesNotVisible,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static int At(string text, string what, int from = 0)
    {
        var at = text.IndexOf(what, from, StringComparison.Ordinal);
        Assert.True(at >= 0, $"Not found after {from}: {what}");
        return at;
    }

    /// <summary>FR-009.1, FR-009.2, SEC-004: a hint paragraph is a leaf — no link, script, button or expandable element inside.</summary>
    private static void AssertLeaf((string Inner, string Text, int Position) hint) =>
        Assert.DoesNotContain("<", hint.Inner, StringComparison.Ordinal);

    // ---- Teaching hints (AC-003, FR-002) -----------------------------------------------------------------------------

    /// <summary>
    /// AC-003, FR-002.1 … FR-002.3, FR-008: course A has T1 (two grades) and T2, course B T1, course C T2 — each teaching
    /// hint appears once per course that has it, with the agreed text, for the Dean and the Admin, in both languages.
    /// </summary>
    [Theory]
    [MemberData(nameof(RolesAndLanguages))]
    public async Task EachTeachingHint_AppearsOncePerCourseThatHasIt(Actor actor, string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, actor, language, ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);
        var a = await host.CourseAsync(1, "Test Course A", ct);
        await host.GradedAsync(a, 1, "Test Item One", 10m, "Overone", 11m, null, ct);
        await host.GradedAsync(a, 2, "Test Item Two", 5m, "Overtwo", 6m, null, ct);
        await host.ItemNotInClassroomAsync(a, 3, "Test Item Gone A", D1, ct);
        var b = await host.CourseAsync(2, "Test Course B", ct);
        await host.GradedAsync(b, 4, "Test Item Three", 10m, "Overthree", 12m, null, ct);
        var c = await host.CourseAsync(3, "Test Course C", ct);
        await host.ItemNotInClassroomAsync(c, 5, "Test Item Gone C", D1, ct);
        var hints = DataHealthHintTexts.Of(language);

        var teaching = HintParagraphs(SectionHtml(await PageAsync(client, ct), TeachingHook));

        Assert.Equal(2, teaching.Count(h => h.Text == hints.AboveMaximum));
        Assert.Equal(2, teaching.Count(h => h.Text == hints.ItemsNotInClassroom));
        Assert.All(teaching.Where(h => h.Text == hints.AboveMaximum || h.Text == hints.ItemsNotInClassroom), AssertLeaf);
        Assert.DoesNotContain(teaching, h => hints.Technical.Contains(h.Text));
    }

    /// <summary>AC-003, FR-002.1, FR-002.2, I-1: each teaching hint sits under its kind's heading line, before the kind's list.</summary>
    [Fact]
    public async Task TheTeachingHints_SitUnderTheHeading_BeforeTheList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, "uk", ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);
        var graded = await host.CourseAsync(1, "Test Graded Course", ct);
        await host.GradedAsync(graded, 1, "Test Graded Item", 10m, "Overmax", 12m, null, ct);
        var gone = await host.CourseAsync(2, "Test Gone Course", ct);
        await host.ItemNotInClassroomAsync(gone, 2, "Test Gone Item", D1, ct);
        var uk = DataHealthTexts.Uk;
        var hints = DataHealthHintTexts.Uk;

        var teaching = Section(await PageAsync(client, ct), TeachingHook);

        var aboveHeading = At(teaching, uk.AboveMaximum);
        var aboveHint = At(teaching, hints.AboveMaximum, aboveHeading);
        Assert.True(aboveHint < At(teaching, "Test Graded Item", aboveHeading), "The T1 hint is not before its list.");
        var goneHeading = At(teaching, uk.ItemsNotInClassroom);
        var goneHint = At(teaching, hints.ItemsNotInClassroom, goneHeading);
        Assert.True(goneHint < At(teaching, "Test Gone Item", goneHeading), "The T2 hint is not before its list.");
    }

    // ---- Technical hints for the Admin (AC-004, FR-003) --------------------------------------------------------------

    /// <summary>
    /// AC-004, FR-003, FR-008: each of X2a, X2b, X3, X4, X5 alone — the Admin sees its title and, after it, its hint once
    /// with the agreed text, in both languages; the connection page link follows the hint for X2a and X2b only.
    /// </summary>
    [Theory]
    [MemberData(nameof(KindsAndLanguages))]
    public async Task TheAdmin_SeesEachTechnicalKindsHint_AfterItsTitle(Kind kind, string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, language, ct);
        await using var _host = host;
        await SeedAsync(host, kind, ct);
        var hints = DataHealthHintTexts.Of(language);
        var expected = hints.Of(kind);

        var technical = SectionHtml(await PageAsync(client, ct), TechnicalHook);

        var hint = Assert.Single(HintParagraphs(technical));
        Assert.Equal(expected, hint.Text);
        AssertLeaf(hint);
        var text = Text(technical);
        Assert.True(At(text, AdminTitle(kind, DataHealthTexts.Of(language))) < At(text, expected), "The hint is not after the title.");
        if (kind is Kind.NoRecentSuccess or Kind.NoSuccessYet)
        {
            Assert.True(technical.IndexOf(ConnectionLink, hint.Position, StringComparison.Ordinal) > hint.Position, "No connection link after the hint.");
        }
        else
        {
            Assert.DoesNotContain(ConnectionLink, technical, StringComparison.Ordinal);
        }
    }

    /// <summary>FR-003 table, FR-003.3: the X5 hint precedes its list and form; the X3 hint follows the course name.</summary>
    [Fact]
    public async Task TheX5Hint_PrecedesTheForm_AndTheX3Hint_FollowsTheCourseName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, "uk", ct);
        await using var _host = host;
        await SeedAsync(host, Kind.CoursesNotVisible, ct);
        await host.SkipAsync(CourseTestData.CourseId(7), "Test Unknown State Course", UnknownStateReason, D1, ct);
        var hints = DataHealthHintTexts.Uk;

        var technical = SectionHtml(await PageAsync(client, ct), TechnicalHook);

        var x5 = Assert.Single(HintParagraphs(technical), h => h.Text == hints.CoursesNotVisible);
        Assert.True(x5.Position < At(technical, $"id=\"{FormHook}\""), "The X5 hint is not before the form.");
        Assert.True(x5.Position < At(technical, "Test Hidden Course"), "The X5 hint is not before the list.");
        var x3 = Assert.Single(HintParagraphs(technical), h => h.Text == hints.UnknownCourseState);
        Assert.True(At(technical, "Test Unknown State Course") < x3.Position, "The X3 hint is not after the course name.");
    }

    /// <summary>AC-005 last clause, FR-004.2, I-5: the Admin's X4 title stays whole, with the instruction to check the role.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheAdminsX4Title_StaysWhole(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, language, ct);
        await using var _host = host;
        await SeedAsync(host, Kind.CourseListEmpty, ct);
        var texts = DataHealthTexts.Of(language);

        var technical = Section(await PageAsync(client, ct), TechnicalHook);

        Assert.Contains(texts.With(texts.CourseListEmptySince, D1), technical, StringComparison.Ordinal);
    }

    // ---- The Dean and technical problems (AC-005, FR-004) ------------------------------------------------------------

    /// <summary>
    /// AC-005, FR-004.1, SEC-002: each technical kind alone — the Dean sees «Повідомте Адміністратора», none of the
    /// FR-003 hints in either language and no connection page link.
    /// </summary>
    [Theory]
    [MemberData(nameof(KindsAndLanguages))]
    public async Task TheDean_SeesNoTechnicalHint_OnlyTheHintToTellTheAdmin(Kind kind, string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        await SeedAsync(host, kind, ct);

        var body = await PageAsync(client, ct);

        var technical = SectionHtml(body, TechnicalHook);
        Assert.Empty(HintParagraphs(technical));
        Assert.Contains(DataHealthTexts.Of(language).TellTheAdmin, Text(technical), StringComparison.Ordinal);
        var text = Text(body);
        foreach (var hint in DataHealthHintTexts.Uk.Technical.Concat(DataHealthHintTexts.En.Technical))
        {
            Assert.DoesNotContain(hint, text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(ConnectionLink, body, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-005, FR-004.2, OD-001 a: the Dean's X4 title ends at its date — «Google не повертає жодного курсу з &lt;дата&gt;» —
    /// with no instruction to check the technical account's role, in both languages.
    /// </summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheDeansX4Title_EndsAtItsDate(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, language, ct);
        await using var _host = host;
        await SeedAsync(host, Kind.CourseListEmpty, ct);
        var texts = DataHealthTexts.Of(language);
        var hints = DataHealthHintTexts.Of(language);

        var technical = Section(await PageAsync(client, ct), TechnicalHook);

        Assert.Contains(texts.With(hints.DeanCourseListEmptySince, D1), technical, StringComparison.Ordinal);
        Assert.DoesNotContain(hints.AdminRoleInstruction, technical, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(texts.With(texts.CourseListEmptySince, D1), technical, StringComparison.Ordinal);
    }

    // ---- Archived courses (AC-006, FR-006) ---------------------------------------------------------------------------

    /// <summary>
    /// AC-006, FR-006.1, I-3: an archived course with T1, T2 and an unknown state — the archived section shows none of the
    /// new hints, and its marks and existing texts are as before (the Dean keeps «Повідомте Адміністратора»).
    /// </summary>
    [Theory]
    [InlineData(Actor.Dean)]
    [InlineData(Actor.Admin)]
    public async Task TheArchivedSection_ShowsNoNewHint(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, actor, "uk", ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);
        var archived = await host.CourseAsync(1, "Test Archived Course", ct, state: CourseTestData.States.Archived);
        await host.GradedAsync(archived, 1, "Test Archived Item", 10m, "Archivedstudent", 14m, null, ct);
        await host.ItemNotInClassroomAsync(archived, 2, "Test Archived Gone Item", D1, ct);
        await host.SkipAsync(CourseTestData.CourseId(1), "Test Archived Course", UnknownStateReason, D1, ct);
        var uk = DataHealthTexts.Uk;

        var section = SectionHtml(await PageAsync(client, ct), ArchivedHook);

        Assert.Empty(HintParagraphs(section));
        var text = Text(section);
        foreach (var hint in DataHealthHintTexts.Uk.All)
        {
            Assert.DoesNotContain(hint, text, StringComparison.Ordinal);
        }

        Assert.Contains(uk.AboveMaximum, text, StringComparison.Ordinal);
        Assert.Contains("Test Archived Item", text, StringComparison.Ordinal);
        Assert.Contains(uk.ItemsNotInClassroom, text, StringComparison.Ordinal);
        Assert.Contains("Test Archived Gone Item", text, StringComparison.Ordinal);
        Assert.Contains(uk.UnknownCourseState, text, StringComparison.Ordinal);
        if (actor == Actor.Dean)
        {
            Assert.Contains(uk.TellTheAdmin, text, StringComparison.Ordinal);
        }
    }

    // ---- Read-only mode (AC-007, FR-005) -----------------------------------------------------------------------------

    /// <summary>
    /// AC-007, FR-005.1, FR-005.4: in read-only mode (the Owner suspended the installation) the T1, T2, X3 and X4 hints are
    /// shown to the Admin as outside it.
    /// </summary>
    [Theory]
    [InlineData("T1")]
    [InlineData("T2")]
    [InlineData("X3")]
    [InlineData("X4")]
    public async Task InReadOnlyMode_TheHintsThatPromiseNothingBlocked_AreShown(string kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, "uk", ct, Cause.Suspended);
        await using var _host = host;
        var hints = DataHealthHintTexts.Uk;
        string expected, hook;
        switch (kind)
        {
            case "T1":
                await host.RecentSuccessAsync(ct);
                var graded = await host.CourseAsync(1, "Test Graded Course", ct);
                await host.GradedAsync(graded, 1, "Test Graded Item", 10m, "Overmax", 12m, null, ct);
                (expected, hook) = (hints.AboveMaximum, TeachingHook);
                break;
            case "T2":
                await host.RecentSuccessAsync(ct);
                var gone = await host.CourseAsync(1, "Test Gone Course", ct);
                await host.ItemNotInClassroomAsync(gone, 1, "Test Gone Item", D1, ct);
                (expected, hook) = (hints.ItemsNotInClassroom, TeachingHook);
                break;
            case "X3":
                await SeedAsync(host, Kind.UnknownCourseState, ct);
                (expected, hook) = (hints.UnknownCourseState, TechnicalHook);
                break;
            default:
                await SeedAsync(host, Kind.CourseListEmpty, ct);
                (expected, hook) = (hints.CourseListEmpty, TechnicalHook);
                break;
        }

        var body = await PageAsync(client, ct);

        Assert.True(Has(body, ReadOnlyHook));
        Assert.Single(HintParagraphs(SectionHtml(body, hook)), h => h.Text == expected);
    }

    /// <summary>
    /// AC-007, FR-005.3, I-4: in read-only mode the X5 problem shows its title, its list and the form as today, and no hint —
    /// each of its sentences names something the mode blocks.
    /// </summary>
    [Fact]
    public async Task InReadOnlyMode_TheX5Problem_HasNoHint()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, "uk", ct, Cause.Suspended);
        await using var _host = host;
        await SeedAsync(host, Kind.CoursesNotVisible, ct);

        var body = await PageAsync(client, ct);

        var technical = SectionHtml(body, TechnicalHook);
        Assert.Contains(DataHealthTexts.Uk.CoursesNotVisible, Text(technical), StringComparison.Ordinal);
        Assert.Contains("Test Hidden Course", Text(technical), StringComparison.Ordinal);
        Assert.True(Has(body, FormHook));
        Assert.Empty(HintParagraphs(technical));
        Assert.DoesNotContain(DataHealthHintTexts.Uk.CoursesNotVisible, Text(body), StringComparison.Ordinal);
    }

    // ---- Nothing else changed (AC-008, FR-009.2, SEC-004, SEC-005) ---------------------------------------------------

    /// <summary>
    /// AC-008, FR-009.2, SEC-004, SEC-005: with a problem of every hinted kind the page shows its hints with no script and
    /// no inline style, calls no substituted Google port and writes no audit event.
    /// </summary>
    [Fact]
    public async Task ThePageWithEveryHint_UsesNoScript_CallsNoGoogleApi_AndWritesNoAuditEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, "uk", ct);
        await using var _host = host;
        await SeedAsync(host, Kind.CourseListEmpty, ct);
        await host.SkipAsync(CourseTestData.CourseId(7), "Test Unknown State Course", UnknownStateReason, D1, ct);
        var hidden = await host.CourseAsync(1, "Test Hidden Course", ct);
        await host.MarkCourseAsync(hidden, "not_visible_to_technical_account", D1, ct);
        var graded = await host.CourseAsync(2, "Test Graded Course", ct);
        await host.GradedAsync(graded, 1, "Test Graded Item", 10m, "Overmax", 12m, null, ct);
        await host.ItemNotInClassroomAsync(graded, 2, "Test Gone Item", D1, ct);
        var auditBefore = await AuditAsync(host, ct);
        var classroomBefore = host.Classroom.ImpersonatedAs.Count;
        var meetBefore = host.Meet.Calls.Count;

        var body = await PageAsync(client, ct);

        var hints = HintParagraphs(SectionHtml(body, TechnicalHook)).Concat(HintParagraphs(SectionHtml(body, TeachingHook))).ToList();
        Assert.True(hints.Count >= 5, $"Expected at least five hints, found {hints.Count}.");
        Assert.DoesNotContain("<script", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", SectionHtml(body, TechnicalHook), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", SectionHtml(body, TeachingHook), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(classroomBefore, host.Classroom.ImpersonatedAs.Count);
        Assert.Equal(meetBefore, host.Meet.Calls.Count);
        Assert.Equal(auditBefore, await AuditAsync(host, ct));
    }

    /// <summary>Every audit row but the retention purge's own (US-037 writes it on its schedule), as one text.</summary>
    private static async Task<string> AuditAsync(InstallationTestHost host, CancellationToken ct) =>
        Assert.Single(await host.QueryAsync(
            "SELECT coalesce(string_agg(row_to_json(a)::text, '|' ORDER BY id), '') FROM audit_event a WHERE action <> 'retention_purge_run'",
            r => r.GetString(0),
            ct));
}
