using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The installation's single <c>sync_state</c> row (US-013 entity model §1; db-design §3). Stages changes; never
/// saves — the use case owns the transaction boundary through <see cref="IUnitOfWork"/> (AD-7).
/// </summary>
/// <remarks>
/// Compile-only skeleton created at TEST_WRITING under US-013 OD-008; IMPLEMENTATION owns it from here.
/// </remarks>
public interface ISyncStateRepository
{
    /// <summary>The stored row, tracked for a change; null when the installation has never synchronized.</summary>
    Task<SyncState?> GetAsync(CancellationToken cancellationToken);

    /// <summary>The stored row, read untracked; null when the installation has never synchronized.</summary>
    Task<SyncState?> GetForReadAsync(CancellationToken cancellationToken);

    void Add(SyncState state);
}
