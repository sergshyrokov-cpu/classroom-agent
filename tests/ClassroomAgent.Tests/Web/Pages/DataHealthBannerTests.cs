using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using static ClassroomAgent.Tests.TestInfrastructure.DataHealthHostExtensions;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cause = ClassroomAgent.Tests.TestInfrastructure.ReadOnlyModeHost.Cause;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-062 FR-003 over HTTP and PostgreSQL (TC-2): the banner in the installation layout — on every page of a signed-in
/// Admin or Dean while a counted problem exists, its text in the language's plural forms, one link to «Стан даних»,
/// restrained, absent without a session, on the forced password change (OD-001 a) and when its computation fails
/// (I-4). The page markup it reads is fixed by the test strategy §3.2. Data is synthetic (TC-4).
/// </summary>
public sealed class DataHealthBannerTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset D1 = new(2026, 9, 10, 21, 30, 0, TimeSpan.Zero);

    /// <summary>Pages of the layout the banner is checked on: the landing page, a teaching page and «Стан даних».</summary>
    public static TheoryData<string> LayoutPages => new("/", "/workspace/courses", PagePath);

    /// <summary>The AC-001 data: course A with two above-maximum grades and an item gone, course B gone, the run failed.</summary>
    private static async Task SeedAc001Async(InstallationTestHost host, CancellationToken ct)
    {
        await host.ReadyToSeedAsync(ct);
        await host.SyncStateAsync(
            ct, status: "failed", lastError: "GoogleUnavailable", lastSuccessfulRunAt: host.Time.GetUtcNow().AddHours(-3));
        var a = await host.CourseAsync(1, "Test Course A", ct);
        await host.GradedAsync(a, 1, "Test Item One", 10m, "Overone", 11m, null, ct);
        await host.GradedAsync(a, 2, "Test Item Two", 5m, "Overtwo", 6m, null, ct);
        await host.ItemNotInClassroomAsync(a, 3, "Test Item Gone", D1, ct);
        var b = await host.CourseAsync(2, "Test Course B", ct);
        await host.MarkCourseAsync(b, "not_in_classroom", D1, ct);
    }

    /// <summary>
    /// AC-001, FR-003.1, FR-003.2: the Dean sees «1 технічна проблема, 3 навчальні» on every layout page, as one link to
    /// «Стан даних».
    /// </summary>
    [Theory]
    [MemberData(nameof(LayoutPages))]
    public async Task TheBanner_CountsTheProblems_OnEveryPage(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAc001Async(host, ct);

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal("1 технічна проблема, 3 навчальні", Banner(page.Body));
    }

    /// <summary>FR-002.1, FR-003.1: the Admin sees the same banner.</summary>
    [Fact]
    public async Task TheAdmin_SeesTheBanner()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await SeedAc001Async(host, ct);

        Assert.Equal(DataHealthTexts.Uk.Banner(1, 3), Banner((await client.GetAsync("/", ct)).Body));
    }

    /// <summary>AC-002: with no problem of any kind there is no banner element on any page.</summary>
    [Theory]
    [MemberData(nameof(LayoutPages))]
    public async Task WithNoProblem_ThereIsNoBanner(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.ReadyToSeedAsync(ct);
        await host.RecentSuccessAsync(ct);
        await host.CourseAsync(1, "Test Healthy Course", ct);

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.False(Has(page.Body, BannerHook));
    }

    /// <summary>AC-003, FR-001.5: an archived course's problem alone shows no banner.</summary>
    [Fact]
    public async Task AnArchivedCoursesProblemAlone_ShowsNoBanner()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.ReadyToSeedAsync(ct);
        await host.RecentSuccessAsync(ct);
        var archived = await host.CourseAsync(1, "Test Archived Course", ct, state: CourseTestData.States.Archived);
        await host.GradedAsync(archived, 1, "Test Archived Item", 10m, "Archivedstudent", 14m, null, ct);

        Assert.False(Has((await client.GetAsync("/", ct)).Body, BannerHook));
    }

    /// <summary>
    /// FR-003.2, OD-005 a: the plural forms — Ukrainian one / few / many (1, 2–4, 5–20, 11, 21, 22), English one / other —
    /// with both numbers always shown, zero included. Technical problems are courses skipped for an unknown state (X3),
    /// teaching ones courses "not in Classroom" (T4), one per course.
    /// </summary>
    [Theory]
    [InlineData("uk", 0, 1)]
    [InlineData("uk", 2, 5)]
    [InlineData("uk", 5, 2)]
    [InlineData("uk", 11, 21)]
    [InlineData("uk", 21, 22)]
    [InlineData("uk", 3, 0)]
    [InlineData("en", 1, 1)]
    [InlineData("en", 2, 0)]
    [InlineData("en", 0, 3)]
    public async Task TheBannerText_UsesTheLanguagesPluralForms(string language, int technical, int teaching)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await DataMarksHostExtensions.StartAsync(database, language, ct);
        await using var _host = host;
        await host.ReadyToSeedAsync(ct);
        await host.RecentSuccessAsync(ct);
        for (var i = 1; i <= technical; i++)
        {
            await host.SkipAsync(CourseTestData.CourseId(100 + i), $"Test Unknown {i}", UnknownStateReason, D1, ct);
        }

        for (var i = 1; i <= teaching; i++)
        {
            var id = await host.CourseAsync(i, $"Test Gone {i}", ct);
            await host.MarkCourseAsync(id, "not_in_classroom", D1, ct);
        }

        var page = await client.GetAsync("/", ct);

        Assert.Equal(DataHealthTexts.Of(language).Banner(technical, teaching), Banner(page.Body));
    }

    /// <summary>The plural rule the tests pin, checked on its own against the Specification's example.</summary>
    [Fact]
    public void ThePinnedPluralRule_MatchesTheSpecificationsExamples()
    {
        Assert.Equal("1 технічна проблема, 3 навчальні", DataHealthTexts.Uk.Banner(1, 3));
        Assert.Equal("1 technical problem, 3 teaching", DataHealthTexts.En.Banner(1, 3));
        Assert.Equal("0 технічних проблем, 3 навчальні", DataHealthTexts.Uk.Banner(0, 3));
    }

    /// <summary>FR-003.3: a coloured strip — no button, sound, script, animation or pop-up inside the banner.</summary>
    [Fact]
    public async Task TheBanner_IsRestrained()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAc001Async(host, ct);

        var body = (await client.GetAsync("/", ct)).Body;

        var start = body.IndexOf($"id=\"{BannerHook}\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = body.IndexOf("</a>", start, StringComparison.OrdinalIgnoreCase);
        var banner = body[body.LastIndexOf('<', start)..end];
        foreach (var forbidden in new[] { "<button", "<audio", "<video", "<script", "<marquee", "<blink", "<dialog", "onclick" })
        {
            Assert.DoesNotContain(forbidden, banner, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>AC-012, FR-003.4: in read-only mode, in English, the banner is shown.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheEnglishBanner_IsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await DataMarksHostExtensions.StartAsync(database, "en", ct, Cause.Suspended);
        await using var _host = host;
        await host.ReadyToSeedAsync(ct);
        var gone = await host.CourseAsync(1, "Test Gone Course", ct);
        await host.MarkCourseAsync(gone, "not_in_classroom", D1, ct);
        var hidden = await host.CourseAsync(2, "Test Hidden Course", ct);
        await host.MarkCourseAsync(hidden, "not_visible_to_technical_account", D1, ct);

        Assert.Equal("1 technical problem, 1 teaching", Banner((await client.GetAsync("/", ct)).Body));
    }

    /// <summary>AC-013: the last successful legitimacy check more than 24 hours ago is one technical problem in the banner.</summary>
    [Fact]
    public async Task NoConnectionWithTheControlPlane_IsCountedInTheBanner()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.ReadyToSeedAsync(ct);
        await host.RecentSuccessAsync(ct);
        await host.LastCheckAsync(host.Time.GetUtcNow().AddHours(-25), ct);

        Assert.Equal("1 технічна проблема, 0 навчальних", Banner((await client.GetAsync("/", ct)).Body));
    }

    /// <summary>AC-015, FR-003.1, OD-001 a: a Dean with a temporary password sees no banner on the forced change page.</summary>
    [Fact]
    public async Task TheForcedPasswordChangePage_HasNoBanner()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        await SeedAc001Async(host, ct);

        var page = await client.GetAsync(ChangePasswordPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.False(Has(page.Body, BannerHook));
    }

    /// <summary>FR-003.1: a page without a signed-in user carries no banner, problems or not.</summary>
    [Fact]
    public async Task ThePageWithoutASession_HasNoBanner()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        await SeedAc001Async(host, ct);

        var page = await client.GetAsync("/sign-in", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.False(Has(page.Body, BannerHook));
    }

    /// <summary>FR-003.1: no <c>/api/v1</c> response carries the banner.</summary>
    [Fact]
    public async Task AnApiResponse_HasNoBanner()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAc001Async(host, ct);

        var response = await client.GetAsync("/api/v1/no-such-resource", ct);

        Assert.False(Has(response.Body, BannerHook));
        Assert.DoesNotContain("навчальн", response.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Spec §8, I-4, api-design §2.3: when computing the banner fails, the page still renders without the banner, and the
    /// failure is logged as a warning naming the exception type and no course data.
    /// </summary>
    [Fact]
    public async Task AFailingComputation_LeavesThePageWithoutTheBanner_AndIsLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(
            database,
            Actor.Dean,
            ct,
            beforeStart: h => h.ConfigureServices = s => s.AddScoped<IDataHealthSource, FailingDataHealthSource>());
        await using var _host = host;
        await SeedAc001Async(host, ct);

        var page = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.False(Has(page.Body, BannerHook));
        var events = await host.ReadLogEventsAsync(ct);
        var warning = Assert.Single(
            events,
            e => e.Level == "Warning" && e.Line.Contains(nameof(DataHealthProbeException), StringComparison.Ordinal));
        Assert.DoesNotContain("Test Course", warning.Line, StringComparison.Ordinal);
        Assert.DoesNotContain(FailingDataHealthSource.Secret, warning.Line, StringComparison.Ordinal);
    }

    /// <summary>The exception type the failing source throws, so the log line can be found by its name.</summary>
    private sealed class DataHealthProbeException(string message) : Exception(message);

    /// <summary>A source whose every read fails; its message is a marker that must not reach the log line's text.</summary>
    private sealed class FailingDataHealthSource : IDataHealthSource
    {
        public const string Secret = "Test Course A secret detail";

        public Task<IReadOnlyList<CourseHealthFact>> GetCourseFactsAsync(CancellationToken cancellationToken) =>
            throw new DataHealthProbeException(Secret);

        public Task<IReadOnlyList<AboveMaximumGradeRow>> GetAboveMaximumGradesAsync(
            IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken) =>
            throw new DataHealthProbeException(Secret);

        public Task<IReadOnlyList<ItemNotInClassroomRow>> GetItemsNotInClassroomAsync(
            IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken) =>
            throw new DataHealthProbeException(Secret);
    }
}
