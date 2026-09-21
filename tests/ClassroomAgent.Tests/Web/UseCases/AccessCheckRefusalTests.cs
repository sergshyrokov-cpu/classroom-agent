using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-011 AC-005, AC-006: the two refusals of openapi POST 409. In read-only mode — any BR-025 cause — no call reaches
/// Google, not even a token request; without a usable connection there is nothing to check. Both are audited as
/// refused, and the read-only guard runs first (spec FR-006, FR-009, I-11; api-design §2.4; S-03; TC-5). The port is
/// substituted and records zero calls — this closes the carried US-007 finding F-5 for the first real Google port.
/// </summary>
public sealed class AccessCheckRefusalTests(PostgreSqlFixture database)
{
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheRunIsRefusedWith409_AndNoCallReachesGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        var response = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Empty(probe.Calls);
        Assert.Null(AccessCheckHostExtensions.VerdictOf(response));
    }

    /// <summary>API-5: the 409 names the reason — the host-wide US-008 handler's translated page.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheRefusalNamesTheReason(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        var response = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text(ReasonKeyOf(cause), "uk"), response.Text, StringComparison.Ordinal);
    }

    /// <summary>FR-008, db-design §3.2: the read-only refusal is audited as refused / read_only_mode, with the connection id.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheRefusalIsAudited(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;
        var connectionId = await host.ConnectionIdAsync(ct);

        var response = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        var row = Assert.Single(await host.AccessCheckAuditRowsAsync(ct));
        Assert.Equal(AccessCheckTestData.Audit.Refused, row.Outcome);
        Assert.Equal(AccessCheckTestData.Audit.ReadOnlyMode, row.RefusalCategory);
        Assert.Equal(AccessCheckTestData.Audit.TargetType, row.TargetType);
        Assert.Equal(connectionId, row.TargetId);
    }

    /// <summary>
    /// api-design §2.4: a school both read-only and unconfigured answers with the read-only reason — the guard runs
    /// before the connection is read.
    /// </summary>
    [Fact]
    public async Task ReadOnlyAndUnconfigured_AnswersWithTheReadOnlyReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.Suspended,
            SeededConnection.None);
        await using var _host = host;

        var response = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Empty(probe.Calls);
        var row = Assert.Single(await host.AccessCheckAuditRowsAsync(ct));
        Assert.Equal(AccessCheckTestData.Audit.ReadOnlyMode, row.RefusalCategory);
        Assert.Null(row.TargetId);
        Assert.DoesNotContain(host.Text(AccessCheckTestData.TextKeys.RefusedNotConfigured, "uk"), response.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-005: with no connection saved the run is refused, says what to do first, and calls nothing.</summary>
    [Fact]
    public async Task WithoutASavedConnection_TheRunIsRefused_AndCallsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(
            database,
            ct,
            connection: SeededConnection.None);
        await using var _host = host;

        var response = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.RefusedNotConfigured, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Empty(probe.RequestCalls);
        Assert.Null(AccessCheckHostExtensions.VerdictOf(response));
    }

    /// <summary>Spec I-11, US-009 OD-002: a connection saved for another domain is not usable — refused the same way.</summary>
    [Fact]
    public async Task WithAConnectionForAnotherDomain_TheRunIsRefused_AndCallsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(
            database,
            ct,
            connection: SeededConnection.ForAnotherDomain);
        await using var _host = host;

        var response = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.RefusedDomainMismatch, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Empty(probe.RequestCalls);
    }

    /// <summary>db-design §3.2: no connection → refused / connection_not_usable, target type set, no target id.</summary>
    [Fact]
    public async Task WithoutASavedConnection_TheRefusalIsAudited_WithoutATargetId()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(
            database,
            ct,
            connection: SeededConnection.None);
        await using var _host = host;

        await client.RunAccessCheckAsync(ct);

        var row = Assert.Single(await host.AccessCheckAuditRowsAsync(ct));
        Assert.Equal(AccessCheckTestData.Audit.Refused, row.Outcome);
        Assert.Equal(AccessCheckTestData.Audit.ConnectionNotUsable, row.RefusalCategory);
        Assert.Equal(AccessCheckTestData.Audit.TargetType, row.TargetType);
        Assert.Null(row.TargetId);
    }

    /// <summary>db-design §3.2: a mismatched connection → refused / connection_not_usable, with the row's id.</summary>
    [Fact]
    public async Task WithAConnectionForAnotherDomain_TheRefusalIsAudited_WithTheConnectionId()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(
            database,
            ct,
            connection: SeededConnection.ForAnotherDomain);
        await using var _host = host;
        var connectionId = await host.ConnectionIdAsync(ct);

        await client.RunAccessCheckAsync(ct);

        var row = Assert.Single(await host.AccessCheckAuditRowsAsync(ct));
        Assert.Equal(AccessCheckTestData.Audit.ConnectionNotUsable, row.RefusalCategory);
        Assert.Equal(connectionId, row.TargetId);
    }

    /// <summary>A refusal changes nothing else: the stored connection is untouched.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task ARefusal_LeavesTheConnectionUntouched(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;
        var before = await host.WorkspaceConnectionsAsync(ct);

        var response = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal(before, await host.WorkspaceConnectionsAsync(ct));
    }

    private static string ReasonKeyOf(ReadOnlyModeHost.Cause cause) => cause switch
    {
        ReadOnlyModeHost.Cause.NeverConfirmed => SignInTestData.TextKeys.RefusedNotYetConfirmed,
        ReadOnlyModeHost.Cause.Suspended => SignInTestData.TextKeys.RefusedSuspendedByOwner,
        ReadOnlyModeHost.Cause.GracePeriodExpired => SignInTestData.TextKeys.RefusedGracePeriodExpired,
        _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, null),
    };
}
