namespace ClassroomAgent.Application.Ports;

/// <summary>Commits the changes staged by repositories; called only by use cases (AD-7).</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="work"/> inside one database transaction, so several commits land together or not at
    /// all (US-008 db-design §4.4: a successful sign-in must not leave an account without its audit row, and the
    /// row needs the identity the insert generates). The use case still owns the boundary (AD-7).
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}
