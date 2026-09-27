using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The installation's <c>classroom_participant</c> table (US-014 entity model §7). Stages changes; never saves —
/// the use case owns the transaction boundary through <see cref="IUnitOfWork"/> (AD-7).
/// </summary>
/// <remarks>
/// Compile-only skeleton created at TEST_WRITING under US-014 OD-012; IMPLEMENTATION owns it from here.
/// </remarks>
public interface IClassroomParticipantRepository
{
    /// <summary>
    /// The stored participants whose <c>google_user_id</c> is one of <paramref name="googleUserIds"/> — one
    /// query per course rather than one per person.
    /// </summary>
    Task<IReadOnlyList<ClassroomParticipant>> GetByGoogleUserIdsAsync(
        IReadOnlyCollection<string> googleUserIds,
        CancellationToken cancellationToken);

    void Add(ClassroomParticipant participant);
}
