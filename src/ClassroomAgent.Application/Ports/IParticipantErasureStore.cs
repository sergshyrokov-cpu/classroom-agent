using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The erasure's set-based deletes and clearings (US-038 db-design §7.1, entity model §7). Runs inside the caller's
/// transaction, child-first; never opens one itself (AD-7).
/// </summary>
public interface IParticipantErasureStore
{
    /// <summary>
    /// Deletes the participant's Meet participations (by <paramref name="normalizedEmail"/>, none when it is null),
    /// submissions, memberships and row; clears the organizer email of their meetings and the owner of their courses.
    /// Returns null when the participant row no longer exists (the caller rolls back and answers 404).
    /// </summary>
    Task<ErasureCounts?> EraseAsync(
        long participantId,
        string googleUserId,
        string? normalizedEmail,
        CancellationToken cancellationToken);
}
