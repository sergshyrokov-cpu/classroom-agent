using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-007 AC-010: a refused write is logged, not audited. One <c>Warning</c> line names the operation and
/// the reason category, carries no personal data and no payload (SC-10, DC-10), writes no audit row and no
/// data at all, and never re-logs the mode change US-005 already owns (spec FR-009).
/// </summary>
public sealed class ReadOnlyRefusalLoggingTests(PostgreSqlFixture database)
{
    /// <summary>The event name the refusal is logged with; the US-005 events are 5101 … 5114.</summary>
    private const string RefusedEvent = "ReadOnlyWriteRefused";

    [Fact]
    public async Task ARefusal_WritesOneWarningLine_NamingTheOperationAndTheReason()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.Suspended, ct);
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => WriteAsync(host, ct));

        var refusals = await RefusalsAsync(host, ct);

        var line = Assert.Single(refusals);
        Assert.Equal("Warning", line.Level);
        Assert.Contains(SyntheticWriteUseCase.Operation, line.Line, StringComparison.Ordinal);
        Assert.Contains(nameof(LegitimacyModeReason.SuspendedByOwner), line.Line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheBackstopRefusal_IsLoggedAsWell()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.Suspended, ct);
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => ReadOnlyModeHost.InScopeAsync<UnguardedWriteUseCase>(
            host,
            useCase => useCase.ExecuteAsync(host.Time.GetUtcNow(), ct)));

        var line = Assert.Single(await RefusalsAsync(host, ct));

        Assert.Equal("Warning", line.Level);
    }

    [Fact]
    public async Task TheLine_CarriesNoPersonalDataAndNoPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.GracePeriodExpired, ct);
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => WriteAsync(host, ct));

        var files = await host.ReadLogFilesAsync(ct);

        // SC-10: internal identifiers only - never a domain, a client id or a stack trace.
        Assert.All(files, content =>
        {
            Assert.DoesNotContain(InstallationTestData.Domain, content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(InstallationTestData.ClientId, content, StringComparison.Ordinal);
            Assert.DoesNotContain(SyntheticWriteUseCase.MarkerDomain, content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("StackTrace", content, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task RepeatedRefusals_DoNotRelogTheModeChange()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.Suspended, ct);
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => WriteAsync(host, ct));
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => WriteAsync(host, ct));

        var events = await host.ReadLogEventsAsync(ct);

        Assert.Equal(2, events.Count(e => e.EventName == RefusedEvent));
        Assert.True(events.Count(e => e.EventName == "ReadOnlyModeEntered") <= 1);
    }

    [Fact]
    public async Task ARefusal_WritesNoAuditRowAndNoData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.Suspended, ct);
        var before = await host.LegitimacyStatesAsync(ct);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => WriteAsync(host, ct));

        Assert.Equal(before, await host.LegitimacyStatesAsync(ct));

        // SC-11: a refused write is not an audited action, and this Story creates no AuditEvent table.
        var auditTables = await host.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'audit_event'",
            ct);
        Assert.Equal(0, auditTables);
    }

    private static async Task<IReadOnlyList<LogEvent>> RefusalsAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken) =>
        (await host.ReadLogEventsAsync(cancellationToken)).Where(e => e.EventName == RefusedEvent).ToList();

    private static Task WriteAsync(InstallationTestHost host, CancellationToken cancellationToken) =>
        ReadOnlyModeHost.InScopeAsync<SyntheticWriteUseCase>(
            host,
            useCase => useCase.ExecuteAsync(host.Time.GetUtcNow(), cancellationToken));
}
