using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// The "Last synchronization" block of the connection page (US-017 spec FR-007, API design §3). UTC instants; the view
/// formats them in the request culture. <see cref="Diagnosis"/> is set only for <see cref="LastSynchronizationStatus.Failed"/>.
/// </summary>
public sealed record LastSynchronizationView(
    LastSynchronizationStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? LastSuccessfulRunAt,
    SyncDiagnosis? Diagnosis);
