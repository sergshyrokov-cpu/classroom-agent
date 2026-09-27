using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>The single <c>sync_state</c> row (US-013 db-design §3). Stages changes; never saves (AD-7).</summary>
public sealed class SyncStateRepository(ClassroomAgentDbContext db) : ISyncStateRepository
{
    public Task<SyncState?> GetAsync(CancellationToken cancellationToken) =>
        db.SyncStates.SingleOrDefaultAsync(cancellationToken);

    public Task<SyncState?> GetForReadAsync(CancellationToken cancellationToken) =>
        db.SyncStates.AsNoTracking().SingleOrDefaultAsync(cancellationToken);

    public void Add(SyncState state)
    {
        // Set explicitly: EF Core would send the CLR default false rather than the column default.
        db.SyncStates.Add(state).Property<bool>(SyncStateConfiguration.Singleton).CurrentValue = true;
    }
}
