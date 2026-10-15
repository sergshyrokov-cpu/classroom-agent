using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's single salt (US-038 db-design §3.3). Read by the lifting and the Meet load; created lazily by
/// the first erasure inside its transaction with <c>INSERT … ON CONFLICT DO NOTHING</c>, so two concurrent first
/// erasures agree on one salt. The value is never logged.
/// </summary>
public sealed class ErasureSaltStore(ClassroomAgentDbContext db, TimeProvider timeProvider) : IErasureSaltStore
{
    public Task<ErasureSalt?> FindAsync(CancellationToken cancellationToken) =>
        db.ErasureSalts.AsNoTracking().SingleOrDefaultAsync(cancellationToken);

    public async Task<ErasureSalt> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var existing = await FindAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        // Inserted by SQL rather than through the change tracker: a losing concurrent insert must leave nothing staged.
        var candidate = ErasureSalt.Generate();
        var now = timeProvider.GetUtcNow();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO erasure_salt (salt, singleton, created_at, updated_at)
            VALUES ({candidate.Value}, TRUE, {now}, {now})
            ON CONFLICT (singleton) DO NOTHING
            """,
            cancellationToken);

        return await FindAsync(cancellationToken)
            ?? throw new InvalidOperationException("The erasure salt could not be created.");
    }
}
