using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-010 AC-002, AC-005, AC-006, AC-007, AC-008: what the instruction shows — this school's own
/// service-account client ID and domain from <c>LegitimacyState</c>, the read-only reason when it applies, and
/// the not-yet-confirmed statement when no check has ever succeeded (spec FR-001, FR-002, FR-003, FR-009).
/// Rendering writes nothing, in any mode (spec FR-010).
/// </summary>
public sealed class ConnectionInstructionPageTests(PostgreSqlFixture database)
{
    /// <summary>AC-002: the client ID shown is the one from <c>LegitimacyState</c>, rendered as stored.</summary>
    [Fact]
    public async Task ThePage_ShowsThisSchoolsServiceAccountClientId()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(ConnectionInstructionTestData.ClientId, page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(ConnectionInstructionTestData.TextKeys.ClientIdLabel, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-002: the page says the value belongs to this school alone (§6).</summary>
    [Fact]
    public async Task ThePage_SaysTheClientIdBelongsToThisSchoolAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Contains(
            host.Text(ConnectionInstructionTestData.TextKeys.ClientIdThisSchoolOnly, "uk"),
            page.Text,
            StringComparison.Ordinal);
    }

    /// <summary>AC-002: the school's domain is shown, so the reader sees which school this is for.</summary>
    [Fact]
    public async Task ThePage_ShowsTheSchoolsDomain()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Contains(ConnectionInstructionTestData.Domain, page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(ConnectionInstructionTestData.TextKeys.DomainLabel, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-002, spec FR-006: of the three identifiers §6 (v78) warns against confusing, only the service
    /// account's client ID belongs here. The installation's **OAuth web client id** must not appear.
    /// </summary>
    [Fact]
    public async Task ThePage_DoesNotShowTheOAuthWebClientId()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        // The status is asserted first: an absence proved against a 404 would prove nothing.
        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain(InstallationConfigurationKeys.OAuthClientIdValue, page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-002, spec FR-006: nor does the <c>Installation</c> identifier of the service channel.</summary>
    [Fact]
    public async Task ThePage_DoesNotShowTheInstallationIdentifier()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain(host.InstallationId.ToString(), page.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC-006: the Owner recreated the service account and the next successful check recorded the new client
    /// ID. The page reads the current state on every request, so the new value appears with no restart.
    /// </summary>
    [Fact]
    public async Task WhenTheClientIdIsRotated_TheNextRenderShowsTheNewOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var before = await client.OpenInstructionAsync(ct);
        await host.ExecuteAsync(
            "UPDATE legitimacy_state SET client_id = @clientId",
            ct,
            ("clientId", ConnectionInstructionTestData.RotatedClientId));
        var after = await client.OpenInstructionAsync(ct);

        Assert.Contains(ConnectionInstructionTestData.ClientId, before.Text, StringComparison.Ordinal);
        Assert.Contains(ConnectionInstructionTestData.RotatedClientId, after.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(ConnectionInstructionTestData.ClientId, after.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-008: with no successful legitimacy check the page is still served, says what is missing, and shows
    /// no invented value in place of the client ID or the domain.
    /// </summary>
    [Fact]
    public async Task WithNoSuccessfulLegitimacyCheck_ThePageSaysWhatIsMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.NeverConfirmed);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(ConnectionInstructionTestData.TextKeys.NotConfirmed, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(ConnectionInstructionTestData.ClientId, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(ConnectionInstructionTestData.Domain, page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-008: the parts that do not depend on the school are shown in full even then — the scope list and
    /// the technical-account requirements.
    /// </summary>
    [Fact]
    public async Task WithNoSuccessfulLegitimacyCheck_TheSchoolIndependentPartsAreComplete()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.NeverConfirmed);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        foreach (var scope in ConnectionInstructionTestData.Scopes)
        {
            Assert.Contains(scope, page.Text, StringComparison.Ordinal);
        }

        foreach (var key in ConnectionInstructionTestData.TextKeys.TechnicalAccountStatements)
        {
            Assert.Contains(host.Text(key, "uk"), page.Text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC-005: an <c>upgrade_required</c> answer records a domain and a client ID without a successful check.
    /// It licenses nothing here either, exactly as US-009 FR-003 already rules (spec I-1).
    /// </summary>
    [Fact]
    public async Task ADomainRecordedWithoutASuccessfulCheck_IsNotTreatedAsKnown()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.NeverConfirmed);
        await using var _host = host;
        await host.InsertLegitimacyStateAsync(ct, lastSuccessfulCheckAt: null, compatibility: "upgrade_required");

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(ConnectionInstructionTestData.TextKeys.NotConfirmed, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(ConnectionInstructionTestData.ClientId, page.Text, StringComparison.Ordinal);
    }

    public static TheoryData<ReadOnlyModeHost.Cause, string> ReadOnlyReasons => new()
    {
        { ReadOnlyModeHost.Cause.NeverConfirmed, SignInTestData.TextKeys.ReadOnlyNotYetConfirmed },
        { ReadOnlyModeHost.Cause.Suspended, SignInTestData.TextKeys.ReadOnlySuspended },
        { ReadOnlyModeHost.Cause.GracePeriodExpired, SignInTestData.TextKeys.ReadOnlyGracePeriodExpired },
    };

    /// <summary>
    /// AC-005: in all three read-only causes the instruction is served — BR-026 names it as viewable — with
    /// the reason stated, and the answer is <c>200</c>, never the <c>409</c> a write would get (api-design §2.3).
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadOnlyReasons))]
    public async Task InReadOnlyMode_TheInstructionIsServedWithItsReason(ReadOnlyModeHost.Cause cause, string reasonKey)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(reasonKey, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-005: the instruction itself stays complete in read-only mode, not reduced to the reason.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheInstructionIsStillComplete(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        foreach (var scope in ConnectionInstructionTestData.Scopes)
        {
            Assert.Contains(scope, page.Text, StringComparison.Ordinal);
        }

        Assert.Contains(host.Text(ConnectionInstructionTestData.TextKeys.ScopesLabel, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-005: a suspended school past nothing else still sees its own client ID — the values come from the
    /// installation's own database, so an unreachable Control Plane changes nothing.
    /// </summary>
    [Fact]
    public async Task InReadOnlyMode_TheClientIdIsStillShown()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Contains(ConnectionInstructionTestData.ClientId, page.Text, StringComparison.Ordinal);
        Assert.Contains(ConnectionInstructionTestData.Domain, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-007: rendering writes nothing — no audit row, no row in any table, however often it is opened.</summary>
    [Fact]
    public async Task OpeningThePage_WritesNothingAtAll()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        var before = await host.TableRowCountsAsync(ct);
        var auditBefore = await host.AuditRowsAsync(ct);

        var first = await client.OpenInstructionAsync(ct);
        await client.OpenInstructionAsync(ct);
        await client.OpenInstructionAsync(ct);

        // The page must really be served: "writes nothing" is also true of a path that answers 404.
        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(before, await host.TableRowCountsAsync(ct));
        Assert.Equal(auditBefore, await host.AuditRowsAsync(ct));
    }

    /// <summary>AC-007, AC-008: nor does it write when the state is incomplete.</summary>
    [Fact]
    public async Task OpeningThePageWithNoLegitimacyState_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.NeverConfirmed);
        await using var _host = host;
        var before = await host.TableRowCountsAsync(ct);

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(before, await host.TableRowCountsAsync(ct));
        Assert.Empty(await host.LegitimacyStatesAsync(ct));
    }

    /// <summary>AC-007: nor in read-only mode, where a write would also have to be a permitted service write.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task OpeningThePageInReadOnlyMode_WritesNothing(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;
        var before = await host.TableRowCountsAsync(ct);

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(before, await host.TableRowCountsAsync(ct));
    }

    /// <summary>AC-001, spec FR-013: the settings section carries the entry that leads here.</summary>
    [Fact]
    public async Task TheLandingPage_LinksToTheInstruction()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Contains(
            host.Text(ConnectionInstructionTestData.TextKeys.NavigationEntry, "uk"),
            landing.Text,
            StringComparison.Ordinal);
        Assert.Contains(ConnectionInstructionTestData.Path, landing.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Spec FR-013: the entry joins the section US-009 created rather than replacing it — both settings pages
    /// are reachable from the same place.
    /// </summary>
    [Fact]
    public async Task TheSettingsSection_CarriesBothEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Contains(WorkspaceConnectionTestData.Path, landing.Body, StringComparison.Ordinal);
        Assert.Contains(ConnectionInstructionTestData.Path, landing.Body, StringComparison.Ordinal);
    }
}
