using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.DataHealthHostExtensions;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cause = ClassroomAgent.Tests.TestInfrastructure.ReadOnlyModeHost.Cause;
using Hint = ClassroomAgent.Tests.TestInfrastructure.PageSearchHintTestData;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-062 FR-002, FR-004, FR-005, FR-007 over HTTP and PostgreSQL (TC-2): the «Стан даних» page at
/// <c>GET /workspace/data-health</c> — who may open it (TC-5), its sections, the texts and hints of FR-007 / OD-003 in
/// both languages (NFR-073), the dates in the school's zone (NFR-074: night instants, so a UTC date fails), the Dean's
/// page without technical details (§2 notes on row 749), the read-only message, and the page-search hint. The page markup
/// it reads is fixed by the test strategy §3.2. Data is synthetic (TC-4).
/// </summary>
public sealed class DataHealthPageTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset D1 = new(2026, 9, 10, 21, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset D2 = new(2026, 9, 12, 22, 15, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset D3 = new(2026, 9, 5, 23, 45, 0, TimeSpan.Zero);

    private static readonly DataHealthTexts Uk = DataHealthTexts.Uk;

    private static async Task<(InstallationTestHost Host, FormClient Client)> StartAsync(
        PostgreSqlFixture database, Actor actor, CancellationToken ct, Cause cause = Cause.NotReadOnly)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct, cause);
        await host.ReadyToSeedAsync(ct);
        return (host, client);
    }

    private static async Task<string> PageAsync(FormClient client, CancellationToken ct)
    {
        var page = await client.GetAsync(PagePath, ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        return page.Body;
    }

    // ---- Access (TC-5, SC-4, api-design §4) ---------------------------------------------------------------------------

    /// <summary>§2 row 749, TC-5: the Dean and the Admin open the page; it is titled «Стан даних» and is not cached.</summary>
    [Theory]
    [InlineData(Actor.Dean)]
    [InlineData(Actor.Admin)]
    public async Task TheDeanAndTheAdmin_OpenThePage(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, actor, ct);
        await using var _host = host;

        var page = await client.GetAsync(PagePath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(Uk.PageTitle, page.Text, StringComparison.Ordinal);
        Assert.True(NoStore.IsIn(NoStore.Of(page)));
    }

    /// <summary>TC-5, api-design §4: without a session the page redirects to sign-in; a restricted session to the change.</summary>
    [Theory]
    [InlineData(Actor.Anonymous, "/sign-in")]
    [InlineData(Actor.DeanWithTemporaryPassword, ChangePasswordPath)]
    public async Task WithoutAFullSession_ThePageRedirects(Actor actor, string target)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, actor, ct);
        await using var _host = host;

        var page = await client.GetAsync(PagePath, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.StartsWith(target, page.LocationPath, StringComparison.Ordinal);
    }

    /// <summary>FR-005, NFR-075, I-7: the ordinary page-search hint is the first element under the title, in both languages.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task ThePage_CarriesTheOrdinaryPageSearchHint(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await DataMarksHostExtensions.StartAsync(database, language, ct);
        await using var _host = host;

        var body = await PageAsync(client, ct);

        var hint = Assert.Single(Hint.Hints(body));
        Assert.Equal(Hint.Variants.Ordinary, hint.Variant);
        Assert.Equal(Hint.Expected(Hint.Variants.Ordinary, language), hint.Text);
        Assert.True(Hint.StartsWithHint(Hint.AfterTitle(body, 1)));
    }

    // ---- Sections (FR-004.2) ----------------------------------------------------------------------------------------

    /// <summary>FR-004.2: with no problem at all the page says «Проблем із даними немає».</summary>
    [Fact]
    public async Task WithNoProblem_ThePageSaysSo()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);

        var text = Text(await PageAsync(client, ct));

        Assert.Contains(Uk.NoProblems!, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-004.2, OD-003: the sections in order — technical, teaching, archived — and a section with nothing to show says
    /// «Немає» in one line.
    /// </summary>
    [Fact]
    public async Task TheSections_AreInOrder_AndAnEmptyOneSaysNone()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SyncStateAsync(ct, status: "failed", lastError: "GoogleUnavailable", lastSuccessfulRunAt: host.Time.GetUtcNow().AddHours(-1));

        var body = await PageAsync(client, ct);

        var technical = body.IndexOf($"id=\"{TechnicalHook}\"", StringComparison.Ordinal);
        var teaching = body.IndexOf($"id=\"{TeachingHook}\"", StringComparison.Ordinal);
        var archived = body.IndexOf($"id=\"{ArchivedHook}\"", StringComparison.Ordinal);
        Assert.True(technical >= 0 && technical < teaching && teaching < archived, "Sections are not technical, teaching, archived.");
        Assert.Contains(Uk.LastRunFailed, Section(body, TechnicalHook), StringComparison.Ordinal);
        Assert.Contains(Uk.None, Section(body, TeachingHook), StringComparison.Ordinal);
        Assert.Contains(Uk.None, Section(body, ArchivedHook), StringComparison.Ordinal);
    }

    // ---- Teaching problems (AC-005, FR-004.3) -----------------------------------------------------------------------

    /// <summary>
    /// AC-005, FR-001.6, FR-004.3, FR-007, OD-003: every teaching kind on the Dean's page — the grade's item, student,
    /// points and maximum; the item gone with its date; T3 with its date, without one, never loaded by its stored name and
    /// untitled by its Google id; T4 with its date — the hints H1, H2 and H4, journal links for the loaded courses only.
    /// </summary>
    [Fact]
    public async Task TheDeansTeachingSection_ShowsEveryKindWithItsDetailsAndHints()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);
        var graded = await host.CourseAsync(1, "Test Graded Course", ct);
        await host.GradedAsync(graded, 1, "Test Graded Item", 10m, "Overmax", 12m, null, ct);
        await host.ItemNotInClassroomAsync(graded, 2, "Test Gone Item", D1, ct);
        var since = await host.CourseAsync(2, "Test Skipped Since", ct, lastCompleteReadAt: D2);
        await host.SkipAsync(CourseTestData.CourseId(2), "Test Skipped Since", ValueOutOfRangeReason, D3, ct);
        var noDate = await host.CourseAsync(3, "Test Skipped No Date", ct);
        await host.SkipAsync(CourseTestData.CourseId(3), "Test Skipped No Date", ValueOutOfRangeReason, D3, ct);
        await host.SkipAsync(CourseTestData.CourseId(4), "Test Never Loaded", ValueOutOfRangeReason, D3, ct);
        await host.SkipAsync(CourseTestData.CourseId(5), null, ValueOutOfRangeReason, D3, ct);
        var absent = await host.CourseAsync(6, "Test Absent Course", ct);
        await host.MarkCourseAsync(absent, "not_in_classroom", D2, ct);

        var body = await PageAsync(client, ct);
        var teaching = Section(body, TeachingHook);

        Assert.Contains(Uk.AboveMaximum, teaching, StringComparison.Ordinal);
        Assert.Contains("Test Graded Item", teaching, StringComparison.Ordinal);
        Assert.Contains("Overmax", teaching, StringComparison.Ordinal);
        Assert.Contains("12", teaching, StringComparison.Ordinal);
        Assert.Contains(Uk.ItemsNotInClassroom, teaching, StringComparison.Ordinal);
        Assert.Contains("Test Gone Item", teaching, StringComparison.Ordinal);
        Assert.Contains(Uk.Date(D1), teaching, StringComparison.Ordinal);
        Assert.Contains(Uk.With(Uk.NotUpdatedSince, D2), teaching, StringComparison.Ordinal);
        Assert.Contains(Uk.NotUpdated, teaching, StringComparison.Ordinal);
        Assert.Contains("Test Never Loaded", teaching, StringComparison.Ordinal);
        Assert.Equal(2, Occurrences(teaching, Uk.With(Uk.NeverLoaded, D3)));
        Assert.Contains(string.Format(Uk.Culture, Uk.Untitled, CourseTestData.CourseId(5)), teaching, StringComparison.Ordinal);
        Assert.Contains(Uk.With(Uk.CourseNotInClassroomSince, D2), teaching, StringComparison.Ordinal);
        Assert.Equal(4, Occurrences(teaching, Uk.HintSkipped));
        Assert.Equal(1, Occurrences(teaching, Uk.HintNotInClassroom));
        Assert.Contains(Uk.ContactTheAdmin, teaching, StringComparison.Ordinal);
        foreach (var id in new[] { graded, since, noDate, absent })
        {
            Assert.Contains($"href=\"/workspace/journal?courseId={id}\"", body, StringComparison.Ordinal);
        }

        Assert.Equal(4, Occurrences(body, "href=\"/workspace/journal?courseId="));
    }

    /// <summary>FR-004.3: within a course the kinds are in the order T4, T3, T2, T1.</summary>
    [Fact]
    public async Task WithinACourse_TheKindsAreInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);
        var course = await host.CourseAsync(1, "Test Everything Course", ct, lastCompleteReadAt: D3);
        await host.GradedAsync(course, 1, "Test Item", 10m, "Overmax", 12m, null, ct);
        await host.ItemNotInClassroomAsync(course, 2, "Test Gone Item", D1, ct);
        await host.SkipAsync(CourseTestData.CourseId(1), "Test Everything Course", ValueOutOfRangeReason, D2, ct);
        await host.MarkCourseAsync(course, "not_in_classroom", D2, ct);

        var teaching = Section(await PageAsync(client, ct), TeachingHook);

        var positions = new[]
        {
            teaching.IndexOf(Uk.With(Uk.CourseNotInClassroomSince, D2), StringComparison.Ordinal),
            teaching.IndexOf(Uk.With(Uk.NotUpdatedSince, D3), StringComparison.Ordinal),
            teaching.IndexOf(Uk.ItemsNotInClassroom, StringComparison.Ordinal),
            teaching.IndexOf(Uk.AboveMaximum, StringComparison.Ordinal),
        };
        Assert.All(positions, p => Assert.True(p >= 0));
        Assert.Equal(positions.Order(), positions);
    }

    /// <summary>FR-004.3, I-8: the hint to contact the Admin is the Dean's only; the Admin's teaching section lacks it.</summary>
    [Fact]
    public async Task TheAdmin_DoesNotSeeTheHintToContactTheAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);
        var absent = await host.CourseAsync(1, "Test Absent Course", ct);
        await host.MarkCourseAsync(absent, "not_in_classroom", D2, ct);

        var text = Text(await PageAsync(client, ct));

        Assert.Contains(Uk.With(Uk.CourseNotInClassroomSince, D2), text, StringComparison.Ordinal);
        Assert.DoesNotContain(Uk.ContactTheAdmin, text, StringComparison.Ordinal);
    }

    // ---- Archived (AC-003, FR-004.4, OD-004) --------------------------------------------------------------------------

    /// <summary>AC-003, FR-004.4: the archived course is under «Архівні курси (не враховуються)» with the grade's details only.</summary>
    [Fact]
    public async Task AnArchivedCourse_IsListedUnderItsOwnHeading()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);
        var archived = await host.CourseAsync(1, "Test Archived Course", ct, state: CourseTestData.States.Archived);
        await host.GradedAsync(archived, 1, "Test Archived Item", 10m, "Archivedstudent", 14m, null, ct);

        var body = await PageAsync(client, ct);

        var section = Section(body, ArchivedHook);
        Assert.Contains(Uk.ArchivedHeading!, section, StringComparison.Ordinal);
        Assert.Contains("Test Archived Course", section, StringComparison.Ordinal);
        Assert.Contains("Test Archived Item", section, StringComparison.Ordinal);
        Assert.Contains("Archivedstudent", section, StringComparison.Ordinal);
        Assert.Contains("14", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Test Archived Course", Section(body, TeachingHook), StringComparison.Ordinal);
    }

    /// <summary>OD-004 a: an archived course skipped for an unknown state — the Dean sees X3's title and H3, not the name.</summary>
    [Fact]
    public async Task AnArchivedUnknownStateCourse_ReachesTheDeanWithoutItsName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);
        await host.CourseAsync(1, "Test Archived Unknown", ct, state: CourseTestData.States.Archived);
        await host.SkipAsync(CourseTestData.CourseId(1), "Test Archived Unknown", UnknownStateReason, D1, ct);

        var body = await PageAsync(client, ct);

        var section = Section(body, ArchivedHook);
        Assert.Contains(Uk.UnknownCourseState, section, StringComparison.Ordinal);
        Assert.Contains(Uk.TellTheAdmin, section, StringComparison.Ordinal);
        Assert.DoesNotContain("Test Archived Unknown", body, StringComparison.Ordinal);
    }

    // ---- Technical problems (AC-006 … AC-009, AC-013, AC-014, FR-002.3) --------------------------------------------------

    /// <summary>
    /// AC-006, FR-002.3, spec §7: the run failed with a permission error and a course is skipped for an unknown state —
    /// the Dean sees both titles, each with «Повідомте Адміністратора», and neither the diagnosis nor the course name.
    /// </summary>
    [Fact]
    public async Task TheDean_SeesTechnicalTitlesWithTheHint_AndNoDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SyncStateAsync(
            ct, status: "failed", lastError: "ScopeNotAuthorized", failedStep: "classroom",
            lastSuccessfulRunAt: host.Time.GetUtcNow().AddHours(-2));
        await host.SkipAsync(CourseTestData.CourseId(7), "Test Unknown State Course", UnknownStateReason, D1, ct);

        var body = await PageAsync(client, ct);

        var technical = Section(body, TechnicalHook);
        Assert.Contains(Uk.LastRunFailed, technical, StringComparison.Ordinal);
        Assert.Contains(Uk.UnknownCourseState, technical, StringComparison.Ordinal);
        Assert.Equal(2, Occurrences(technical, Uk.TellTheAdmin));
        Assert.DoesNotContain(host.Text(AccessCheckTestData.TextKeys.OutcomeScopeNotAuthorized, "uk"), Text(body), StringComparison.Ordinal);
        Assert.DoesNotContain("Test Unknown State Course", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-006, FR-001.3, FR-002.3: the Admin sees the diagnosis text the connection page shows for the stored error, a
    /// link to the connection page, and the skipped course's name — and no «Повідомте Адміністратора».
    /// </summary>
    [Fact]
    public async Task TheAdmin_SeesTheDiagnosisAndTheCourseName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SyncStateAsync(
            ct, status: "failed", lastError: "ScopeNotAuthorized", failedStep: "classroom",
            lastSuccessfulRunAt: host.Time.GetUtcNow().AddHours(-2));
        await host.SkipAsync(CourseTestData.CourseId(7), "Test Unknown State Course", UnknownStateReason, D1, ct);

        var body = await PageAsync(client, ct);

        var technical = Section(body, TechnicalHook);
        Assert.Contains(Text(host.Text(AccessCheckTestData.TextKeys.OutcomeScopeNotAuthorized, "uk")), technical, StringComparison.Ordinal);
        Assert.Contains("Test Unknown State Course", technical, StringComparison.Ordinal);
        var start = body.IndexOf($"id=\"{TechnicalHook}\"", StringComparison.Ordinal);
        var end = body.IndexOf($"id=\"{TeachingHook}\"", start, StringComparison.Ordinal);
        Assert.Contains("href=\"/settings/workspace-connection\"", body[start..end], StringComparison.Ordinal);
        Assert.DoesNotContain(Uk.TellTheAdmin, technical, StringComparison.Ordinal);
    }

    /// <summary>VR-004: a stored error naming no known diagnosis is shown with the <c>Unexpected</c> text.</summary>
    [Fact]
    public async Task AnUnknownStoredError_IsShownWithTheUnexpectedText()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SyncStateAsync(
            ct, status: "failed", lastError: "A legacy error text", lastSuccessfulRunAt: host.Time.GetUtcNow().AddHours(-2));

        var technical = Section(await PageAsync(client, ct), TechnicalHook);

        Assert.Contains(
            Text(host.Text(LastSynchronizationTestData.Keys.DiagnosisUnexpected, "uk")), technical, StringComparison.Ordinal);
        Assert.DoesNotContain("A legacy error text", technical, StringComparison.Ordinal);
    }

    /// <summary>AC-007: 25 hours since the last success — «понад 24 години»; none ever — «ще не було».</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Staleness_IsWorded(bool everSucceeded)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        if (everSucceeded)
        {
            await host.RecentSuccessAsync(ct, TimeSpan.FromHours(25));
        }

        var technical = Section(await PageAsync(client, ct), TechnicalHook);

        Assert.Contains(everSucceeded ? Uk.NoRecentSuccess : Uk.NoSuccessYet, technical, StringComparison.Ordinal);
        Assert.DoesNotContain(everSucceeded ? Uk.NoSuccessYet : Uk.NoRecentSuccess, technical, StringComparison.Ordinal);
    }

    /// <summary>AC-008: the Admin sees «Google не повертає жодного курсу з E1 …» and no staleness.</summary>
    [Fact]
    public async Task AnEmptyCourseList_IsWordedWithItsDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var success = host.Time.GetUtcNow().AddHours(-1);
        await host.SyncStateAsync(ct, startedAt: success.AddMinutes(-5), lastSuccessfulRunAt: success, emptySince: D1);

        var technical = Section(await PageAsync(client, ct), TechnicalHook);

        Assert.Contains(Uk.With(Uk.CourseListEmptySince, D1), technical, StringComparison.Ordinal);
        Assert.DoesNotContain(Uk.NoRecentSuccess, technical, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-009, FR-006.1: three courses "not visible", one archived — the Admin sees X5 once, the three names and a
    /// checkbox each, valued with the internal course id, and the confirm button.
    /// </summary>
    [Fact]
    public async Task TheAdmin_SeesTheNotVisibleCourses_WithTheConfirmationForm()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);
        var ids = new List<long>();
        for (var i = 1; i <= 3; i++)
        {
            var id = await host.CourseAsync(
                i, $"Test Hidden {i}", ct, state: i == 3 ? CourseTestData.States.Archived : CourseTestData.States.Active);
            await host.MarkCourseAsync(id, "not_visible_to_technical_account", D1, ct);
            ids.Add(id);
        }

        var body = await PageAsync(client, ct);

        var technical = Section(body, TechnicalHook);
        Assert.Equal(1, Occurrences(technical, Uk.CoursesNotVisible));
        for (var i = 1; i <= 3; i++)
        {
            Assert.Contains($"Test Hidden {i}", technical, StringComparison.Ordinal);
        }

        Assert.Equal(
            ids.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).Order(),
            CheckboxValues(body).Order());
        Assert.Contains(Uk.ConfirmButton!, Text(body), StringComparison.Ordinal);
        Assert.Contains(Html.AntiforgeryFieldName, body, StringComparison.Ordinal);
        Assert.DoesNotContain("Test Hidden 3", Section(body, ArchivedHook), StringComparison.Ordinal);
    }

    /// <summary>AC-009, FR-002.3, FR-006.1: the Dean sees X5's title with «Повідомте Адміністратора» — no names, no form.</summary>
    [Fact]
    public async Task TheDean_SeesTheNotVisibleProblem_WithoutNamesOrForm()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);
        for (var i = 1; i <= 3; i++)
        {
            var id = await host.CourseAsync(i, $"Test Hidden {i}", ct);
            await host.MarkCourseAsync(id, "not_visible_to_technical_account", D1, ct);
        }

        var body = await PageAsync(client, ct);

        var technical = Section(body, TechnicalHook);
        Assert.Contains(Uk.CoursesNotVisible, technical, StringComparison.Ordinal);
        Assert.Contains(Uk.TellTheAdmin, technical, StringComparison.Ordinal);
        Assert.DoesNotContain("Test Hidden", body, StringComparison.Ordinal);
        Assert.Empty(CheckboxValues(body));
        Assert.False(Has(body, FormHook));
        Assert.DoesNotContain(Uk.ConfirmButton!, Text(body), StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-013, FR-002.3, I-2: C1 25 hours ago, at night (Kyiv date the next day) — both roles see «Немає зв'язку із
    /// сервісом з C1; з C1 + 7 днів …»; the Dean with «Повідомте Адміністратора», the Admin with «Повідомте Власника».
    /// </summary>
    [Theory]
    [InlineData(Actor.Dean)]
    [InlineData(Actor.Admin)]
    public async Task NoConnectionWithTheControlPlane_IsWordedWithBothDates_AndTheRolesHint(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, actor, ct);
        await using var _host = host;
        await host.RecentSuccessAsync(ct);
        var c1 = new DateTimeOffset(2026, 9, 15, 22, 30, 0, TimeSpan.Zero);
        Assert.True(host.Time.GetUtcNow() - c1 > TimeSpan.FromHours(24));
        await host.LastCheckAsync(c1, ct);

        var technical = Section(await PageAsync(client, ct), TechnicalHook);

        Assert.Contains(Uk.With(Uk.ControlPlaneUnreachable, c1, c1.AddDays(7)), technical, StringComparison.Ordinal);
        Assert.Contains(actor == Actor.Dean ? Uk.TellTheAdmin : Uk.TellTheOwner, technical, StringComparison.Ordinal);
        Assert.DoesNotContain(actor == Actor.Dean ? Uk.TellTheOwner : Uk.TellTheAdmin, technical, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-014, FR-004.5, I-3: past the grace period the X6 text is gone and the landing page's read-only message for the
    /// reason is at the top of the page.
    /// </summary>
    [Fact]
    public async Task PastTheGracePeriod_TheReadOnlyMessageReplacesTheProblem()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, Cause.GracePeriodExpired);
        await using var _host = host;

        var body = await PageAsync(client, ct);

        var notice = Section(body, ReadOnlyHook);
        Assert.Contains(Text(host.Text(SignInTestData.TextKeys.ReadOnlyTitle, "uk")), notice, StringComparison.Ordinal);
        Assert.Contains(Text(host.Text(SignInTestData.TextKeys.ReadOnlyGracePeriodExpired, "uk")), notice, StringComparison.Ordinal);
        Assert.True(body.IndexOf($"id=\"{ReadOnlyHook}\"", StringComparison.Ordinal) < body.IndexOf($"id=\"{TechnicalHook}\"", StringComparison.Ordinal));
        Assert.DoesNotContain("Немає зв'язку", Text(body), StringComparison.Ordinal);
    }

    /// <summary>FR-004.5: outside read-only mode there is no read-only message on the page.</summary>
    [Fact]
    public async Task OutsideReadOnlyMode_ThereIsNoReadOnlyMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        Assert.False(Has(await PageAsync(client, ct), ReadOnlyHook));
    }

    // ---- English, read-only (AC-012, NFR-073) ----------------------------------------------------------------------

    /// <summary>
    /// AC-012, FR-003.4, FR-007: an English-speaking Dean in read-only mode — the page in English, names as stored, no
    /// Ukrainian problem text.
    /// </summary>
    [Fact]
    public async Task InReadOnlyMode_TheEnglishPage_IsInEnglish_WithNamesAsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await DataMarksHostExtensions.StartAsync(database, "en", ct, Cause.Suspended);
        await using var _host = host;
        await host.ReadyToSeedAsync(ct);
        var gone = await host.CourseAsync(1, "Тестовий курс Гамма", ct);
        await host.MarkCourseAsync(gone, "not_in_classroom", D1, ct);
        await host.GradedAsync(gone, 1, "Тестове завдання", 10m, "Тестенко", 12m, null, ct);
        var hidden = await host.CourseAsync(2, "Test Hidden Course", ct);
        await host.MarkCourseAsync(hidden, "not_visible_to_technical_account", D1, ct);
        var en = DataHealthTexts.En;

        var text = Text(await PageAsync(client, ct));

        Assert.Contains(en.PageTitle, text, StringComparison.Ordinal);
        Assert.Contains(en.With(en.CourseNotInClassroomSince, D1), text, StringComparison.Ordinal);
        Assert.Contains(en.HintNotInClassroom, text, StringComparison.Ordinal);
        Assert.Contains(en.AboveMaximum, text, StringComparison.Ordinal);
        Assert.Contains(en.CoursesNotVisible, text, StringComparison.Ordinal);
        Assert.Contains(en.TellTheAdmin, text, StringComparison.Ordinal);
        Assert.Contains(en.ContactTheAdmin, text, StringComparison.Ordinal);
        Assert.Contains("Тестовий курс Гамма", text, StringComparison.Ordinal);
        Assert.Contains("Тестове завдання", text, StringComparison.Ordinal);
        Assert.Contains("Тестенко", text, StringComparison.Ordinal);
        foreach (var ukrainian in new[] { Uk.PageTitle, Uk.AboveMaximum, Uk.CoursesNotVisible, Uk.TellTheAdmin, Uk.HintNotInClassroom })
        {
            Assert.DoesNotContain(ukrainian, text, StringComparison.Ordinal);
        }
    }
}
