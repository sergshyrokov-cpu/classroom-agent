using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Keys = ClassroomAgent.Tests.TestInfrastructure.LastSynchronizationTestData.Keys;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-017 AC-006, AC-007, AC-008, AC-013, spec FR-006 to FR-009, I-6, I-7, OD-006, OD-010: the "Last synchronization"
/// block on the Admin's connection page — its statuses, its times in the request culture, its diagnosis worded as
/// "Check access" words it, its "never run" state and its legacy-value fallback, in both languages (NFR-073). The page
/// is read over HTTP as a real Admin on a real host over PostgreSQL (TC-2); <c>sync_state</c> is seeded directly.
/// </summary>
public sealed class LastSynchronizationBlockTests(PostgreSqlFixture database)
{
    private static string Resolve(InstallationTestHost host, string key, string language) => host.Text(key, language);

    private static async Task<PageResponse> OpenAsync(FormClient admin, string language, CancellationToken ct)
    {
        var page = await admin.OpenSettingsAsync(ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        return page;
    }

    /// <summary>AC-006: before any run the block says so — its title, "never run" and "no successful run yet".</summary>
    [Theory]
    [MemberData(nameof(LastSynchronizationTestData.Languages), MemberType = typeof(LastSynchronizationTestData))]
    public async Task WithNoSyncStateRow_TheBlockSaysNoRunHasHappened(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;
        Assert.Empty(await host.SyncStatesAsync(ct));

        var page = await OpenAsync(admin, language, ct);

        Assert.Contains(Resolve(host, Keys.Title, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(Resolve(host, Keys.StatusNeverRun, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(Resolve(host, Keys.LastSuccessNone, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Resolve(host, Keys.StatusFailed, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-006, FR-007, OD-006, I-7: a failed run shows the "failed" status and the diagnosis text — the existing
    /// "Check access" text for the six configuration codes, the synchronization wording for the other two — and no other
    /// diagnosis's text.
    /// </summary>
    [Theory]
    [MemberData(nameof(LastSynchronizationTestData.CodesByLanguage), MemberType = typeof(LastSynchronizationTestData))]
    public async Task AFailedRun_ShowsItsDiagnosisInTheRequestLanguage(string code, string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;
        await host.SeedFailedAsync(code, ct);

        var page = await OpenAsync(admin, language, ct);

        Assert.Contains(Resolve(host, Keys.Title, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(Resolve(host, Keys.StatusFailed, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(
            Resolve(host, LastSynchronizationTestData.DiagnosisKey(code), language),
            page.Text,
            StringComparison.Ordinal);

        // Exactly the one diagnosis: another code's own wording is not on the page.
        var others = LastSynchronizationTestData.ConfigurationCodes
            .Concat(LastSynchronizationTestData.SynchronizationCodes)
            .Where(other => other != code)
            .Select(other => Resolve(host, LastSynchronizationTestData.DiagnosisKey(other), language))
            .Where(text => !Resolve(host, LastSynchronizationTestData.DiagnosisKey(code), language).Contains(text, StringComparison.Ordinal));
        Assert.All(others, text => Assert.DoesNotContain(text, page.Text, StringComparison.Ordinal));
        Assert.DoesNotContain(Resolve(host, Keys.StatusNeverRun, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-006, FR-007: a failed run with an earlier success shows that success's time in the request culture, and the
    /// failed run's own finish.
    /// </summary>
    [Theory]
    [MemberData(nameof(LastSynchronizationTestData.Languages), MemberType = typeof(LastSynchronizationTestData))]
    public async Task AFailedRun_ShowsItsFinishAndTheLastSuccess_InTheRequestCulture(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;
        await host.SeedFailedAsync("GoogleUnavailable", ct, LastSynchronizationTestData.EarlierSuccess);

        var page = await OpenAsync(admin, language, ct);

        Assert.Contains(Resolve(host, Keys.LastSuccess, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(LastSynchronizationTestData.Format(LastSynchronizationTestData.EarlierSuccess, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(LastSynchronizationTestData.Format(LastSynchronizationTestData.Finished, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Resolve(host, Keys.LastSuccessNone, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-013, FR-006, I-6: a value an earlier version left (<c>RunFailed:…</c>) is shown as "unexpected error", and
    /// neither the prefix nor the exception type name reaches the page.
    /// </summary>
    [Theory]
    [MemberData(nameof(LastSynchronizationTestData.Languages), MemberType = typeof(LastSynchronizationTestData))]
    public async Task ALegacyRunFailedValue_IsShownAsUnexpected_AndLeaksNothing(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;
        await host.SeedFailedAsync("RunFailed:HttpRequestException", ct);

        var page = await OpenAsync(admin, language, ct);

        Assert.Contains(Resolve(host, Keys.DiagnosisUnexpected, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("RunFailed", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpRequestException", page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-006, FR-007: a completed run shows the "completed" status and its finish time formatted in the request
    /// culture exactly as the landing page formats the last legitimacy check (short date, <c>HH:mm</c>, <c>UTC</c>).
    /// </summary>
    [Theory]
    [MemberData(nameof(LastSynchronizationTestData.Languages), MemberType = typeof(LastSynchronizationTestData))]
    public async Task ACompletedRun_ShowsItsFinishInTheRequestCulture(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;
        await host.SeedCompletedAsync(ct);

        var page = await OpenAsync(admin, language, ct);

        Assert.Contains(Resolve(host, Keys.StatusCompleted, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(Resolve(host, Keys.FinishedAt, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(LastSynchronizationTestData.Format(LastSynchronizationTestData.Finished, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(Resolve(host, Keys.LastSuccess, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Resolve(host, Keys.LastSuccessNone, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Resolve(host, Keys.StatusFailed, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>The two cultures show the same instant differently, so the theory above cannot pass on one format.</summary>
    [Fact]
    public void TheTwoCultures_FormatTheTestInstantDifferently() =>
        Assert.NotEqual(
            LastSynchronizationTestData.Format(LastSynchronizationTestData.Finished, "uk"),
            LastSynchronizationTestData.Format(LastSynchronizationTestData.Finished, "en"));

    /// <summary>AC-006, FR-007: a running run shows "running" and when it started, not a finish.</summary>
    [Theory]
    [MemberData(nameof(LastSynchronizationTestData.Languages), MemberType = typeof(LastSynchronizationTestData))]
    public async Task ARunningRun_ShowsItsStart(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;
        await host.SeedRunningAsync(ct);

        var page = await OpenAsync(admin, language, ct);

        Assert.Contains(Resolve(host, Keys.StatusRunning, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(Resolve(host, Keys.StartedAt, language), page.Text, StringComparison.Ordinal);
        Assert.Contains(LastSynchronizationTestData.Format(LastSynchronizationTestData.Started, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Resolve(host, Keys.StatusCompleted, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Resolve(host, Keys.StatusFailed, language), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-008, FR-009: in every read-only mode the page and its block are still viewable, and looking at them writes
    /// nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheBlockIsStillShown(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, "uk", ct, cause);
        await using var _host = host;
        using var _admin = admin;
        await host.SeedFailedAsync("ScopeNotAuthorized", ct);
        var before = await host.SyncStatesAsync(ct);

        var page = await OpenAsync(admin, "uk", ct);

        Assert.Contains(Resolve(host, Keys.Title, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(Resolve(host, Keys.StatusFailed, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.SyncStatesAsync(ct));
    }

    /// <summary>
    /// AC-007, FR-008, TC-5: a signed-in Dean is refused the page and the refusal carries nothing of the block — with
    /// the control that the Admin's response for the very same seeded row does. (The 403 itself is US-009's policy and
    /// passes before this Story; here it is the regression guard that the block adds no second way in.)
    /// </summary>
    [Fact]
    public async Task ADean_IsRefusedAndSeesNothingOfTheBlock()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, "uk", ct);
        await using var _host = host;
        using var _admin = admin;
        await host.SeedFailedAsync("ScopeNotAuthorized", ct);
        var (dean, _) = await host.SignInDeanAsync(ct);
        using var _dean = dean;

        var control = await OpenAsync(admin, "uk", ct);
        var refused = await dean.GetAsync(WorkspaceConnectionTestData.Path, ct);

        Assert.Contains(Resolve(host, Keys.Title, "uk"), control.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        foreach (var language in new[] { "uk", "en" })
        {
            Assert.DoesNotContain(Resolve(host, Keys.Title, language), refused.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(
                Resolve(host, LastSynchronizationTestData.DiagnosisKey("ScopeNotAuthorized"), language),
                refused.Text,
                StringComparison.Ordinal);
        }
    }

    /// <summary>NFR-073, OD-010: every key of the block is in both files with a value.</summary>
    [Theory]
    [MemberData(nameof(LastSynchronizationTestData.Languages), MemberType = typeof(LastSynchronizationTestData))]
    public async Task EveryKeyOfTheBlock_IsTranslated(string culture)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var texts = host.AllTexts(culture);

        Assert.All(Keys.All, key =>
        {
            Assert.True(texts.ContainsKey(key), $"'{key}' is missing from the {culture} file.");
            Assert.False(string.IsNullOrWhiteSpace(texts[key]), $"'{key}' is empty in the {culture} file.");
        });
    }

    /// <summary>NFR-073: the two languages really differ for the title, and the synchronization wording of
    /// <c>GoogleUnavailable</c> is its own (spec I-7), not the "Check access" one.</summary>
    [Fact]
    public async Task TheTitleDiffersBetweenLanguages_AndGoogleUnavailableHasItsOwnWording()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.NotEqual(Resolve(host, Keys.Title, "uk"), Resolve(host, Keys.Title, "en"));
        foreach (var language in new[] { "uk", "en" })
        {
            Assert.NotEqual(
                Resolve(host, "AccessCheck.Outcome.GoogleUnavailable", language),
                Resolve(host, Keys.DiagnosisGoogleUnavailable, language));
        }
    }
}
