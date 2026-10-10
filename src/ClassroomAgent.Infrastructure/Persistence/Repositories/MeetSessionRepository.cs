using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The stored Meet meetings with their participations (US-031 entity model §5, db-design §7). Stages changes; never
/// saves (AD-7).
/// </summary>
public sealed class MeetSessionRepository(ClassroomAgentDbContext db) : IMeetSessionRepository
{
    /// <summary>
    /// db-design §7 step 1: the sessions of one page's conference ids, each with <b>all</b> its participations, tracked,
    /// so start and end are recomputed over old and new connections together (AC-002). One query on the unique index.
    /// </summary>
    public async Task<IReadOnlyList<MeetSession>> GetByConferenceIdsAsync(
        IReadOnlyCollection<string> conferenceIds,
        CancellationToken cancellationToken)
    {
        if (conferenceIds.Count == 0)
        {
            return [];
        }

        var ids = conferenceIds.Distinct(StringComparer.Ordinal).ToList();
        return await db.MeetSessions
            .Include(s => s.Participations)
            .Where(s => ids.Contains(s.ConferenceId))
            .ToListAsync(cancellationToken);
    }

    public void Add(MeetSession session) => db.MeetSessions.Add(session);
}
