using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>Commits what the repositories staged in this scope's <see cref="ClassroomAgentDbContext"/> (AD-7).</summary>
public sealed class UnitOfWork(ClassroomAgentDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
