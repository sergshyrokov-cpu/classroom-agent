using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-011 AC-008: at every start the installation runs the same eight steps as "Проверить доступ" and writes the
/// outcome to the log only — <c>Information</c> when access is in place, <c>Error</c> otherwise; when it cannot run
/// it says it was skipped and why, at <c>Warning</c>. It never delays or prevents the start, writes no audit row and
/// no table, and its lines carry no address, domain, token or Google text (spec FR-010, FR-014, OD-004, I-6, I-7;
/// DC-5, DC-10; SC-10).
/// </summary>
public sealed class StartupSelfCheckTests(PostgreSqlFixture database)
{
    private const string Completed = AccessCheckTestData.LogEvents.SelfCheckCompleted;
    private const string Skipped = AccessCheckTestData.LogEvents.SelfCheckSkipped;

    [Fact]
    public async Task WithAccessInPlace_TheSelfCheckRunsTheEightSteps_AndLogsInformation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, probe) = await AccessCheckHostExtensions.StartWithSelfCheckAsync(database, ct);
        await using var _host = host;

        var events = await HostLogs.WaitForEventAsync(host.LogDirectory, Completed, 1, ct);

        var line = Assert.Single(events, e => e.EventName == Completed);
        Assert.Equal("Information", line.Level);
        Assert.Equal(AccessCheckTestData.Verdict.AccessInPlace, line.Property("Verdict"));
        Assert.Equal(8, probe.BackgroundCalls.Count);
        Assert.Equal(
            AccessCheckTestData.Scopes,
            probe.BackgroundCalls.Where(c => c.Kind == ProbeCallKind.Delegation).Select(c => c.Scope));
        Assert.All(
            probe.BackgroundCalls.Where(c => c.Kind == ProbeCallKind.Delegation),
            c => Assert.Equal(AccessCheckTestData.TechnicalAccount, c.TechnicalAccount));
    }

    /// <summary>DC-10: a failed self-check is an Error line with the verdict — what the Owner reads after a key rotation.</summary>
    [Fact]
    public async Task WithAMissingScope_TheSelfCheckLogsAnError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, _) = await AccessCheckHostExtensions.StartWithSelfCheckAsync(
            database,
            ct,
            script: p => p.Delegation[AccessCheckTestData.Scopes[3]] = AccessCheckStepOutcome.ScopeNotAuthorized);
        await using var _host = host;

        var events = await HostLogs.WaitForEventAsync(host.LogDirectory, Completed, 1, ct);

        var line = Assert.Single(events, e => e.EventName == Completed);
        Assert.Equal("Error", line.Level);
        Assert.Equal(AccessCheckTestData.Verdict.NotConfigured, line.Property("Verdict"));
    }

    /// <summary>Spec FR-010: the line carries each step's outcome — here, the key the Owner forgot to place.</summary>
    [Fact]
    public async Task WithTheKeyUnavailable_TheErrorLineNamesTheOutcome()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, probe) = await AccessCheckHostExtensions.StartWithSelfCheckAsync(
            database,
            ct,
            script: p =>
            {
                foreach (var scope in AccessCheckTestData.Scopes)
                {
                    p.Delegation[scope] = AccessCheckStepOutcome.KeyUnavailable;
                }
            });
        await using var _host = host;

        var events = await HostLogs.WaitForEventAsync(host.LogDirectory, Completed, 1, ct);

        var line = Assert.Single(events, e => e.EventName == Completed);
        Assert.Equal("Error", line.Level);
        Assert.Contains(nameof(AccessCheckStepOutcome.KeyUnavailable), line.Line, StringComparison.Ordinal);
        Assert.Single(probe.BackgroundCalls);
    }

    /// <summary>OD-004, I-6, I-7: in read-only mode the self-check does not run — one Warning line says so, and Google gets nothing.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheSelfCheckIsSkipped_WithAWarning_AndCallsNothing(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, probe) = await AccessCheckHostExtensions.StartWithSelfCheckAsync(database, ct, cause);
        await using var _host = host;

        var events = await HostLogs.WaitForEventAsync(host.LogDirectory, Skipped, 1, ct);

        var line = Assert.Single(events, e => e.EventName == Skipped);
        Assert.Equal("Warning", line.Level);
        Assert.False(string.IsNullOrEmpty(line.Property("Reason")));
        Assert.DoesNotContain(events, e => e.EventName == Completed);
        Assert.Empty(probe.Calls);
    }

    /// <summary>OD-004: without a usable connection the self-check is skipped too, with a different reason.</summary>
    [Theory]
    [InlineData(SeededConnection.None)]
    [InlineData(SeededConnection.ForAnotherDomain)]
    public async Task WithoutAUsableConnection_TheSelfCheckIsSkipped_WithAWarning(SeededConnection connection)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, probe) = await AccessCheckHostExtensions.StartWithSelfCheckAsync(database, ct, connection: connection);
        await using var _host = host;

        var events = await HostLogs.WaitForEventAsync(host.LogDirectory, Skipped, 1, ct);

        var line = Assert.Single(events, e => e.EventName == Skipped);
        Assert.Equal("Warning", line.Level);
        Assert.Empty(probe.Calls);
    }

    /// <summary>OD-004: the two skip reasons are distinguishable in the log.</summary>
    [Fact]
    public async Task TheTwoSkipReasons_AreDifferent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readOnly, _) = await AccessCheckHostExtensions.StartWithSelfCheckAsync(database, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _readOnly = readOnly;
        var (unconfigured, _) = await AccessCheckHostExtensions.StartWithSelfCheckAsync(database, ct, connection: SeededConnection.None);
        await using var _unconfigured = unconfigured;

        var first = Assert.Single(await HostLogs.WaitForEventAsync(readOnly.LogDirectory, Skipped, 1, ct), e => e.EventName == Skipped);
        var second = Assert.Single(await HostLogs.WaitForEventAsync(unconfigured.LogDirectory, Skipped, 1, ct), e => e.EventName == Skipped);

        Assert.NotEqual(first.Property("Reason"), second.Property("Reason"));
    }

    /// <summary>FR-010: a failing port does not stop the installation — the start completes and the page answers.</summary>
    [Fact]
    public async Task AFailingPort_DoesNotPreventTheStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, probe) = await AccessCheckHostExtensions.StartWithSelfCheckAsync(database, ct, script: p => p.Throws = true);
        await using var _host = host;

        await probe.FirstCall.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);
        using var client = host.CreateClient();
        var signIn = await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.Equal(System.Net.HttpStatusCode.OK, signIn.Status);
    }

    /// <summary>FR-010: the self-check writes no audit row and no table.</summary>
    [Fact]
    public async Task TheSelfCheck_WritesNothingToTheDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, _) = await AccessCheckHostExtensions.StartWithSelfCheckAsync(database, ct);
        await using var _host = host;

        await HostLogs.WaitForEventAsync(host.LogDirectory, Completed, 1, ct);

        Assert.Empty(await host.AuditRowsAsync(ct));
        Assert.Single(await host.WorkspaceConnectionsAsync(ct));
    }

    /// <summary>SC-10, FR-014: no log line carries the technical account, the domain or the token.</summary>
    [Fact]
    public async Task TheLog_CarriesNoAddressDomainOrToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, _) = await AccessCheckHostExtensions.StartWithSelfCheckAsync(database, ct);
        await using var _host = host;

        await HostLogs.WaitForEventAsync(host.LogDirectory, Completed, 1, ct);
        var all = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));

        Assert.DoesNotContain(AccessCheckTestData.TechnicalAccount, all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InstallationTestData.Domain, all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(FakeGoogleAccessProbe.IssuedToken, all, StringComparison.Ordinal);
    }
}
