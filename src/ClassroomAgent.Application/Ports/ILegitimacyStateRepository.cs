using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>The single <see cref="LegitimacyState"/> row (US-005 db-design §4.2, §4.3). Stages changes; never saves.</summary>
public interface ILegitimacyStateRepository
{
    /// <summary>The row, tracked for an update; null when none exists yet.</summary>
    Task<LegitimacyState?> GetAsync(CancellationToken cancellationToken);

    /// <summary>The row, untracked; null when none exists yet. Throws when the database is unreachable.</summary>
    Task<LegitimacyState?> GetForReadAsync(CancellationToken cancellationToken);

    void Add(LegitimacyState state);
}
