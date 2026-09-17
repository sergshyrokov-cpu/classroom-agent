using ClassroomAgent.Application.Models;
using ClassroomAgent.Contracts;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-005 AC-002: the check runs right after start, then 6 hours after a success or 15 minutes after a
/// failure, one at a time, and nothing a check does stops the schedule (spec FR-003, I-5). Time is the
/// manual clock of the test strategy §3: waiting is driven by <see cref="TimeProvider"/> timers.
/// </summary>
public sealed class LegitimacyCheckScheduleTests(PostgreSqlFixture database)
{
    private static readonly TimeSpan AfterSuccess = TimeSpan.FromHours(6);
    private static readonly TimeSpan AfterFailure = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task FirstCheck_RunsRightAfterStart_WithoutAdvancingTime()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();

        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);

        Assert.Equal(host.Time.GetUtcNow(), host.ControlPlane.Calls[0].At);
    }

    [Fact]
    public async Task Call_CarriesConfiguredInstallationId_ContractVersion_AndReleaseVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();

        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);

        var call = host.ControlPlane.Calls[0];
        Assert.Equal(host.InstallationId, call.InstallationId);
        Assert.Equal(ContractVersion.Current, call.ContractVersion);
        Assert.Equal(1, call.ContractVersion);
        Assert.Matches(@"^(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})$", call.ApplicationVersion);
    }

    [Fact]
    public async Task AfterSuccess_NextCheckIsSixHoursLater()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane.ReplySuccess().ReplySuccess();

        host.Start();
        await host.WaitForNextCheckAtAsync(start + AfterSuccess, ct);
        host.Time.Advance(AfterSuccess - TimeSpan.FromSeconds(1));
        Assert.Single(host.ControlPlane.Calls);
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.ControlPlane.WaitForCallsAsync(2, ct);

        Assert.Equal(start + AfterSuccess, host.ControlPlane.Calls[1].At);
        await host.WaitForNextCheckAtAsync(start + AfterSuccess + AfterSuccess, ct);
    }

    [Theory]
    [InlineData(CheckFailureCategory.Unreachable)]
    [InlineData(CheckFailureCategory.Timeout)]
    [InlineData(CheckFailureCategory.ErrorAnswer)]
    [InlineData(CheckFailureCategory.UnparseableAnswer)]
    [InlineData(CheckFailureCategory.UnknownInstallation)]
    public async Task AfterFailure_NextCheckIsFifteenMinutesLater(CheckFailureCategory category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane.ReplyFailure(category);

        host.Start();

        await host.WaitForNextCheckAtAsync(start + AfterFailure, ct);
        Assert.DoesNotContain(start + AfterSuccess, host.Time.PendingDueTimes);
    }

    [Fact]
    public async Task AfterUpgradeRequiredAnswer_NextCheckIsFifteenMinutesLater()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane.Reply(FakeControlPlaneClient.Answer(compatibility: Domain.Enums.CompatibilityState.UpgradeRequired));

        host.Start();

        await host.WaitForNextCheckAtAsync(start + AfterFailure, ct);
    }

    [Fact]
    public async Task AfterSuspendedAnswer_NextCheckIsSixHoursLater()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane.Reply(FakeControlPlaneClient.Answer(status: Domain.Enums.InstallationStatus.Suspended));

        host.Start();

        await host.WaitForNextCheckAtAsync(start + AfterSuccess, ct);
    }

    [Fact]
    public async Task FailuresRetryEveryFifteenMinutes_UntilASuccess_ThenSixHours()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane
            .ReplyFailure(CheckFailureCategory.Unreachable)
            .ReplyFailure(CheckFailureCategory.Timeout)
            .ReplyFailure(CheckFailureCategory.ErrorAnswer)
            .ReplySuccess();

        host.Start();
        for (var i = 1; i <= 3; i++)
        {
            await host.WaitForNextCheckAtAsync(start + (AfterFailure * i), ct);
            host.Time.Advance(AfterFailure);
            await host.ControlPlane.WaitForCallsAsync(i + 1, ct);
        }

        await host.WaitForNextCheckAtAsync(start + (AfterFailure * 3) + AfterSuccess, ct);
        Assert.Equal(
            new[] { start, start + AfterFailure, start + (AfterFailure * 2), start + (AfterFailure * 3) },
            host.ControlPlane.Calls.Select(c => c.At));
    }

    [Fact]
    public async Task IntervalCountsFromCompletion_NotFromStart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        var slow = host.ControlPlane.ReplyLater();

        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(TimeSpan.FromSeconds(20));
        slow.SetResult(FakeControlPlaneClient.Answer());

        await host.WaitForNextCheckAtAsync(start + TimeSpan.FromSeconds(20) + AfterSuccess, ct);
    }

    [Fact]
    public async Task OnlyOneCheckRunsAtATime_EvenWhenACallHangs()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var hanging = host.ControlPlane.ReplyLater();

        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        // Below the 30-second call limit: a timeout ending the call is not what this test is about.
        foreach (var step in new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(9) })
        {
            host.Time.Advance(step);
        }

        hanging.TrySetResult(FakeControlPlaneClient.Answer());
        var released = host.Time.GetUtcNow();
        await host.Time.WaitUntilAsync(
            () => host.Time.PendingDueTimes.Any(d => d > released),
            "the next check to be scheduled after the hanging call",
            ct);

        Assert.Single(host.ControlPlane.Calls);
    }

    [Fact]
    public async Task ExceptionInACheck_DoesNotStopTheSchedule_CountsAsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane
            .Throw(new InvalidOperationException("zz-client-exception-marker"))
            .ReplySuccess();

        host.Start();
        await host.WaitForNextCheckAtAsync(start + AfterFailure, ct);
        host.Time.Advance(AfterFailure);
        await host.ControlPlane.WaitForCallsAsync(2, ct);

        await host.WaitForNextCheckAtAsync(start + AfterFailure + AfterSuccess, ct);
        var rows = await host.LegitimacyStatesAsync(ct);
        Assert.Equal(start + AfterFailure, Assert.Single(rows).LastSuccessfulCheckAt);
    }

    [Fact]
    public async Task ExceptionInACheck_DoesNotStopTheHost()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane.Throw(new HttpRequestException("boom"));

        host.Start();
        await host.WaitForNextCheckAtAsync(start + AfterFailure, ct);

        var live = await host.SendPrivateAsync("GET", "/health/live", ct);
        Assert.Equal(System.Net.HttpStatusCode.OK, live.Status);
    }

    [Fact]
    public async Task HostStop_WhileACallHangs_CompletesAndStartsNoNewCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplyLater();

        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        await host.StopAsync().WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Single(host.ControlPlane.Calls);
    }
}
