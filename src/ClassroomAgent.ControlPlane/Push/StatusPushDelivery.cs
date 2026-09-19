namespace ClassroomAgent.ControlPlane.Push;

/// <summary>
/// The fixed delivery parameters of a status-change push (US-006 spec FR-006, I-7, I-8; api-design §6, §8;
/// NFR-014). They are constants, not configuration: the Control Plane gets no new setting.
/// </summary>
public static class StatusPushDelivery
{
    /// <summary>One attempt waits at most this long for a response status, connecting included (spec I-7).</summary>
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The pauses before attempts 2, 3 and 4, each counted from the end of the failed attempt (spec I-8).</summary>
    public static readonly IReadOnlyList<TimeSpan> RetryPauses =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
    ];

    /// <summary>At most four attempts in total: the first plus the three retries.</summary>
    public static int MaximumAttempts => RetryPauses.Count + 1;
}
