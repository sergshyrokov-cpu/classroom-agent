using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The installation's single connection record (US-009 entity model §3.4). Stages changes; never saves — the
/// use case owns the transaction boundary through <see cref="IUnitOfWork"/> (AD-7).
/// </summary>
public interface IWorkspaceConnectionRepository
{
    /// <summary>The stored connection, read untracked; null when none is stored (db-design §3.4).</summary>
    Task<WorkspaceConnection?> GetForReadAsync(CancellationToken cancellationToken);

    /// <summary>The stored connection, tracked for a change; null when none is stored.</summary>
    Task<WorkspaceConnection?> GetForUpdateAsync(CancellationToken cancellationToken);

    void Add(WorkspaceConnection connection);
}
