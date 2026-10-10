using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// The "Last synchronization" block of the connection page (US-017 spec FR-007, API design §3). UTC instants; the view
/// formats them in the request culture. <see cref="Diagnosis"/> is set only for <see cref="LastSynchronizationStatus.Failed"/>.
/// </summary>
/// <remarks>
/// US-031 (openapi <c>LastSynchronizationView</c>): <see cref="MeetLoadedUpTo"/> is the Meet watermark as a local
/// date-time in the school's time zone (null = not loaded yet), <see cref="FailedStep"/> the step that stopped a failed
/// run (null otherwise and for pre-Story rows).
/// </remarks>
public sealed record LastSynchronizationView(
    LastSynchronizationStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? LastSuccessfulRunAt,
    SyncDiagnosis? Diagnosis,
    DateTime? MeetLoadedUpTo = null,
    SyncStep? FailedStep = null);
