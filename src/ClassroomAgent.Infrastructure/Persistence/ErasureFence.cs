using ClassroomAgent.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// US-038 db-design §7.3: the write fence as one transaction-scoped PostgreSQL advisory lock — exclusive for an
/// erasure, shared for each synchronization course and Meet page transaction. Released by commit or rollback, so
/// nothing can leak; outside a transaction it would be released at once, so it refuses to run there.
/// </summary>
public sealed class ErasureFence(ClassroomAgentDbContext db) : IErasureFence
{
    /// <summary>db-design §7.3: the fixed advisory-lock key ("ERASE" in ASCII).</summary>
    public const long ErasureFenceKey = 0x4552415345L;

    public Task EnterAsWriterAsync(CancellationToken cancellationToken) =>
        EnterAsync("SELECT pg_advisory_xact_lock_shared({0})", cancellationToken);

    public Task EnterAsErasureAsync(CancellationToken cancellationToken) =>
        EnterAsync("SELECT pg_advisory_xact_lock({0})", cancellationToken);

    private async Task EnterAsync(string sql, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The erasure fence is entered only inside an open transaction.");
        }

        await db.Database.ExecuteSqlRawAsync(sql, [ErasureFenceKey], cancellationToken);
    }
}
