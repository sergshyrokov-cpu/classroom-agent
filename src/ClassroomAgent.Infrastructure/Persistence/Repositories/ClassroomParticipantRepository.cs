using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's <c>classroom_participant</c> table (US-014 entity model §7). Stages changes; never saves
/// (AD-7).
/// </summary>
public sealed class ClassroomParticipantRepository(ClassroomAgentDbContext db) : IClassroomParticipantRepository
{
    /// <summary>
    /// One query per course rather than one per person (entity model §7), tracked because the upsert updates the
    /// rows it finds.
    /// </summary>
    public async Task<IReadOnlyList<ClassroomParticipant>> GetByGoogleUserIdsAsync(
        IReadOnlyCollection<string> googleUserIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(googleUserIds);
        if (googleUserIds.Count == 0)
        {
            return [];
        }

        return await db.ClassroomParticipants
            .Where(p => googleUserIds.Contains(p.GoogleUserId))
            .ToListAsync(cancellationToken);
    }

    public void Add(ClassroomParticipant participant) => db.ClassroomParticipants.Add(participant);
}
