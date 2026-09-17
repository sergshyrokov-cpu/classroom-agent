using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>The single <c>legitimacy_state</c> row (US-005 db-design §4.2, §4.3). Stages changes; never saves (AD-7).</summary>
public sealed class LegitimacyStateRepository(ClassroomAgentDbContext db) : ILegitimacyStateRepository
{
    public Task<LegitimacyState?> GetAsync(CancellationToken cancellationToken) =>
        db.LegitimacyStates.SingleOrDefaultAsync(cancellationToken);

    public Task<LegitimacyState?> GetForReadAsync(CancellationToken cancellationToken) =>
        db.LegitimacyStates.AsNoTracking().SingleOrDefaultAsync(cancellationToken);

    public void Add(LegitimacyState state)
    {
        // Set explicitly: EF Core would send the CLR default false rather than the column default.
        db.LegitimacyStates.Add(state).Property<bool>(LegitimacyStateConfiguration.Singleton).CurrentValue = true;
    }
}
