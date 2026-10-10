using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>The stored Meet meetings (US-031 entity model §5, db-design §7).</summary>
public interface IMeetSessionRepository
{
    /// <summary>The sessions of these conference ids, each with all its participations.</summary>
    Task<IReadOnlyList<MeetSession>> GetByConferenceIdsAsync(
        IReadOnlyCollection<string> conferenceIds,
        CancellationToken cancellationToken);

    void Add(MeetSession session);
}
