using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MeetTestData;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-031 AC-006 (spec FR-002, FR-007, VR-002, I-1, OD-002, OD-003): which window the Meet step asks for, and when the
/// watermark moves. The clock is the injected manual one, so every window is an exact instant.
/// </summary>
public sealed class MeetWindowTests
{
    private static GoogleReadFailedException Configuration() =>
        new(GoogleReadFailureKind.Configuration, SyncDiagnosis.ScopeNotAuthorized);

    /// <summary>AC-006, OD-002, I-1: with no successful Meet pull yet the window is [now − 180 days + 1 hour, now).</summary>
    [Fact]
    public async Task TheFirstPull_AsksFrom180DaysLessAnHourAgo_UpToNow()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var call = Assert.Single(world.Meet.Calls);
        Assert.Equal(now - Horizon, call.From);
        Assert.Equal(now, call.To);
    }

    /// <summary>FR-007: a completed run stores the end of the window it asked for as the watermark.</summary>
    [Fact]
    public async Task ACompletedRun_StoresTheEndOfItsWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(SyncRunStatus.Completed, world.States.Stored!.Status);
        Assert.Equal(now, world.States.Stored.MeetLoadedUpTo);
    }

    /// <summary>AC-006, OD-003: after a success that ended at T the next window is [T − 3 days, now).</summary>
    [Fact]
    public async Task ALaterPull_AsksFromThreeDaysBeforeTheWatermark()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var watermark = world.Time.GetUtcNow();
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        world.Time.Advance(TimeSpan.FromHours(5));
        var now = world.Time.GetUtcNow();
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Equal(2, world.Meet.Calls.Count);
        Assert.Equal(watermark - Overlap, world.Meet.Calls[1].From);
        Assert.Equal(now, world.Meet.Calls[1].To);
        Assert.Equal(now, world.States.Stored!.MeetLoadedUpTo);
    }

    /// <summary>
    /// FR-002, I-1: a watermark older than Google's horizon (a long outage) asks for what Google still keeps — never
    /// earlier than now − 180 days + 1 hour.
    /// </summary>
    [Fact]
    public async Task AWatermarkOlderThanTheHorizon_IsClampedToIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        world.Time.Advance(TimeSpan.FromDays(200));
        var now = world.Time.GetUtcNow();
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Equal(now - Horizon, world.Meet.Calls[1].From);
    }

    /// <summary>FR-002: a watermark just inside the horizon is used as it is, three days back — the clamp does not bite early.</summary>
    [Fact]
    public async Task AWatermarkInsideTheHorizon_IsNotClamped()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var watermark = world.Time.GetUtcNow();
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        world.Time.Advance(TimeSpan.FromDays(170));
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Equal(watermark - Overlap, world.Meet.Calls[1].From);
    }

    /// <summary>
    /// AC-006, FR-007: a run whose Meet step fails leaves the watermark at T, and the run after it still asks from
    /// T − 3 days.
    /// </summary>
    [Fact]
    public async Task AFailedMeetStep_LeavesTheWatermark_AndTheNextWindowStartsFromIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var watermark = world.Time.GetUtcNow();
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Equal(watermark, world.States.Stored!.MeetLoadedUpTo);

        world.Time.Advance(TimeSpan.FromDays(1));
        world.Meet.FailOnFirstPage = Configuration();
        var failed = await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);
        Assert.True(failed.Failed);
        Assert.Equal(watermark, world.States.Stored.MeetLoadedUpTo);

        world.Time.Advance(TimeSpan.FromDays(1));
        world.Meet.Reset();
        await world.Run.ExecuteAsync(SyncWorld.RunId(3), ct);

        Assert.Equal(watermark - Overlap, world.Meet.Calls[2].From);
    }

    /// <summary>
    /// FR-001, FR-007: a run stopped by the Classroom step does not reach the Meet step — the Meet port is not called —
    /// and does not move the watermark.
    /// </summary>
    [Fact]
    public async Task ARunStoppedByTheClassroomStep_NeverReachesTheMeetStep()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var watermark = world.Time.GetUtcNow();
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Single(world.Meet.Calls);

        world.Time.Advance(TimeSpan.FromDays(1));
        world.Classroom.FailOnListing = Configuration();
        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.True(outcome.Failed);
        Assert.Single(world.Meet.Calls);
        Assert.Equal(watermark, world.States.Stored!.MeetLoadedUpTo);
    }

    /// <summary>FR-002: one window per run — the port is asked once, however many pages it answers with.</summary>
    [Fact]
    public async Task ARunOfManyPages_AsksForOneWindowOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var at = world.Time.GetUtcNow() - TimeSpan.FromDays(1);
        world.Meet
            .WithPage(Event(1, 1, at, 60, Teacher(1), Teacher(1)))
            .WithPage(Event(2, 2, at, 60, Teacher(1), Teacher(1)))
            .WithPage(Event(3, 3, at, 60, Teacher(1), Teacher(1)));

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Single(world.Meet.Calls);
        Assert.Equal(3, world.MeetSessions.Stored.Count);
    }
}
