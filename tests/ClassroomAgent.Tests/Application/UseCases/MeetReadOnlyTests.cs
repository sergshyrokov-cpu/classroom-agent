using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MeetTestData;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-031 AC-010 (spec FR-001, §7; AD-6, TC-5): in read-only mode — for each BR-025 reason — no run starts, so the Meet
/// step never runs and the substituted <c>IMeetReportsReader</c> receives no call. An unusable connection is the same
/// skip. The control world, not read-only, calls the port once, so "no call" is not a property of an idle double.
/// </summary>
public sealed class MeetReadOnlyTests
{
    public static TheoryData<LegitimacyModeReason> Reasons => new(Enum.GetValues<LegitimacyModeReason>());

    private static void Seed(SyncWorld world) =>
        world.Meet.WithPage(Event(1, 1, world.Time.GetUtcNow() - TimeSpan.FromDays(1), 600, Teacher(1), Teacher(1)));

    /// <summary>The control: outside read-only mode the same seeded world reads Meet once and stores the meeting.</summary>
    [Fact]
    public async Task OutsideReadOnlyMode_TheMeetPortIsCalled()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        Seed(world);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Single(world.Meet.Calls);
        Assert.Single(world.MeetSessions.Stored);
    }

    /// <summary>AC-010: each read-only reason skips the run; the Meet port and the meetings' store are not touched.</summary>
    [Theory]
    [MemberData(nameof(Reasons))]
    public async Task InReadOnlyMode_TheMeetPortReceivesNoCall(LegitimacyModeReason reason)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(readOnly: true, readOnlyReason: reason);
        Seed(world);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(reason, outcome.ReadOnlyReason);
        Assert.Empty(world.Meet.Calls);
        Assert.Equal(0, world.MeetSessions.Reads);
        Assert.Empty(world.MeetSessions.Added);
        Assert.Null(world.States.Stored);
    }

    /// <summary>US-013 FR-005: with no usable connection there is no run, so no Meet call either.</summary>
    [Fact]
    public async Task WithNoUsableConnection_TheMeetPortReceivesNoCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld(connection: SeededConnection.None);
        Seed(world);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Empty(world.Meet.Calls);
        Assert.Equal(0, world.MeetSessions.Reads);
    }
}
