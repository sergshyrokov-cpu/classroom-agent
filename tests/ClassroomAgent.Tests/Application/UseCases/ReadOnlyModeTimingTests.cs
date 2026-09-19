using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-007 AC-008, AC-009: the mode is determined at the moment of the write, from the current row and the
/// current clock - never cached for the process lifetime. A change takes effect on the next write, without
/// a restart and without any manual step (spec FR-001, I-5).
/// </summary>
public sealed class ReadOnlyModeTimingTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task TheGracePeriodExpiring_TakesEffectOnTheNextWrite_WithoutARestart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var lastSuccess = InstallationTestHost.DefaultStart - TimeSpan.FromDays(6);
        await host.InsertLegitimacyStateAsync(ct, lastSuccess);
        host.ConfigureServices = ReadOnlyModeHost.Register;
        host.Start();

        await WriteAsync(host, ct);

        host.Time.Advance(TimeSpan.FromDays(2));

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => WriteAsync(host, ct));
    }

    [Fact]
    public async Task ASuspensionRecorded_TakesEffectOnTheNextWrite_WithoutARestart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await WriteAsync(host, ct);

        await host.ExecuteAsync("UPDATE legitimacy_state SET status = 'suspended'", ct);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => WriteAsync(host, ct));
    }

    [Fact]
    public async Task AResumption_RestoresWrites_WithoutARestart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.Suspended, ct);
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => WriteAsync(host, ct));

        await host.ExecuteAsync("UPDATE legitimacy_state SET status = 'active'", ct);

        await WriteAsync(host, ct);
        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.Equal(SyntheticWriteUseCase.MarkerDomain, row.Domain);
    }

    [Fact]
    public async Task ASuccessfulCheck_EndsTheRefusal_InTheSameHost()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.GracePeriodExpired, ct);
        host.ConfigureServices = ReadOnlyModeHost.Register;
        var start = host.Time.GetUtcNow();
        host.ControlPlane.ReplySuccess();
        host.Start();

        // The LegitimacyState write that carries the recovery is permitted throughout (AC-003, AC-009).
        await host.WaitForNextCheckAtAsync(start + TimeSpan.FromHours(6), ct);

        await WriteAsync(host, ct);
        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.Equal(SyntheticWriteUseCase.MarkerDomain, row.Domain);
    }

    [Fact]
    public async Task ExactlySevenDays_StillWrites_AndOneTickLaterDoesNot()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var lastSuccess = InstallationTestHost.DefaultStart - TimeSpan.FromDays(7);
        await host.InsertLegitimacyStateAsync(ct, lastSuccess);
        host.ConfigureServices = ReadOnlyModeHost.Register;
        host.Start();

        // US-005 I-4: "more than 7 days" is strict, and the boundary moves with the injected clock.
        await WriteAsync(host, ct);

        host.Time.Advance(TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond));

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => WriteAsync(host, ct));
    }

    private static Task WriteAsync(InstallationTestHost host, CancellationToken cancellationToken) =>
        ReadOnlyModeHost.InScopeAsync<SyntheticWriteUseCase>(
            host,
            useCase => useCase.ExecuteAsync(host.Time.GetUtcNow(), cancellationToken));
}
