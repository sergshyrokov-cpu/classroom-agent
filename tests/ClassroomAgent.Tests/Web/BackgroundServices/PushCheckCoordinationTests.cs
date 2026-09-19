using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-006 AC-009, AC-011: a push starts a check at once, at most one a minute and never two at a time;
/// a push that cannot start one leaves a single pending check that runs as soon as it is allowed (v77), and
/// the schedule restarts from the pushed check (spec FR-010; api-design §5).
/// </summary>
public sealed class PushCheckCoordinationTests(PostgreSqlFixture database)
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan AfterSuccess = TimeSpan.FromHours(6);
    private static readonly TimeSpan AfterFailure = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The minute is counted from the start of the previous check started by a push; a scheduled check does
    /// not start it (`trebovaniya.md` v77 §9; Story AC-011; spec FR-010, I-11).
    /// </summary>
    [Fact]
    public async Task PushWithinAMinuteOfThePreviousPushCheck_LeavesOnePendingCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(database, ct, replies: 4);

        // No push-triggered check has run yet, so this one starts at once and starts the minute.
        host.Time.Advance(Minute);
        await PushAsync(host, ct);
        await host.ControlPlane.WaitForCallsAsync(2, ct);

        host.Time.Advance(TimeSpan.FromSeconds(10));
        await PushAsync(host, ct);
        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.Equal(2, host.ControlPlane.Calls.Count);

        host.Time.Advance(TimeSpan.FromSeconds(50));

        await host.ControlPlane.WaitForCallsAsync(3, ct);
        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.Equal(3, host.ControlPlane.Calls.Count);
    }

    [Fact]
    public async Task ManyPushesWithinAMinute_LeaveOnlyOnePendingCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(database, ct, replies: 6);

        // The first push starts a check and the minute with it; the four that follow fall inside that minute.
        host.Time.Advance(Minute);
        await PushAsync(host, ct);
        await host.ControlPlane.WaitForCallsAsync(2, ct);

        host.Time.Advance(TimeSpan.FromSeconds(5));
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await PushAsync(host, ct)).Status);
        }

        host.Time.Advance(TimeSpan.FromSeconds(55));
        await host.ControlPlane.WaitForCallsAsync(3, ct);
        host.Time.Advance(Minute);

        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.Equal(3, host.ControlPlane.Calls.Count);
    }

    [Fact]
    public async Task PushAfterAMinute_StartsACheckAtOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(database, ct, replies: 3);

        host.Time.Advance(Minute);
        await PushAsync(host, ct);

        await host.ControlPlane.WaitForCallsAsync(2, ct);
        Assert.Equal(host.Time.GetUtcNow(), host.ControlPlane.Calls[1].At);
    }

    [Fact]
    public async Task PushWhileACheckIsRunning_StartsNoSecondCheck_ButLeavesAPendingOne()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var running = host.ControlPlane.ReplyLater();
        host.ControlPlane.ReplySuccess().ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(Minute + Minute);

        await PushAsync(host, ct);
        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.Single(host.ControlPlane.Calls);

        running.SetResult(FakeControlPlaneClient.Answer());

        await host.ControlPlane.WaitForCallsAsync(2, ct);
    }

    [Fact]
    public async Task PendingCheckAfterARunningScheduledCheck_StillRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var running = host.ControlPlane.ReplyLater();
        host.ControlPlane.ReplySuccess().ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);

        // The push arrives while the very first check is still waiting for its answer.
        await PushAsync(host, ct);
        running.SetResult(FakeControlPlaneClient.Answer());

        // The minute since the running check is not over, so the pending check waits for it.
        host.Time.Advance(Minute);
        await host.ControlPlane.WaitForCallsAsync(2, ct);
    }

    [Fact]
    public async Task AfterAPushedSuccessfulCheck_TheNextScheduledCheckIsSixHoursLater()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(database, ct, replies: 3);
        host.Time.Advance(Minute);

        await PushAsync(host, ct);
        await host.ControlPlane.WaitForCallsAsync(2, ct);
        var pushedAt = host.Time.GetUtcNow();
        await host.WaitForNextCheckAtAsync(pushedAt + AfterSuccess, ct);

        host.Time.Advance(AfterSuccess);
        await host.ControlPlane.WaitForCallsAsync(3, ct);
        Assert.Equal(pushedAt + AfterSuccess, host.ControlPlane.Calls[2].At);
    }

    [Fact]
    public async Task AfterAPushedUnsuccessfulCheck_TheNextScheduledCheckIsFifteenMinutesLater()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        host.ControlPlane.ReplyFailure(ClassroomAgent.Application.Models.CheckFailureCategory.Unreachable);
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(Minute);

        await PushAsync(host, ct);
        await host.ControlPlane.WaitForCallsAsync(2, ct);
        var pushedAt = host.Time.GetUtcNow();
        await host.WaitForNextCheckAtAsync(pushedAt + AfterFailure, ct);

        host.Time.Advance(AfterFailure);
        await host.ControlPlane.WaitForCallsAsync(3, ct);
    }

    [Fact]
    public async Task ThePendingCheckStartsItsOwnMinute()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(database, ct, replies: 4);

        host.Time.Advance(TimeSpan.FromSeconds(30));
        await PushAsync(host, ct);
        host.Time.Advance(TimeSpan.FromSeconds(30));
        await host.ControlPlane.WaitForCallsAsync(2, ct);

        // A push right after the pending check ran is inside its minute: no third check yet.
        await PushAsync(host, ct);
        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.Equal(2, host.ControlPlane.Calls.Count);

        host.Time.Advance(Minute);
        await host.ControlPlane.WaitForCallsAsync(3, ct);
    }

    [Fact]
    public async Task PushedChecksNeverRunConcurrently()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        var second = host.ControlPlane.ReplyLater();
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(Minute);
        await PushAsync(host, ct);
        await host.ControlPlane.WaitForCallsAsync(2, ct);

        host.Time.Advance(Minute);
        await PushAsync(host, ct);
        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.Equal(2, host.ControlPlane.Calls.Count);

        second.SetResult(FakeControlPlaneClient.Answer());
        await host.ControlPlane.WaitForCallsAsync(3, ct);
    }

    private static async Task<InstallationTestHost> StartedAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        int replies)
    {
        var host = await InstallationTestHost.CreateAsync(database, cancellationToken);
        for (var i = 0; i < replies; i++)
        {
            host.ControlPlane.ReplySuccess();
        }

        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, cancellationToken);
        return host;
    }

    private static Task<InstallationTestHost.RawResponse> PushAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken) =>
        host.SendPrivateAsync("POST", PushTestData.StatusPushPath, cancellationToken, body: PushTestData.Body(host.InstallationId));
}
