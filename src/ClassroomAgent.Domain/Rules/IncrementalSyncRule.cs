using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// US-018 entity model §1: the incremental synchronization rule of spec FR-001, FR-005 and VR-001 … VR-003, with no
/// clock and no setting (AD-3, I-6).
/// </summary>
public static class IncrementalSyncRule
{
    /// <summary>Spec FR-005, I-6: a constant, not a setting.</summary>
    public static readonly TimeSpan FullRunPeriod = TimeSpan.FromDays(7);

    /// <summary>PostgreSQL <c>timestamptz</c> keeps microseconds; a <see cref="DateTimeOffset"/> tick is 100 ns.</summary>
    private const long TicksPerMicrosecond = 10;

    /// <summary>Spec FR-005, VR-002: null, a stored time later than the start (I-5), or at least <see cref="FullRunPeriod"/> before the start → full.</summary>
    public static bool IsFullRunDue(DateTimeOffset? lastFullRunAt, DateTimeOffset runStartedAt) =>
        lastFullRunAt is not { } last
        || last > runStartedAt
        || runStartedAt - last >= FullRunPeriod;

    /// <summary>db-design §5, VR-001: UTC, truncated to whole microseconds.</summary>
    public static DateTimeOffset ToStoragePrecision(DateTimeOffset value) =>
        new(value.UtcTicks - (value.UtcTicks % TicksPerMicrosecond), TimeSpan.Zero);

    /// <summary>Spec FR-001, VR-001, VR-003: listed state Archived, both instants present, equal at storage precision.</summary>
    public static bool IsUnchangedArchive(
        CourseState listedState,
        DateTimeOffset? listedUpdateTime,
        DateTimeOffset? completeReadUpdateTime) =>
        listedState == CourseState.Archived
        && listedUpdateTime is { } listed
        && completeReadUpdateTime is { } remembered
        && ToStoragePrecision(listed) == ToStoragePrecision(remembered);
}
