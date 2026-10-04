namespace ClassroomAgent.Application.Models;

/// <summary>
/// US-019 spec FR-002, FR-003: whether other work — a synchronization run or a retention purge — was in progress at
/// the moment a run was requested, read together with the request (spec I-1).
/// </summary>
public enum SynchronizationRequestTiming
{
    /// <summary>Nothing was in progress; the requested run starts at once.</summary>
    StartsNow,

    /// <summary>A run or a purge was in progress; the request is remembered and starts when that work ends.</summary>
    AfterCurrentWork,
}
