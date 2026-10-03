namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// The single definition of "older than the retention period N" (US-037 spec FR-001, FR-002; <c>trebovaniya.md</c>
/// §5 v36), shared by synchronization's refusal of an unimported course and by the retention purge.
/// </summary>
public static class RetentionRule
{
    /// <summary>The run's one cutoff: <paramref name="now"/> minus <paramref name="years"/> calendar years.</summary>
    public static DateTimeOffset Cutoff(DateTimeOffset now, int years) => now.AddYears(-years);

    /// <summary>Expired means strictly earlier than the cutoff (US-015 I-3): the boundary itself is kept.</summary>
    public static bool IsExpired(DateTimeOffset date, DateTimeOffset cutoff) => date < cutoff;

    /// <summary>The latest of the dates, nulls ignored; null when every date is null.</summary>
    public static DateTimeOffset? LatestActivity(IEnumerable<DateTimeOffset?> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        DateTimeOffset? latest = null;
        foreach (var date in dates)
        {
            if (date is { } value && (latest is null || value > latest))
            {
                latest = value;
            }
        }

        return latest;
    }
}
