using System.Reflection;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-024 spec FR-001 … FR-008, VR-003, entity model §3–§4: <see cref="GetDatabaseStatisticsQuery"/> maps the counters
/// and the single <c>sync_state</c> row to a <see cref="DatabaseStatisticsView"/> — the total as the sum of the five
/// states, the state line, the three times in the school's zone, the failure guidance by role — and carries no
/// diagnosis. The ports are in memory (TC-1); the counters over PostgreSQL are <c>DatabaseStatisticsReadSourceTests</c>.
/// </summary>
public sealed class DatabaseStatisticsQueryTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 22, 0, 0, TimeSpan.Zero);

    private static readonly DatabaseCounts SampleCounts = new(5, 4, 3, 2, 1, 40, 700, 120, 3000, 260, 17);

    public static TheoryData<AppRole> BothRoles => new(AppRole.Admin, AppRole.Dean);

    private static (GetDatabaseStatisticsQuery Query, FakeDatabaseStatisticsSource Counts, SyncWorld World) Build(
        DatabaseCounts? counts = null)
    {
        var source = new FakeDatabaseStatisticsSource { Counts = counts ?? SampleCounts };
        var world = new SyncWorld();
        return (new GetDatabaseStatisticsQuery(source, world.States, new SchoolTimeZone(JournalTestData.Kyiv)), source, world);
    }

    /// <summary>A completed full run: started 22:00 UTC, finished 22:30 UTC, Meet loaded up to 22:15 UTC, 1 March 2026.</summary>
    private static SyncState CompletedFullRun()
    {
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        state.CompleteRun(Start.AddMinutes(30), 12, Start.AddMinutes(15), fullRun: true);
        return state;
    }

    /// <summary>The completed full run, then a second run that failed with a diagnosis and a step.</summary>
    private static SyncState FailedAfterSuccess()
    {
        var state = CompletedFullRun();
        state.BeginRun(SyncWorld.RunId(2), Start.AddHours(2));
        state.FailRun(Start.AddHours(2).AddMinutes(1), 3, SyncDiagnosis.TechnicalAccountCannotRead, SyncStep.Meet);
        return state;
    }

    /// <summary>AC-002, FR-002, I-3: each of the five states is passed through and the total is their sum.</summary>
    [Fact]
    public async Task TheCourseCounters_AreTheFiveStates_AndTheirSum()
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, _, _) = Build();

        var view = await query.ExecuteAsync(AppRole.Dean, ct);

        Assert.Equal(new CourseCounts(5, 4, 3, 2, 1, 15), view.Courses);
    }

    /// <summary>AC-002, FR-002: a state with no course shows 0 and the total still sums the five.</summary>
    [Fact]
    public async Task AStateWithNoCourse_IsZero_AndTheTotalIsStillTheSum()
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, _, _) = Build(new DatabaseCounts(7, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0));

        var view = await query.ExecuteAsync(AppRole.Admin, ct);

        Assert.Equal(new CourseCounts(7, 0, 0, 3, 0, 10), view.Courses);
    }

    /// <summary>AC-003, AC-004, FR-003, FR-004: the other counters are passed through, each to its own field.</summary>
    [Fact]
    public async Task TheOtherCounters_ArePassedThrough_EachToItsOwnField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, _, _) = Build();

        var view = await query.ExecuteAsync(AppRole.Dean, ct);

        Assert.Equal(40, view.Teachers);
        Assert.Equal(700, view.Students);
        Assert.Equal(120, view.CourseWorkItems);
        Assert.Equal(3000, view.Submissions);
        Assert.Equal(260, view.MeetMeetings);
        Assert.Equal(17, view.MeetCodesAwaitingDecision);
    }

    /// <summary>AC-006, FR-005, I-2: no row — the state and the three times are "not yet"; no guidance for either role.</summary>
    [Theory]
    [MemberData(nameof(BothRoles))]
    public async Task WithNoRow_TheBlock_IsNotYet_WithNoTimes(AppRole role)
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, _, world) = Build(new DatabaseCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        Assert.Null(world.States.Stored);

        var view = await query.ExecuteAsync(role, ct);

        Assert.Equal(new SynchronizationBlock(SynchronizationDisplayState.NotYet, null, null, null), view.Synchronization);
        Assert.Equal(FailureGuidance.None, view.FailureGuidance);
        Assert.Equal(new CourseCounts(0, 0, 0, 0, 0, 0), view.Courses);
    }

    /// <summary>AC-005, FR-005: running → in progress, keeping the earlier success; completed → completed.</summary>
    [Fact]
    public async Task TheState_IsMappedFromTheStoredStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, _, world) = Build();
        var state = CompletedFullRun();
        world.States.Add(state);

        var completed = await query.ExecuteAsync(AppRole.Admin, ct);
        state.BeginRun(SyncWorld.RunId(2), Start.AddHours(2));
        var running = await query.ExecuteAsync(AppRole.Admin, ct);

        Assert.Equal(SynchronizationDisplayState.Completed, completed.Synchronization.State);
        Assert.Equal(SynchronizationDisplayState.InProgress, running.Synchronization.State);
        Assert.Equal(new DateTime(2026, 3, 2, 0, 30, 0), running.Synchronization.LastSuccessfulRunAt);
        Assert.Equal(FailureGuidance.None, completed.FailureGuidance);
        Assert.Equal(FailureGuidance.None, running.FailureGuidance);
    }

    /// <summary>
    /// AC-014, FR-005: 2026-03-01 22:30 UTC in Europe/Kyiv is 2 March 00:30; the full run's start (22:00) and the Meet
    /// watermark (22:15) are converted likewise; the values are wall-clock (<see cref="DateTimeKind.Unspecified"/>).
    /// </summary>
    [Fact]
    public async Task TheThreeTimes_AreInTheSchoolsZone()
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, _, world) = Build();
        world.States.Add(CompletedFullRun());

        var block = (await query.ExecuteAsync(AppRole.Dean, ct)).Synchronization;

        Assert.Equal(new DateTime(2026, 3, 2, 0, 30, 0), block.LastSuccessfulRunAt);
        Assert.Equal(new DateTime(2026, 3, 2, 0, 0, 0), block.LastFullRunAt);
        Assert.Equal(new DateTime(2026, 3, 2, 0, 15, 0), block.MeetLoadedUpTo);
        Assert.All(
            new[] { block.LastSuccessfulRunAt, block.LastFullRunAt, block.MeetLoadedUpTo },
            t => Assert.Equal(DateTimeKind.Unspecified, t!.Value.Kind));
    }

    /// <summary>AC-014, FR-005: in summer the zone is UTC+3 — 21:30 UTC on 10 July is 00:30 on 11 July.</summary>
    [Fact]
    public async Task InSummer_TheOffset_IsThreeHours()
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, _, world) = Build();
        var start = new DateTimeOffset(2026, 7, 10, 21, 0, 0, TimeSpan.Zero);
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), start);
        state.CompleteRun(start.AddMinutes(30), 1, start.AddMinutes(30));
        world.States.Add(state);

        var block = (await query.ExecuteAsync(AppRole.Admin, ct)).Synchronization;

        Assert.Equal(new DateTime(2026, 7, 11, 0, 30, 0), block.LastSuccessfulRunAt);
        Assert.Equal(new DateTime(2026, 7, 11, 0, 30, 0), block.MeetLoadedUpTo);
        Assert.Null(block.LastFullRunAt);
    }

    /// <summary>AC-005, OD-004: the block is the same for an Admin and a Dean.</summary>
    [Fact]
    public async Task TheBlock_IsTheSameForBothRoles()
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, _, world) = Build();
        world.States.Add(FailedAfterSuccess());

        var admin = await query.ExecuteAsync(AppRole.Admin, ct);
        var dean = await query.ExecuteAsync(AppRole.Dean, ct);

        Assert.Equal(admin.Synchronization, dean.Synchronization);
        Assert.Equal(admin with { FailureGuidance = FailureGuidance.None }, dean with { FailureGuidance = FailureGuidance.None });
    }

    /// <summary>
    /// AC-007, FR-006: a failed run reads "failed", keeps the earlier success and the earlier full run, and offers the
    /// Dean a hint and the Admin the connection page.
    /// </summary>
    [Theory]
    [InlineData(AppRole.Dean, FailureGuidance.AskAdmin)]
    [InlineData(AppRole.Admin, FailureGuidance.ConnectionPageLink)]
    public async Task AFailedRun_ReadsFailed_WithTheRolesGuidance(AppRole role, FailureGuidance expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, _, world) = Build();
        world.States.Add(FailedAfterSuccess());

        var view = await query.ExecuteAsync(role, ct);

        Assert.Equal(SynchronizationDisplayState.Failed, view.Synchronization.State);
        Assert.Equal(new DateTime(2026, 3, 2, 0, 30, 0), view.Synchronization.LastSuccessfulRunAt);
        Assert.Equal(new DateTime(2026, 3, 2, 0, 0, 0), view.Synchronization.LastFullRunAt);
        Assert.Equal(expected, view.FailureGuidance);
    }

    /// <summary>AC-007, I-7: a first run that failed — "failed", no times, and still the role's guidance.</summary>
    [Theory]
    [InlineData(AppRole.Dean, FailureGuidance.AskAdmin)]
    [InlineData(AppRole.Admin, FailureGuidance.ConnectionPageLink)]
    public async Task AFirstRunThatFailed_HasNoTimes_AndTheRolesGuidance(AppRole role, FailureGuidance expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, _, world) = Build();
        var state = SyncState.BeginFirstRun(SyncWorld.RunId(1), Start);
        state.FailRun(Start.AddMinutes(1), 0, SyncDiagnosis.KeyRejected, SyncStep.Classroom);
        world.States.Add(state);

        var view = await query.ExecuteAsync(role, ct);

        Assert.Equal(new SynchronizationBlock(SynchronizationDisplayState.Failed, null, null, null), view.Synchronization);
        Assert.Equal(expected, view.FailureGuidance);
    }

    /// <summary>
    /// AC-005, AC-013, FR-005, S-08, api-design §2.6: no member of the DTOs can carry the diagnosis, the failed step, the
    /// processed counter, the run id or the run's own start and end.
    /// </summary>
    [Fact]
    public void TheDtos_HaveNoFieldForADiagnosis_AStep_OrTheProcessedCounter()
    {
        string[] forbidden = ["Error", "Diagnosis", "Step", "Processed", "RunId", "StartedAt", "FinishedAt", "Message"];
        var members = new[] { typeof(DatabaseStatisticsView), typeof(SynchronizationBlock), typeof(CourseCounts) }
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .ToList();

        Assert.NotEmpty(members);
        foreach (var property in members)
        {
            Assert.DoesNotContain(forbidden, f => property.Name.Contains(f, StringComparison.OrdinalIgnoreCase));
            Assert.NotEqual(typeof(string), property.PropertyType);
        }
    }

    /// <summary>VR-003: a stored status outside the three known values is an unexpected failure.</summary>
    [Fact]
    public async Task AnUnknownStatus_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, _, world) = Build();
        var state = CompletedFullRun();
        typeof(SyncState).GetProperty(nameof(SyncState.Status))!.SetValue(state, (SyncRunStatus)42);
        world.States.Add(state);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => query.ExecuteAsync(AppRole.Admin, ct));
    }

    /// <summary>
    /// AC-009, AC-017, FR-008, FR-011, S-04: one page is one read of the counters and one read of the state; nothing is
    /// added or saved, and the result does not depend on the read-only state (the query has no guard to ask).
    /// </summary>
    [Fact]
    public async Task OnePage_IsOneReadOfEach_AndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (query, counts, world) = Build();
        world.States.Add(CompletedFullRun());
        var addedBefore = world.States.Added.Count;

        await query.ExecuteAsync(AppRole.Dean, ct);

        Assert.Equal(1, counts.Calls);
        Assert.Equal(1, world.States.Reads);
        Assert.Equal(addedBefore, world.States.Added.Count);
        Assert.Equal(0, world.Work.Commits);
    }

    /// <summary>FR-008, S-04: the query's only dependencies are the two read ports and the time zone — no guard, no unit of work, no Google port.</summary>
    [Fact]
    public void TheQuery_DependsOnReadPortsOnly()
    {
        var constructor = Assert.Single(typeof(GetDatabaseStatisticsQuery).GetConstructors());

        Assert.Equal(
            ["IDatabaseStatisticsSource", "ISyncStateRepository", "SchoolTimeZone"],
            constructor.GetParameters().Select(p => p.ParameterType.Name).Order(StringComparer.Ordinal));
    }
}
