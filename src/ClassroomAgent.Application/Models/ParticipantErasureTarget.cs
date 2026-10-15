namespace ClassroomAgent.Application.Models;

/// <summary>US-038 entity model §7: the participant an erasure acts on, read inside its transaction.</summary>
public sealed record ParticipantErasureTarget(long ParticipantId, string GoogleUserId, string? Email);
