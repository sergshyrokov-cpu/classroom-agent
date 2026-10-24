using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using Statements = ClassroomAgent.Tests.TestInfrastructure.ConnectionInstructionTestData.TextKeys;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-054 AC-001 to AC-008: the program's texts describe the school's technical account as <c>trebovaniya.md</c>
/// §9 v97 does — a super-admin whose sign-in is closed by five mandatory measures — on the instruction (US-010),
/// the connection page (US-009), the access check (US-011) and the "Last synchronization" block (US-017), in
/// Ukrainian and English (NFR-073). Read over HTTP as a real Admin on a real host over PostgreSQL (TC-2); no Google
/// API is called — the access-check port is the fake (TC-4).
/// </summary>
public sealed class TechnicalAccountSuperAdminTextsTests(PostgreSqlFixture database)
{
    public static TheoryData<string> Languages => new("uk", "en");

    /// <summary>
    /// AC-001, AC-002, AC-006, AC-007, AC-008 (spec §4.1, VR-001): every new or reworded entry exists in both
    /// language files with exactly the binding wording.
    /// </summary>
    [Theory]
    [MemberData(nameof(TechnicalAccountTextsTestData.BindingByLanguage), MemberType = typeof(TechnicalAccountTextsTestData))]
    public async Task EveryRewrittenEntry_HasTheBindingWording(string key, string language)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.Equal(TechnicalAccountTextsTestData.Wording(key, language), host.Text(key, language));
    }

    /// <summary>AC-003, spec FR-002: the two statements whose names no longer match v97 exist in neither file.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheRemovedStatements_ExistInNeitherLanguageFile(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var texts = host.AllTexts(language);

        Assert.All(TechnicalAccountTextsTestData.RemovedKeys, key => Assert.False(texts.ContainsKey(key), $"'{key}' is still in the '{language}' file."));
    }

    /// <summary>
    /// AC-002, AC-004, AC-005 (spec FR-001, FR-005): the text the Admin hands over makes every technical-account
    /// statement in the FR-001 order — statements 1–4, the five measures, statement 5 — and each is rendered once, so
    /// the copied text and the page cannot disagree.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheHandedOverText_MakesEveryStatementInOrder(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;

        var page = await admin.OpenInstructionAsync(ct);
        var text = ConnectionInstructionHostExtensions.HandedOverText(page);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var previous = -1;
        foreach (var key in Statements.TechnicalAccountStatements)
        {
            var statement = host.Text(key, language);
            var at = text.IndexOf(statement, StringComparison.Ordinal);
            Assert.True(at > previous, $"'{key}' is missing from the handed-over text or out of the FR-001 order.");
            Assert.Equal(1, Occurrences(page.Text, statement));
            previous = at;
        }
    }

    /// <summary>
    /// AC-002, spec FR-001, I-3: the five measures are a list nested under the lead statement — one level deeper than
    /// statements 1–5 — in the handed-over text itself, so the nesting survives plain-text copying.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheMeasures_AreNestedUnderTheLeadStatement(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;

        var page = await admin.OpenInstructionAsync(ct);
        var lines = ConnectionInstructionHostExtensions.HandedOverText(page).Split('\n');

        var statementIndent = Indent(lines, host.Text(Statements.TechnicalAccountMeasuresLead, language));
        Assert.Equal(statementIndent, Indent(lines, host.Text(Statements.TechnicalAccountSuperAdminRole, language)));
        Assert.Equal(statementIndent, Indent(lines, host.Text(Statements.TechnicalAccountEnteredInSettings, language)));
        Assert.All(
            Statements.Measures,
            key => Assert.True(
                Indent(lines, host.Text(key, language)) > statementIndent,
                $"'{key}' is not nested under the lead statement."));
    }

    /// <summary>AC-003, spec I-5: no pre-v97 statement or hint reaches the instruction, in either language.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheInstruction_CarriesNoRetiredStatement(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;

        var page = await admin.OpenInstructionAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.All(
            TechnicalAccountTextsTestData.Retired,
            retired => Assert.DoesNotContain(retired, page.Text, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC-003, AC-006 (spec FR-003): the connection page shows the v97 hint under the technical-account field, and no
    /// pre-v97 statement or hint.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheConnectionPage_ShowsTheV97Hint(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;

        var page = await admin.OpenSettingsAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(
            TechnicalAccountTextsTestData.Wording(TechnicalAccountTextsTestData.HintKey, language),
            page.Text,
            StringComparison.Ordinal);
        Assert.All(
            TechnicalAccountTextsTestData.Retired,
            retired => Assert.DoesNotContain(retired, page.Text, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC-007, spec FR-004: an access check whose read is refused for lack of right tells the school's super-admin to
    /// give the account the super-admin role — not read-only rights — on that read's step.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheAccessCheck_TellsTheSchoolToGrantTheSuperAdminRole(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertAppUserAsync(ct, uiLanguage: language);
        host.ControlPlaneHandler = WorkspaceConnectionHostExtensions.ApprovingChannel();
        host.ConfigureServices = AccessCheckHostExtensions.RegisterFakeProbe;
        host.Start();
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        await AccessCheckHostExtensions.SeedConnectionAsync(host, SeededConnection.Usable, ct);
        AccessCheckHostExtensions.ProbeOf(host).MeetActivity = AccessCheckStepOutcome.TechnicalAccountCannotRead;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var step = AccessCheckHostExtensions.StepsOf(page)[7];
        Assert.Equal(nameof(AccessCheckStepOutcome.TechnicalAccountCannotRead), step.Outcome);
        Assert.Contains(
            TechnicalAccountTextsTestData.Wording(TechnicalAccountTextsTestData.CannotReadKey, language),
            step.Text,
            StringComparison.Ordinal);
        Assert.All(
            TechnicalAccountTextsTestData.RetiredCannotReadAdvice,
            retired => Assert.DoesNotContain(retired, page.Text, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC-007, spec FR-004: the "Last synchronization" block shows the same advice for a run that failed with the
    /// same diagnosis — one problem, one name (§4 Epic 6, v80).
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheLastSynchronizationBlock_TellsTheSchoolToGrantTheSuperAdminRole(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await LastSynchronizationHostExtensions.StartAdminAsync(database, language, ct);
        await using var _host = host;
        using var _admin = admin;
        await host.SeedFailedAsync(nameof(AccessCheckStepOutcome.TechnicalAccountCannotRead), ct);

        var page = await admin.OpenSettingsAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(
            TechnicalAccountTextsTestData.Wording(TechnicalAccountTextsTestData.CannotReadKey, language),
            page.Text,
            StringComparison.Ordinal);
        Assert.All(
            TechnicalAccountTextsTestData.RetiredCannotReadAdvice,
            retired => Assert.DoesNotContain(retired, page.Text, StringComparison.Ordinal));
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var at = text.IndexOf(value, StringComparison.Ordinal); at >= 0; at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>The leading whitespace of the one line that carries <paramref name="statement"/>.</summary>
    private static int Indent(string[] lines, string statement)
    {
        var line = Assert.Single(lines, l => l.Contains(statement, StringComparison.Ordinal));
        return line.Length - line.TrimStart().Length;
    }
}
