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

    /// <summary>
    /// Discards every change staged in this unit of work and not committed, whatever entity it belongs to, so a later
    /// save writes none of it (US-053 spec FR-002). The run's <c>SyncState</c> stays tracked, because the failure save
    /// that follows is what records the failed run (US-013 spec FR-006). Not a write: nothing reaches the database.
    /// </summary>
    void DiscardUncommittedChanges();
}
