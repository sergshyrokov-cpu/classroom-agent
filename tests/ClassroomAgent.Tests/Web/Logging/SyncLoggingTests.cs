using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-013 AC-007: a run logs its start and its finish with the counter at <c>Information</c>, a failure at
/// <c>Error</c> and a skip at <c>Information</c>; every line of a run carries that run's identifier; no line
/// carries personal data; and a scheduled run writes no audit row (spec FR-012, FR-017, I-4, I-7; DC-10, SC-10,
/// SC-11).
/// </summary>
public sealed class SyncLoggingTests(PostgreSqlFixture database)
{
    /// <summary>AC-007: the start of a run is an Information line.</summary>
    [Fact]
    public async Task ARunStart_IsLoggedAtInformation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);

        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunStarted, ct);

        var line = Assert.Single(events, e => e.EventName == SyncTestData.LogEvents.RunStarted);
        Assert.Equal("Information", line.Level);
    }

    /// <summary>AC-007, DC-10: the finish line carries the counter — zero while the pipeline is empty (I-1).</summary>
    [Fact]
    public async Task ARunFinish_IsLoggedAtInformation_WithTheCounter()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);

        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var line = Assert.Single(events, e => e.EventName == SyncTestData.LogEvents.RunCompleted);
        Assert.Equal("Information", line.Level);
        Assert.Equal("0", line.Property("ProcessedCount"));
    }

    /// <summary>
    /// AC-007, DC-10: every line a run writes carries that run's identifier, so one run reads end to end
    /// (<c>trebovaniya.md</c> §8).
    /// </summary>
    [Fact]
    public async Task EveryLineOfARun_CarriesTheRunIdentifier()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);
        var row = await host.WaitForFinishedRunAsync(ct);
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var events = await HostLogs.ReadEventsAsync(host.LogDirectory, ct);

        var ofRun = events.Where(e =>
            e.EventName == SyncTestData.LogEvents.RunStarted || e.EventName == SyncTestData.LogEvents.RunCompleted).ToList();
        Assert.Equal(2, ofRun.Count);
        Assert.All(ofRun, e => Assert.Equal(row.RunId.ToString(), e.Property("RunId")));
    }

    /// <summary>AC-004, I-4: a skipped run says so at Information, with a category and no free text.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task ASkippedRun_IsLoggedAtInformation_WithItsReason(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, cause);

        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunSkipped, ct);

        var line = Assert.Single(events, e => e.EventName == SyncTestData.LogEvents.RunSkipped);
        Assert.Equal("Information", line.Level);
        Assert.False(string.IsNullOrEmpty(line.Property("Reason")));
        Assert.DoesNotContain(events, e => e.EventName == SyncTestData.LogEvents.RunCompleted);
    }

    /// <summary>AC-005: a missing connection is a skip with a different reason from read-only mode.</summary>
    [Fact]
    public async Task TheTwoSkipReasons_AreDifferent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var readOnly = await SyncHostExtensions.StartAsync(database, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var unconfigured = await SyncHostExtensions.StartAsync(database, ct, connection: SeededConnection.None);

        var first = Assert.Single(
            await readOnly.WaitForLogEventAsync(SyncTestData.LogEvents.RunSkipped, ct),
            e => e.EventName == SyncTestData.LogEvents.RunSkipped);
        var second = Assert.Single(
            await unconfigured.WaitForLogEventAsync(SyncTestData.LogEvents.RunSkipped, ct),
            e => e.EventName == SyncTestData.LogEvents.RunSkipped);

        Assert.NotEqual(first.Property("Reason"), second.Property("Reason"));
    }

    /// <summary>AC-007, SC-10: no line carries the technical account, the school's domain or the connection string.</summary>
    [Fact]
    public async Task TheLog_CarriesNoPersonalDataAndNoConnectionString()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var all = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));

        Assert.DoesNotContain(AccessCheckTestData.TechnicalAccount, all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InstallationTestData.Domain, all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(host.ConnectionString, all, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC-007, FR-017: a scheduled run writes <b>no</b> audit row — <c>trebovaniya.md</c> §5 audits the manual
    /// start, which is US-019.
    /// </summary>
    [Fact]
    public async Task AScheduledRun_WritesNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);
        await host.WaitForFinishedRunAsync(ct);

        Assert.Empty(await host.AuditRowsAsync(ct));
    }
}
