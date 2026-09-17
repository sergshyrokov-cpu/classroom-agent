namespace ClassroomAgent.Application.Ports;

/// <summary>Commits the changes staged by repositories; called only by use cases (AD-7).</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
