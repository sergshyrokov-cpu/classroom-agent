using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-009 AC-002, AC-009, AC-010: what the connection settings page shows — the school's domain, the
/// connection state, the saved technical account, the two explanatory sentences, and the read-only reason
/// when it applies (spec FR-002, FR-003, FR-004). Opening the page writes nothing, including when the
/// saved domain no longer matches (OD-002, spec I-5).
/// </summary>
public sealed class WorkspaceConnectionPageTests(PostgreSqlFixture database)
{
    /// <summary>AC-002: an installation nobody has configured says so, in the user's language.</summary>
    [Fact]
    public async Task WithNoConnectionSaved_ThePageSaysItIsNotConfigured()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenSettingsAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(WorkspaceConnectionTestData.TextKeys.StateNotConfigured, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text(WorkspaceConnectionTestData.TextKeys.StateConfigured, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-002: the form is there, empty, with its antiforgery token.</summary>
    [Fact]
    public async Task WithNoConnectionSaved_TheFormIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenSettingsAsync(ct);

        Assert.True(Html.HasInput(page.Body, WorkspaceConnectionTestData.EmailField));
        Assert.Equal(string.Empty, Html.InputValue(page.Body, WorkspaceConnectionTestData.EmailField));
        Assert.True(Html.HasInput(page.Body, Html.AntiforgeryFieldName));
    }

    /// <summary>AC-002, OD-001: the school's domain is shown, and there is no field to type it into.</summary>
    [Fact]
    public async Task ThePage_ShowsTheInstallationDomainAndDoesNotAskForIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenSettingsAsync(ct);

        Assert.Contains(WorkspaceConnectionTestData.AllowedDomain, page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(WorkspaceConnectionTestData.TextKeys.DomainLabel, "uk"), page.Text, StringComparison.Ordinal);
        Assert.False(Html.HasInput(page.Body, "domain"));
    }

    /// <summary>AC-002: the page explains what the technical account is, and that saving does not check it.</summary>
    [Fact]
    public async Task ThePage_ExplainsTheTechnicalAccountAndThatSavingDoesNotCheckIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenSettingsAsync(ct);

        Assert.Contains(host.Text(WorkspaceConnectionTestData.TextKeys.TechnicalAccountHint, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(WorkspaceConnectionTestData.TextKeys.CheckAccessHint, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-003: a stored connection is shown when the page is reopened.</summary>
    [Fact]
    public async Task WithAConnectionSaved_ThePageShowsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await host.InsertWorkspaceConnectionAsync(ct, impersonationUserEmail: WorkspaceConnectionTestData.TechnicalAccount);

        var page = await client.OpenSettingsAsync(ct);

        Assert.Contains(host.Text(WorkspaceConnectionTestData.TextKeys.StateConfigured, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(WorkspaceConnectionTestData.TechnicalAccount, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-002: opening the page writes nothing at all.</summary>
    [Fact]
    public async Task OpeningThePage_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        var connections = await host.WorkspaceConnectionsAsync(ct);
        var audit = await host.AuditRowsAsync(ct);

        await client.OpenSettingsAsync(ct);
        await client.OpenSettingsAsync(ct);

        Assert.Equal(connections, await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal(audit, await host.AuditRowsAsync(ct));
    }

    /// <summary>
    /// OD-002, spec I-5: a saved connection whose domain no longer matches is reported as invalid, nothing is
    /// deleted or corrected, and merely looking at it writes no audit row.
    /// </summary>
    [Fact]
    public async Task WhenTheSavedDomainNoLongerMatches_ThePageSaysSoAndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await host.InsertWorkspaceConnectionAsync(
            ct,
            domain: "school-two.example.test",
            impersonationUserEmail: "classroom-agent@school-two.example.test");
        var before = await host.WorkspaceConnectionsAsync(ct);

        var page = await client.OpenSettingsAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(WorkspaceConnectionTestData.TextKeys.StateDomainMismatch, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.WorkspaceConnectionsAsync(ct));
        Assert.Empty(await host.ConnectionAuditRowsAsync(ct));
    }

    /// <summary>AC-010: with no successful legitimacy check the page says the allowed domain is unknown.</summary>
    [Fact]
    public async Task WithNoSuccessfulLegitimacyCheck_ThePageSaysTheDomainIsUnknown()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.NeverConfirmed);
        await using var _host = host;

        var page = await client.OpenSettingsAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(WorkspaceConnectionTestData.TextKeys.DomainUnknown, "uk"), page.Text, StringComparison.Ordinal);
    }

    public static TheoryData<ReadOnlyModeHost.Cause, string> ReadOnlyReasons => new()
    {
        { ReadOnlyModeHost.Cause.NeverConfirmed, SignInTestData.TextKeys.ReadOnlyNotYetConfirmed },
        { ReadOnlyModeHost.Cause.Suspended, SignInTestData.TextKeys.ReadOnlySuspended },
        { ReadOnlyModeHost.Cause.GracePeriodExpired, SignInTestData.TextKeys.ReadOnlyGracePeriodExpired },
    };

    /// <summary>AC-009: in read-only mode viewing keeps working and the reason is named (BR-026).</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyReasons))]
    public async Task InReadOnlyMode_ThePageIsServedWithItsReason(ReadOnlyModeHost.Cause cause, string reasonKey)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        var page = await client.OpenSettingsAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(reasonKey, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-009, AD-6: read-only mode is not expressed by hiding the form. The refusal is enforced in
    /// <c>Application</c>, so the form stays as it is and the save is refused when it is sent.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheFormIsNeitherHiddenNorDisabled(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        var page = await client.OpenSettingsAsync(ct);

        Assert.True(Html.HasInput(page.Body, WorkspaceConnectionTestData.EmailField));
        Assert.DoesNotContain("disabled", Html.InputValue(page.Body, WorkspaceConnectionTestData.EmailField) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-001, FR-013: the landing page carries the entry that leads here, and it is the Admin's.</summary>
    [Fact]
    public async Task TheLandingPage_LinksToTheConnectionSettings()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Contains(host.Text(WorkspaceConnectionTestData.TextKeys.NavigationEntry, "uk"), landing.Text, StringComparison.Ordinal);
        Assert.Contains(WorkspaceConnectionTestData.Path, landing.Body, StringComparison.Ordinal);
    }
}
