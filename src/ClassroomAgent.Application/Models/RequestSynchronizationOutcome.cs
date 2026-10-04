namespace ClassroomAgent.Application.Models;

/// <summary>
/// What a manual synchronization request answered (US-019 spec FR-001 step 5). The read-only refusal is not an
/// outcome: it is the guard's <see cref="Exceptions.ReadOnlyModeException"/>, which the host maps to <c>409</c>.
/// </summary>
public enum RequestSynchronizationOutcome
{
    /// <summary>Accepted; nothing else was in progress.</summary>
    Requested,

    /// <summary>Accepted and remembered; the run starts after the current synchronization or purge.</summary>
    RequestedAfterCurrentWork,

    /// <summary>Refused: no usable connection (US-019 OD-009 a). Nothing was enqueued.</summary>
    ConnectionNotUsable,
}
