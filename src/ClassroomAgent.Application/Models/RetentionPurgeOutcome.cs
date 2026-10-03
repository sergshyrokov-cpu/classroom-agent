using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// What a purge run did, for the host to log (US-037 spec FR-012; OD-007 — the Application layer has no logger): the
/// cutoff, the committed counts and the units that failed.
/// </summary>
public sealed record RetentionPurgeOutcome(
    DateTimeOffset Cutoff,
    RetentionPurgeCounts Counts,
    IReadOnlyList<RetentionPurgeFailure> Failures);
