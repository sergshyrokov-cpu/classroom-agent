using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-018 entity model §1–§3: the incremental rule (spec FR-001, FR-005, VR-001 … VR-003), the course's remembered
/// value (FR-002) and the full-run time on <see cref="SyncState"/> (FR-006, I-3) — pure domain, no port (TC-1).
/// </summary>
public sealed class IncrementalSyncRuleTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Start = new(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);

    // ---- IsFullRunDue (FR-005, VR-002) -----------------------------------------------------------------------------

    /// <summary>FR-005, I-6: the period is seven days, a constant.</summary>
    [Fact]
    public void TheFullRunPeriod_IsSevenDays() =>
        Assert.Equal(TimeSpan.FromDays(7), IncrementalSyncRule.FullRunPeriod);

    /// <summary>FR-005, VR-002: no full run has ever succeeded — the run is full.</summary>
    [Fact]
    public void NoFullRunYet_IsDue() =>
        Assert.True(IncrementalSyncRule.IsFullRunDue(null, Start));

    /// <summary>FR-005, OD-001 a: boundaries around seven days of instant arithmetic.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(7 * 24 * 60 - 1, false)]
    [InlineData(7 * 24 * 60, true)]
    [InlineData(30 * 24 * 60, true)]
    public void TheRunIsFull_FromSevenDaysAfterTheLastFullRun(int minutesSince, bool due) =>
        Assert.Equal(due, IncrementalSyncRule.IsFullRunDue(Start - TimeSpan.FromMinutes(minutesSince), Start));

    /// <summary>FR-005: one tick short of seven days is not due.</summary>
    [Fact]
    public void OneTickShortOfSevenDays_IsNotDue() =>
        Assert.False(IncrementalSyncRule.IsFullRunDue(Start - TimeSpan.FromDays(7) + TimeSpan.FromTicks(1), Start));

    /// <summary>VR-002, I-5: a stored time later than the start (clock moved back) makes the run full.</summary>
    [Fact]
    public void AFullRunTimeLaterThanTheStart_IsDue() =>
        Assert.True(IncrementalSyncRule.IsFullRunDue(Start + TimeSpan.FromTicks(1), Start));

    /// <summary>FR-005: instant arithmetic, no time zone — the same instant in another offset decides the same.</summary>
    [Fact]
    public void TheDecision_IgnoresTheOffset()
    {
        var lastFull = (Start - TimeSpan.FromDays(6)).ToOffset(TimeSpan.FromHours(3));

        Assert.False(IncrementalSyncRule.IsFullRunDue(lastFull, Start));
    }

    // ---- ToStoragePrecision (VR-001, db-design §5) ------------------------------------------------------------------

    /// <summary>VR-001, db-design §5: sub-microsecond ticks are truncated, not rounded.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(9)]
    public void SubMicrosecondTicks_AreTruncated(int ticks) =>
        Assert.Equal(T, IncrementalSyncRule.ToStoragePrecision(T.AddTicks(ticks)));

    /// <summary>VR-001: a whole microsecond is kept.</summary>
    [Fact]
    public void WholeMicroseconds_AreKept()
    {
        var value = T.AddTicks(10 * 123_456);

        Assert.Equal(value.UtcTicks, IncrementalSyncRule.ToStoragePrecision(value).UtcTicks);
    }

    /// <summary>db-design §5: the stored value is UTC — the same instant, offset zero.</summary>
    [Fact]
    public void TheValue_IsInUtc()
    {
        var local = T.ToOffset(TimeSpan.FromHours(3)).AddTicks(3);

        var stored = IncrementalSyncRule.ToStoragePrecision(local);

        Assert.Equal(TimeSpan.Zero, stored.Offset);
        Assert.Equal(T.UtcTicks, stored.UtcTicks);
    }

    /// <summary>VR-001: normalising twice changes nothing, so both sides of the comparison agree.</summary>
    [Fact]
    public void Normalising_IsIdempotent()
    {
        var once = IncrementalSyncRule.ToStoragePrecision(T.AddTicks(17));

        Assert.Equal(once, IncrementalSyncRule.ToStoragePrecision(once));
    }

    // ---- IsUnchangedArchive (FR-001, VR-001, VR-003) ----------------------------------------------------------------

    /// <summary>FR-001: archived and equal — unchanged.</summary>
    [Fact]
    public void AnArchivedCourse_WithTheRememberedValue_IsUnchanged() =>
        Assert.True(IncrementalSyncRule.IsUnchangedArchive(CourseState.Archived, T, T));

    /// <summary>VR-001, I-4: Google's finer fraction against the stored, truncated value — still equal.</summary>
    [Fact]
    public void AFinerListedValue_EqualsTheTruncatedStoredOne() =>
        Assert.True(IncrementalSyncRule.IsUnchangedArchive(CourseState.Archived, T.AddTicks(9), T));

    /// <summary>VR-001: the same instant in another offset is equal.</summary>
    [Fact]
    public void TheSameInstantInAnotherOffset_IsEqual() =>
        Assert.True(IncrementalSyncRule.IsUnchangedArchive(CourseState.Archived, T.ToOffset(TimeSpan.FromHours(2)), T));

    /// <summary>FR-001 (d): one microsecond apart is a change.</summary>
    [Fact]
    public void OneMicrosecondApart_IsAChange() =>
        Assert.False(IncrementalSyncRule.IsUnchangedArchive(CourseState.Archived, T.AddTicks(10), T));

    /// <summary>FR-001 (b), (c), VR-001: a missing value on either side — or both — is never equal.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AMissingValue_IsNeverEqual(bool listedMissing, bool storedMissing) =>
        Assert.False(IncrementalSyncRule.IsUnchangedArchive(
            CourseState.Archived,
            listedMissing ? null : T,
            storedMissing ? null : T));

    /// <summary>FR-001 (a), VR-003: only ARCHIVED qualifies.</summary>
    [Theory]
    [InlineData(CourseState.Active)]
    [InlineData(CourseState.Provisioned)]
    [InlineData(CourseState.Declined)]
    [InlineData(CourseState.Suspended)]
    public void ANonArchivedState_IsNeverUnchanged(CourseState state) =>
        Assert.False(IncrementalSyncRule.IsUnchangedArchive(state, T, T));

    // ---- Course (FR-002, entity model §2) ---------------------------------------------------------------------------

    private static CourseDetails Details(DateTimeOffset? updateTime, string name = "Test Course") =>
        new(name, null, null, null, null, null, null, updateTime, null, null, null, null);

    /// <summary>db-design §1: an imported course remembers nothing until a complete read is recorded.</summary>
    [Fact]
    public void AnImportedCourse_RemembersNothingYet() =>
        Assert.Null(Course.Import("100000000001", CourseState.Archived, Details(T)).CompleteReadUpdateTime);

    /// <summary>FR-002, entity model §2: recording a complete read remembers the listed updateTime at storage precision.</summary>
    [Fact]
    public void RecordingACompleteRead_RemembersTheListedUpdateTime_AtStoragePrecision()
    {
        var course = Course.Import("100000000001", CourseState.Archived, Details(T.AddTicks(4)));

        course.RecordCompleteRead();

        Assert.Equal(T, course.CompleteReadUpdateTime);
        Assert.Equal(TimeSpan.Zero, course.CompleteReadUpdateTime!.Value.Offset);
    }

    /// <summary>FR-002: a course committed with no updateTime from Google remembers none.</summary>
    [Fact]
    public void RecordingACompleteRead_WithoutAnUpdateTime_RemembersNone()
    {
        var course = Course.Import("100000000001", CourseState.Archived, Details(T));
        course.RecordCompleteRead();

        course.UpdateFrom(CourseState.Archived, Details(null));
        course.RecordCompleteRead();

        Assert.Null(course.CompleteReadUpdateTime);
    }

    /// <summary>db-design §1, entity model §2: refreshing the mirror does not touch the remembered value.</summary>
    [Fact]
    public void UpdatingFromTheListing_LeavesTheRememberedValue()
    {
        var course = Course.Import("100000000001", CourseState.Archived, Details(T));
        course.RecordCompleteRead();

        course.UpdateFrom(CourseState.Active, Details(T.AddHours(5), "Renamed"));

        Assert.Equal(T, course.CompleteReadUpdateTime);
        Assert.Equal(T.AddHours(5), course.UpdateTime);
    }

    // ---- SyncState (FR-006, I-3, entity model §3) -------------------------------------------------------------------

    private static SyncState CompletedFullRun(DateTimeOffset start)
    {
        var state = SyncState.BeginFirstRun(Guid.NewGuid(), start);
        state.CompleteRun(start.AddMinutes(10), 0, start, fullRun: true);
        return state;
    }

    /// <summary>FR-006: a first row has no full-run time.</summary>
    [Fact]
    public void ANewRow_HasNoFullRunTime() =>
        Assert.Null(SyncState.BeginFirstRun(Guid.NewGuid(), Start).LastFullRunAt);

    /// <summary>FR-006, I-3: a completed full run records its start, not its finish.</summary>
    [Fact]
    public void ACompletedFullRun_RecordsItsStart()
    {
        var state = CompletedFullRun(Start);

        Assert.Equal(Start, state.LastFullRunAt);
        Assert.True(state.LastFullRunAt <= state.LastSuccessfulRunAt);
    }

    /// <summary>FR-006: a completed run that is not full leaves the full-run time — explicitly or by default.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ACompletedNonFullRun_LeavesTheFullRunTime(bool explicitFalse)
    {
        var state = CompletedFullRun(Start);
        var next = Start.AddHours(1);
        state.BeginRun(Guid.NewGuid(), next);

        if (explicitFalse)
        {
            state.CompleteRun(next.AddMinutes(10), 0, next, fullRun: false);
        }
        else
        {
            state.CompleteRun(next.AddMinutes(10), 0, next);
        }

        Assert.Equal(Start, state.LastFullRunAt);
    }

    /// <summary>FR-006, OD-002 a: starting and failing a run leave the full-run time.</summary>
    [Fact]
    public void BeginningAndFailingARun_LeaveTheFullRunTime()
    {
        var state = CompletedFullRun(Start);
        var next = Start.AddDays(7);

        state.BeginRun(Guid.NewGuid(), next);
        Assert.Equal(Start, state.LastFullRunAt);

        state.FailRun(next.AddMinutes(1), 0, SyncDiagnosis.GoogleUnavailable, SyncStep.Classroom);
        Assert.Equal(Start, state.LastFullRunAt);
    }

    /// <summary>FR-006, I-3: a later full run replaces the time with its own start.</summary>
    [Fact]
    public void ALaterFullRun_ReplacesTheTime()
    {
        var state = CompletedFullRun(Start);
        var next = Start.AddDays(7);
        state.BeginRun(Guid.NewGuid(), next);

        state.CompleteRun(next.AddMinutes(10), 0, next, fullRun: true);

        Assert.Equal(next, state.LastFullRunAt);
    }
}
