using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.Models;

/// <summary>US-038 spec FR-004: the outcome and, for an erasure, the counts committed (the host logs them, FR-013).</summary>
public sealed record ParticipantErasureResult(ParticipantErasureOutcome Outcome, ErasureCounts? Counts);
