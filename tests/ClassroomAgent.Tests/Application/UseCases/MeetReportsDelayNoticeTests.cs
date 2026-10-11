using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-033 AC-011 (spec FR-012; openapi <c>MeetDelayNotice</c>): the notice carries the Meet watermark as a local
/// <c>Europe/Kyiv</c> date-time, or null when nothing was ever loaded — no synchronization state at all, or one whose
/// Meet watermark is still empty.
/// </summary>
public sealed class MeetReportsDelayNoticeTests
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk");

    private static async Task<DateTime?> NoticeAsync(SyncWorld.StateRepository states)
    {
        var source = new FakeMeetReportSource();
        var course = source.AddCourse("Course");
        var zone = new SchoolTimeZone(MeetReportsTestData.Kyiv);
        var query = new GetMeetReportsQuery(
            source.Courses,
            source,
            new GetLastSynchronizationQuery(states, zone),
            zone,
            new ManualTimeProvider(MeetReportsTestData.Now));
        var request = new MeetReportsRequest(
            [course.ToString(CultureInfo.InvariantCulture)],
            [MeetReportsTestData.Period.FromText],
            [MeetReportsTestData.Period.ToText]);

        var result = await query.ExecuteAsync(request, Uk, TestContext.Current.CancellationToken);

        Assert.Equal(MeetReportsPageOutcome.Shown, result.Outcome);
        Assert.NotNull(result.Page.Reports);
        return result.Page.Reports.DelayNotice.MeetLoadedUpTo;
    }

    private static SyncWorld.StateRepository StatesWithWatermark(DateTimeOffset watermark)
    {
        var states = new SyncWorld.StateRepository();
        var started = watermark - TimeSpan.FromMinutes(5);
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), started);
        states.Add(state);
        state.CompleteRun(watermark, 1, watermark);
        return states;
    }

    /// <summary>AC-011: in summer (UTC+3) 21:30 UTC is 00:30 of the next day in Kyiv — local date and time, no offset.</summary>
    [Fact]
    public async Task TheWatermark_IsALocalDateTime_InSummer()
    {
        var watermark = new DateTimeOffset(2026, 10, 14, 21, 30, 0, TimeSpan.Zero);

        var loadedUpTo = await NoticeAsync(StatesWithWatermark(watermark));

        Assert.Equal(new DateTime(2026, 10, 15, 0, 30, 0), loadedUpTo);
    }

    /// <summary>AC-011: after the change on 2026-10-25 the offset is +2 — the conversion uses the zone, not a fixed offset.</summary>
    [Fact]
    public async Task TheWatermark_IsALocalDateTime_InWinter()
    {
        var watermark = new DateTimeOffset(2026, 10, 30, 22, 10, 0, TimeSpan.Zero);

        var loadedUpTo = await NoticeAsync(StatesWithWatermark(watermark));

        Assert.Equal(new DateTime(2026, 10, 31, 0, 10, 0), loadedUpTo);
    }

    /// <summary>AC-011: the installation never synchronized — there is no watermark ("not loaded yet").</summary>
    [Fact]
    public async Task WithNoSyncState_TheWatermarkIsNull()
    {
        var loadedUpTo = await NoticeAsync(new SyncWorld.StateRepository());

        Assert.Null(loadedUpTo);
    }

    /// <summary>AC-011: a first run that has not finished yet has no Meet watermark either.</summary>
    [Fact]
    public async Task WithANullWatermark_TheWatermarkIsNull()
    {
        var states = new SyncWorld.StateRepository();
        states.Add(SyncState.BeginFirstRun(SyncWorld.RunId(1), MeetReportsTestData.Now));

        var loadedUpTo = await NoticeAsync(states);

        Assert.Null(loadedUpTo);
    }
}
