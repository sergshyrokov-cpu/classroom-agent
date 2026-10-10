using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-031 AC-009, AC-007 (spec FR-011, FR-010, I-5, OD-010 a; openapi <c>LastSynchronizationView</c>): the query adds
/// the watermark, converted in Application to the school's time zone, and the step that stopped a failed run. The zone
/// is <c>Europe/Kyiv</c>, not UTC, so a mix-up of local and UTC time fails (TC-8).
/// </summary>
public sealed class LastSynchronizationMeetQueryTests
{
    private static readonly DateTimeOffset Start = InstallationTestHost.DefaultStart;

    private static GetLastSynchronizationQuery QueryOver(SyncWorld world) =>
        new(world.States, new SchoolTimeZone(JournalTestData.Kyiv));

    private static SyncState CompletedWithWatermark(SyncWorld world, DateTimeOffset started, DateTimeOffset watermark)
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), started);
        world.States.Add(state);
        state.CompleteRun(watermark, 1, watermark);
        return state;
    }

    /// <summary>
    /// AC-009, OD-010 a: in summer (UTC+3) 21:30 UTC is 00:30 of the next day in Kyiv — the local date and time, with no
    /// offset, is what the view carries.
    /// </summary>
    [Fact]
    public async Task TheWatermark_IsTheSchoolsLocalDateTime_InSummer()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var watermark = new DateTimeOffset(2026, 9, 23, 21, 30, 0, TimeSpan.Zero);
        CompletedWithWatermark(world, watermark - TimeSpan.FromMinutes(5), watermark);

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Equal(new DateTime(2026, 9, 24, 0, 30, 0), view.MeetLoadedUpTo);
    }

    /// <summary>AC-009: in winter (UTC+2) the offset differs — the conversion uses the zone, not a fixed offset.</summary>
    [Fact]
    public async Task TheWatermark_IsTheSchoolsLocalDateTime_InWinter()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var watermark = new DateTimeOffset(2026, 1, 15, 22, 10, 0, TimeSpan.Zero);
        CompletedWithWatermark(world, watermark - TimeSpan.FromMinutes(5), watermark);

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Equal(new DateTime(2026, 1, 16, 0, 10, 0), view.MeetLoadedUpTo);
    }

    /// <summary>AC-009: no successful Meet pull yet — the view carries none ("not loaded yet").</summary>
    [Fact]
    public async Task WithNoWatermark_TheViewCarriesNone()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.States.Add(SyncState.BeginFirstRun(SyncWorld.RunId(1), Start));

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Null(view.MeetLoadedUpTo);
    }

    /// <summary>api-design §3: the watermark is independent of the status — a running run keeps the previous one visible.</summary>
    [Fact]
    public async Task ARunningRun_KeepsThePreviousWatermarkVisible()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var watermark = new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);
        var state = CompletedWithWatermark(world, watermark - TimeSpan.FromMinutes(5), watermark);
        state.BeginRun(SyncWorld.RunId(2), watermark + TimeSpan.FromHours(1));

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Equal(LastSynchronizationStatus.Running, view.Status);
        Assert.Equal(new DateTime(2026, 9, 23, 13, 0, 0), view.MeetLoadedUpTo);
        Assert.Null(view.FailedStep);
    }

    /// <summary>AC-007, FR-010: a failed run carries the step that stopped it, and the earlier watermark stays visible.</summary>
    [Theory]
    [InlineData(SyncStep.Classroom)]
    [InlineData(SyncStep.Meet)]
    public async Task AFailedRun_CarriesItsStep_AndThePreviousWatermark(SyncStep step)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var watermark = new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);
        var state = CompletedWithWatermark(world, watermark - TimeSpan.FromMinutes(5), watermark);
        state.BeginRun(SyncWorld.RunId(2), watermark + TimeSpan.FromHours(1));
        state.FailRun(watermark + TimeSpan.FromHours(2), 0, SyncDiagnosis.ScopeNotAuthorized, step);

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Equal(LastSynchronizationStatus.Failed, view.Status);
        Assert.Equal(step, view.FailedStep);
        Assert.Equal(SyncDiagnosis.ScopeNotAuthorized, view.Diagnosis);
        Assert.Equal(new DateTime(2026, 9, 23, 13, 0, 0), view.MeetLoadedUpTo);
    }

    /// <summary>FR-010: a completed run carries no step.</summary>
    [Fact]
    public async Task ACompletedRun_CarriesNoStep()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        CompletedWithWatermark(world, Start, Start + TimeSpan.FromMinutes(1));

        var view = await QueryOver(world).ExecuteAsync(ct);

        Assert.Equal(LastSynchronizationStatus.Completed, view.Status);
        Assert.Null(view.FailedStep);
        Assert.NotNull(view.MeetLoadedUpTo);
    }
}
