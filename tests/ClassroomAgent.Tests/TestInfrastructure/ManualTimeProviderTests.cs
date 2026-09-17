namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Guards the US-005 test seam itself: the manual clock drives the framework timers the installation
/// uses for its schedule and its call timeout, so a red schedule test means missing behaviour, not a
/// broken clock.
/// </summary>
public sealed class ManualTimeProviderTests
{
    private static readonly DateTimeOffset Start = InstallationTestHost.DefaultStart;

    [Fact]
    public async Task TaskDelay_CompletesOnlyWhenAdvancedToItsDueTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var time = new ManualTimeProvider(Start);

        var delay = Task.Delay(TimeSpan.FromHours(6), time, ct);
        await time.WaitForTimerAtAsync(Start + TimeSpan.FromHours(6), ct);
        time.Advance(TimeSpan.FromHours(6) - TimeSpan.FromTicks(1));
        Assert.False(delay.IsCompleted);
        time.Advance(TimeSpan.FromTicks(1));

        await delay.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);
        Assert.Empty(time.PendingDueTimes);
    }

    [Fact]
    public async Task CancellationTokenSourceWithTimeProvider_CancelsWhenAdvanced()
    {
        var ct = TestContext.Current.CancellationToken;
        var time = new ManualTimeProvider(Start);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), time);

        time.Advance(TimeSpan.FromSeconds(29));
        Assert.False(timeout.IsCancellationRequested);
        time.Advance(TimeSpan.FromSeconds(1));

        await Task.Delay(Timeout.Infinite, timeout.Token).ContinueWith(_ => { }, ct);
        Assert.True(timeout.IsCancellationRequested);
    }

    [Fact]
    public async Task PeriodicTimer_TicksOncePerPeriod()
    {
        var ct = TestContext.Current.CancellationToken;
        var time = new ManualTimeProvider(Start);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15), time);

        var first = timer.WaitForNextTickAsync(ct).AsTask();
        time.Advance(TimeSpan.FromMinutes(15));
        Assert.True(await first.WaitAsync(ManualTimeProvider.RealTimeLimit, ct));
        Assert.Contains(Start + TimeSpan.FromMinutes(30), time.PendingDueTimes);
    }

    [Fact]
    public async Task Advance_FiresTimersInDueOrder_AtTheirOwnInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        var time = new ManualTimeProvider(Start);
        var fired = new List<DateTimeOffset>();
        using var late = time.CreateTimer(_ => fired.Add(time.GetUtcNow()), null, TimeSpan.FromHours(2), Timeout.InfiniteTimeSpan);
        using var early = time.CreateTimer(_ => fired.Add(time.GetUtcNow()), null, TimeSpan.FromHours(1), Timeout.InfiniteTimeSpan);

        time.Advance(TimeSpan.FromDays(1));

        Assert.Equal(new[] { Start + TimeSpan.FromHours(1), Start + TimeSpan.FromHours(2) }, fired);
        Assert.Equal(Start + TimeSpan.FromDays(1), time.GetUtcNow());
        await Task.CompletedTask.WaitAsync(ct);
    }

    [Fact]
    public async Task FakeClient_WithoutScriptedReply_WaitsUntilCancelled_AndRecordsTheCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var time = new ManualTimeProvider(Start);
        var client = new FakeControlPlaneClient(time);
        using var cancel = new CancellationTokenSource();

        var call = client.CheckAsync(Guid.NewGuid(), "1.0.0", 1, cancel.Token);
        await client.WaitForCallsAsync(1, ct);
        Assert.False(call.IsCompleted);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Equal(Start, Assert.Single(client.Calls).At);
    }
}
