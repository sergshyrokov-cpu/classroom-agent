using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>The installation's single salt (US-038 db-design §3.3, entity model §7).</summary>
public interface IErasureSaltStore
{
    /// <summary>The salt, or null while no erasure has ever run — then no fingerprinted exclusion can exist.</summary>
    Task<ErasureSalt?> FindAsync(CancellationToken cancellationToken);

    /// <summary>The salt, created inside the caller's transaction by the first erasure (insert on conflict do nothing).</summary>
    Task<ErasureSalt> GetOrCreateAsync(CancellationToken cancellationToken);
}
