using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-005 AC-009, AC-011: the Application query tells whether the installation is in read-only mode and
/// why — never confirmed, suspended, or more than 7 days since the last success (strict) — from the stored
/// row and the clock, also after a restart with the Control Plane unreachable (spec FR-008, I-4; BR-025).
/// In every test the Control Plane never answers, so the stored row is exactly what was seeded.
/// </summary>
public sealed class LegitimacyModeQueryTests(PostgreSqlFixture database)
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

    [Fact]
    public async Task NoRow_IsReadOnly_NotYetConfirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(ct, seed: null);

        var mode = await QueryAsync(host, ct);

        Assert.Equal(new LegitimacyMode(true, LegitimacyModeReason.NotYetConfirmed, null), mode);
    }

    [Fact]
    public async Task RowWithoutLastSuccess_IsReadOnly_NotYetConfirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(ct, seed: h => h.InsertLegitimacyStateAsync(ct, null, compatibility: "upgrade_required"));

        var mode = await QueryAsync(host, ct);

        Assert.Equal(new LegitimacyMode(true, LegitimacyModeReason.NotYetConfirmed, null), mode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(60 * 24 * 6)]
    public async Task ActiveAndRecent_IsNotReadOnly(int minutesAgo)
    {
        var ct = TestContext.Current.CancellationToken;
        var lastSuccess = InstallationTestHost.DefaultStart - TimeSpan.FromMinutes(minutesAgo);
        await using var host = await StartedAsync(ct, seed: h => h.InsertLegitimacyStateAsync(ct, lastSuccess));

        var mode = await QueryAsync(host, ct);

        Assert.False(mode.IsReadOnly);
        Assert.Null(mode.Reason);
        Assert.Equal(lastSuccess, mode.LastSuccessfulCheckAt);
    }

    [Fact]
    public async Task ExactlySevenDays_IsNotYetReadOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var lastSuccess = InstallationTestHost.DefaultStart - GracePeriod;
        await using var host = await StartedAsync(ct, seed: h => h.InsertLegitimacyStateAsync(ct, lastSuccess));

        var mode = await QueryAsync(host, ct);

        Assert.False(mode.IsReadOnly);
    }

    [Fact]
    public async Task SevenDaysAndOneSecond_IsReadOnly_GracePeriodExpired_WithLastSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var lastSuccess = InstallationTestHost.DefaultStart - GracePeriod - TimeSpan.FromSeconds(1);
        await using var host = await StartedAsync(ct, seed: h => h.InsertLegitimacyStateAsync(ct, lastSuccess));

        var mode = await QueryAsync(host, ct);

        Assert.Equal(new LegitimacyMode(true, LegitimacyModeReason.GracePeriodExpired, lastSuccess), mode);
    }

    [Fact]
    public async Task GracePeriod_ExpiresAsTheClockMoves_WithoutAnyCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var lastSuccess = InstallationTestHost.DefaultStart - TimeSpan.FromDays(6);
        await using var host = await StartedAsync(ct, seed: h => h.InsertLegitimacyStateAsync(ct, lastSuccess));
        Assert.False((await QueryAsync(host, ct)).IsReadOnly);

        host.Time.Advance(TimeSpan.FromDays(1));
        Assert.False((await QueryAsync(host, ct)).IsReadOnly);
        host.Time.Advance(TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond));

        var mode = await QueryAsync(host, ct);
        Assert.True(mode.IsReadOnly);
        Assert.Equal(LegitimacyModeReason.GracePeriodExpired, mode.Reason);
    }

    [Fact]
    public async Task Suspended_IsReadOnly_SuspendedByOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        var lastSuccess = InstallationTestHost.DefaultStart - TimeSpan.FromHours(1);
        await using var host = await StartedAsync(ct, seed: h => h.InsertLegitimacyStateAsync(ct, lastSuccess, status: "suspended"));

        var mode = await QueryAsync(host, ct);

        Assert.Equal(new LegitimacyMode(true, LegitimacyModeReason.SuspendedByOwner, lastSuccess), mode);
    }

    [Fact]
    public async Task SuspendedAndExpired_ReasonIsSuspendedByOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        var lastSuccess = InstallationTestHost.DefaultStart - TimeSpan.FromDays(30);
        await using var host = await StartedAsync(ct, seed: h => h.InsertLegitimacyStateAsync(ct, lastSuccess, status: "suspended"));

        var mode = await QueryAsync(host, ct);

        Assert.True(mode.IsReadOnly);
        Assert.Equal(LegitimacyModeReason.SuspendedByOwner, mode.Reason);
    }

    [Theory]
    [InlineData("upgrade_recommended")]
    [InlineData("upgrade_required")]
    public async Task CompatibilityAlone_DoesNotMakeItReadOnly_WhileWithinGrace(string compatibility)
    {
        var ct = TestContext.Current.CancellationToken;
        var lastSuccess = InstallationTestHost.DefaultStart - TimeSpan.FromDays(3);
        await using var host = await StartedAsync(ct, seed: h => h.InsertLegitimacyStateAsync(ct, lastSuccess, compatibility: compatibility));

        var mode = await QueryAsync(host, ct);

        Assert.False(mode.IsReadOnly);
    }

    [Fact]
    public async Task AfterRestart_WithControlPlaneUnreachable_TwoDaysAgoWorks_EightDaysAgoIsReadOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var first = await InstallationTestHost.CreateAsync(database, ct);
        var confirmedAt = first.Time.GetUtcNow();
        first.ControlPlane.ReplySuccess();
        first.Start();
        await first.WaitForNextCheckAtAsync(confirmedAt + TimeSpan.FromHours(6), ct);
        await first.StopAsync();

        await using (var twoDaysLater = InstallationTestHost.Restart(first, confirmedAt + TimeSpan.FromDays(2)))
        {
            twoDaysLater.ControlPlane.ReplyFailure(CheckFailureCategory.Unreachable);
            twoDaysLater.Start();
            await twoDaysLater.WaitForNextCheckAtAsync(confirmedAt + TimeSpan.FromDays(2) + TimeSpan.FromMinutes(15), ct);

            Assert.False((await QueryAsync(twoDaysLater, ct)).IsReadOnly);
            Assert.Single(await twoDaysLater.LegitimacyStatesAsync(ct));
        }

        await using var eightDaysLater = InstallationTestHost.Restart(first, confirmedAt + TimeSpan.FromDays(8));
        eightDaysLater.ControlPlane.ReplyFailure(CheckFailureCategory.Unreachable);
        eightDaysLater.Start();
        await eightDaysLater.WaitForNextCheckAtAsync(confirmedAt + TimeSpan.FromDays(8) + TimeSpan.FromMinutes(15), ct);

        var mode = await QueryAsync(eightDaysLater, ct);
        Assert.Equal(new LegitimacyMode(true, LegitimacyModeReason.GracePeriodExpired, confirmedAt), mode);
    }

    [Fact]
    public async Task FirstSuccess_EndsNotYetConfirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        var reply = host.ControlPlane.ReplyLater();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        Assert.Equal(LegitimacyModeReason.NotYetConfirmed, (await QueryAsync(host, ct)).Reason);

        reply.SetResult(FakeControlPlaneClient.Answer());
        await host.WaitForNextCheckAtAsync(start + TimeSpan.FromHours(6), ct);

        Assert.Equal(new LegitimacyMode(false, null, start), await QueryAsync(host, ct));
    }

    private async Task<InstallationTestHost> StartedAsync(CancellationToken cancellationToken, Func<InstallationTestHost, Task<int>>? seed)
    {
        var host = await InstallationTestHost.CreateAsync(database, cancellationToken);
        if (seed is not null)
        {
            await seed(host);
        }

        host.Start();
        return host;
    }

    private static async Task<LegitimacyMode> QueryAsync(InstallationTestHost host, CancellationToken cancellationToken)
    {
        using var scope = host.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GetLegitimacyModeQuery>().ExecuteAsync(cancellationToken);
    }
}
