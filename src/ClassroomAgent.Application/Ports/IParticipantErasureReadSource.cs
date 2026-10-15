using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>The reads of the erasure pages and of the erasure itself (US-038 entity model §7, db-design §10).</summary>
public interface IParticipantErasureReadSource
{
    /// <summary>
    /// Participants whose email, full name, surname or given name contains <paramref name="term"/> case-insensitively,
    /// the term taken literally; ordered by full name, email, id; at most <paramref name="limit"/> rows.
    /// </summary>
    Task<ParticipantSearchPage> SearchAsync(string term, int limit, CancellationToken cancellationToken);

    /// <summary>What the confirmation page shows, or null when no participant has that id.</summary>
    Task<ErasureConfirmationData?> GetConfirmationAsync(long participantId, CancellationToken cancellationToken);

    /// <summary>The id, Google user id and email of the participant, or null when no participant has that id.</summary>
    Task<ParticipantErasureTarget?> GetTargetAsync(long participantId, CancellationToken cancellationToken);
}
