using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using MeetKeys = ClassroomAgent.Tests.TestInfrastructure.MeetTestData.Keys;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-031 AC-009, AC-007, AC-014, spec FR-011, OD-010 a, I-5; TC-8: what the Meet part of the "Last synchronization"
/// block shows on the Admin's connection page — the watermark line in the school's time zone, the "not loaded yet"
/// state, and the step that stopped a failed run — in both languages. The page is read over HTTP as a real Admin on a
/// real host over PostgreSQL (TC-2); <c>sync_state</c> is seeded directly, including the two columns this Story adds.
/// </summary>
public sealed class LastSynchronizationMeetBlockTests(PostgreSqlFixture database)
{
    /// <summary>21:30 UTC on 23 September — 00:30 on 24 September in the school's time zone (Europe/Kyiv, UTC+3).</summary>
    private static readonly DateTimeOffset LoadedUpTo = new(2026, 9, 23, 21, 30, 0, TimeSpan.Zero);

    private static readonly DateTime LoadedUpToLocal = new(2026, 9, 24, 0, 30, 0);

    private static async Task SeedAsync(
        InstallationTestHost host,
        string status,
        DateTimeOffset? finishedAt,
        string? lastError,
        DateTimeOffset? lastSuccessfulRunAt,
        DateTimeOffset? meetLoadedUpTo,
        string? failedStep,
        CancellationToken cancellationToken) =>
        await host.ExecuteAsync(
            """
            INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count,
                                    last_error, last_successful_run_at, meet_loaded_up_to, failed_step,
                                    created_at, updated_at)
            VALUES (true, @status, @runId, @startedAt, @finishedAt, 0, @lastError, @lastSuccessfulRunAt,
                    @meetLoadedUpTo, @failedStep, @stamp, @stamp)
            """,
            cancellationToken,
            ("status", status),
            ("runId", Guid.NewGuid()),
            ("startedAt", LastSynchronizationTestData.Started),
            ("finishedAt", finishedAt),
            ("lastError", lastError),
            ("lastSuccessfulRunAt", lastSuccessfulRunAt),
            ("meetLoadedUpTo", meetLoadedUpTo),
            ("failedStep", failedStep),
            ("stamp", host.Time.GetUtcNow()));

    private static Task SeedCompletedAsync(InstallationTestHost host, DateTimeOffset? meetLoadedUpTo, CancellationToken ct) =>
        SeedAsync(
            host,
            "completed",
            LastSynchronizationTestData.Finished,
            null,
            LastSynchronizationTestData.Finished,
            meetLoadedUpTo,
            null,
            ct);

    private static Task SeedFailedAsync(InstallationTestHost host, string? failedStep, CancellationToken ct) =>
        SeedAsync(
            host,
            "failed",
            LastSynchronizationTestData.Finished,
            "ScopeNotAuthorized",
            null,
            null,
            failedStep,
            ct);

    private static async Task<PageResponse> OpenAsync(FormClient admin, string language, CancellationToken ct)
    {
        var page = await admin.OpenSettingsAsync(ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        return page;
    }

    /// <summary>A missing translation cannot pass: each text exists and is not its own key.</summary>
    private static void AssertTranslated(InstallationTestHost host, string language, params string[] keys)
    {
        foreach (var key in keys)
        {
            var text = host.Text(key, language);
            Assert.False(string.IsNullOrWhiteSpace(text), $"'{key}' is empty in {language}.");
            Assert.NotEqual(key, text);
        }
    }

    /// <summary>
    /// AC-009, FR-011, OD-010 a: a stored watermark is shown in the school's time zone in the request culture, with the
    /// line's label — and not as the UTC instant the rest of the block uses.
    /// </summary>
    [Theory]
    [MemberData(nameof(LastSynchronizationTestData.Languages), MemberType = typeof(LastSynchronizationTestData))]
    public async Task AWatermark_IsShownInTheSchoolTimeZone_NotInUtc(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;
        AssertTranslated(host, language, MeetKeys.LoadedUpTo, MeetKeys.NotLoaded);
        await SeedCompletedAsync(host, LoadedUpTo, ct);

        var page = await OpenAsync(admin, language, ct);

        Assert.Contains(host.Text(MeetKeys.LoadedUpTo, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(MeetTestData.FormatLocal(LoadedUpToLocal, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(LastSynchronizationTestData.Format(LoadedUpTo, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(MeetKeys.NotLoaded, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-009, FR-011: a completed run with no watermark says the meetings are not loaded yet.</summary>
    [Theory]
    [MemberData(nameof(LastSynchronizationTestData.Languages), MemberType = typeof(LastSynchronizationTestData))]
    public async Task ACompletedRunWithNoWatermark_SaysNotLoaded(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;
        AssertTranslated(host, language, MeetKeys.LoadedUpTo, MeetKeys.NotLoaded);
        await SeedCompletedAsync(host, null, ct);

        var page = await OpenAsync(admin, language, ct);

        Assert.Contains(host.Text(MeetKeys.NotLoaded, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(MeetKeys.LoadedUpTo, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-009, FR-011: before any run (no <c>sync_state</c> row) the block also says "not loaded yet".</summary>
    [Fact]
    public async Task WithNoSyncStateRow_TheBlockSaysNotLoaded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, "uk", ct);
        await using var _host = host;
        using var _admin = admin;
        AssertTranslated(host, "uk", MeetKeys.NotLoaded);
        Assert.Empty(await host.SyncStatesAsync(ct));

        var page = await OpenAsync(admin, "uk", ct);

        Assert.Contains(host.Text(MeetKeys.NotLoaded, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-007, FR-011: a failed run names the step that stopped it — the Meet read — next to the diagnosis, and does not
    /// name the other step.
    /// </summary>
    [Theory]
    [MemberData(nameof(LastSynchronizationTestData.Languages), MemberType = typeof(LastSynchronizationTestData))]
    public async Task AFailedMeetStep_NamesTheMeetStepAndTheDiagnosis(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;
        AssertTranslated(host, language, MeetKeys.StepMeet, MeetKeys.StepClassroom);
        await SeedFailedAsync(host, "meet", ct);

        var page = await OpenAsync(admin, language, ct);

        Assert.Contains(host.Text(MeetKeys.StepMeet, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(
            host.Text(LastSynchronizationTestData.DiagnosisKey("ScopeNotAuthorized"), language),
            page.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(MeetKeys.StepClassroom, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-007, FR-011: a run that failed in the Classroom read names that step, not the Meet one.</summary>
    [Theory]
    [MemberData(nameof(LastSynchronizationTestData.Languages), MemberType = typeof(LastSynchronizationTestData))]
    public async Task AFailedClassroomStep_NamesTheClassroomStep(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;
        AssertTranslated(host, language, MeetKeys.StepMeet, MeetKeys.StepClassroom);
        await SeedFailedAsync(host, "classroom", ct);

        var page = await OpenAsync(admin, language, ct);

        Assert.Contains(host.Text(MeetKeys.StepClassroom, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(MeetKeys.StepMeet, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-007, FR-011, I-5: a failed row written before this Story has no step — the diagnosis shows and neither step is
    /// named.
    /// </summary>
    [Fact]
    public async Task AFailedRowWithNoStep_ShowsTheDiagnosisAndNeitherStep()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, "uk", ct);
        await using var _host = host;
        using var _admin = admin;
        AssertTranslated(host, "uk", MeetKeys.StepMeet, MeetKeys.StepClassroom);
        await SeedFailedAsync(host, null, ct);

        var page = await OpenAsync(admin, "uk", ct);

        Assert.Contains(
            host.Text(LastSynchronizationTestData.DiagnosisKey("ScopeNotAuthorized"), "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(MeetKeys.StepMeet, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(MeetKeys.StepClassroom, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-011, api-design §3: while a run is going the watermark line still shows what the last completed run loaded.
    /// </summary>
    [Fact]
    public async Task ARunningRun_StillShowsTheWatermark()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, "uk", ct);
        await using var _host = host;
        using var _admin = admin;
        AssertTranslated(host, "uk", MeetKeys.LoadedUpTo);
        await SeedAsync(
            host,
            "running",
            null,
            null,
            LastSynchronizationTestData.EarlierSuccess,
            LoadedUpTo,
            null,
            ct);

        var page = await OpenAsync(admin, "uk", ct);

        Assert.Contains(host.Text(MeetKeys.LoadedUpTo, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(MeetTestData.FormatLocal(LoadedUpToLocal, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-007, TC-5: a signed-in Dean is refused the page and the refusal carries nothing of the Meet lines — with the
    /// control that the Admin's response for the very same row does.
    /// </summary>
    [Fact]
    public async Task ADean_IsRefusedAndSeesNothingOfTheMeetLines()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, "uk", ct);
        await using var _host = host;
        using var _admin = admin;
        AssertTranslated(host, "uk", MeetKeys.LoadedUpTo, MeetKeys.NotLoaded);
        await SeedCompletedAsync(host, LoadedUpTo, ct);
        var (dean, _) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var control = await OpenAsync(admin, "uk", ct);
        var refused = await dean.GetAsync(WorkspaceConnectionTestData.Path, ct);

        Assert.Contains(host.Text(MeetKeys.LoadedUpTo, "uk"), control.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        foreach (var language in new[] { "uk", "en" })
        {
            Assert.DoesNotContain(host.Text(MeetKeys.LoadedUpTo, language), refused.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(host.Text(MeetKeys.NotLoaded, language), refused.Text, StringComparison.Ordinal);
        }
    }
}
