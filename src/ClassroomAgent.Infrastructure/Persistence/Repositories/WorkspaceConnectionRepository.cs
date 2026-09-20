using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's single connection record (US-009 entity model §3.4). Stages changes only; the use case
/// commits through <see cref="IUnitOfWork"/> (AD-7, package-map).
/// </summary>
public sealed class WorkspaceConnectionRepository(ClassroomAgentDbContext db) : IWorkspaceConnectionRepository
{
    /// <summary>Untracked: the read path never writes, and a tracked entity could be flushed by a later commit
    /// in the same request — the defect US-008 security review F-4 named (db-design §3.4).</summary>
    public Task<WorkspaceConnection?> GetForReadAsync(CancellationToken cancellationToken) =>
        db.WorkspaceConnections.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

    /// <summary>Tracked, because the save changes the row it finds.</summary>
    public Task<WorkspaceConnection?> GetForUpdateAsync(CancellationToken cancellationToken) =>
        db.WorkspaceConnections.FirstOrDefaultAsync(cancellationToken);

    public void Add(WorkspaceConnection connection)
    {
        // Set explicitly, as US-005 does for legitimacy_state: EF Core would send the CLR default false rather
        // than the column default, and ck_workspace_connection_singleton would reject the row.
        db.WorkspaceConnections.Add(connection)
            .Property<bool>(WorkspaceConnectionConfiguration.Singleton).CurrentValue = true;
    }
}
