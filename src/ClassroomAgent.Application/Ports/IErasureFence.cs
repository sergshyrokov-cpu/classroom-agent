namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The write fence between an erasure and the writers of synchronization and the Meet load (US-038 db-design §7.3,
/// entity model §7; spec FR-011). Must be entered inside an open transaction; released at commit or rollback.
/// </summary>
public interface IErasureFence
{
    /// <summary>Shared: taken first by each synchronization course transaction and each Meet page transaction.</summary>
    Task EnterAsWriterAsync(CancellationToken cancellationToken);

    /// <summary>Exclusive: taken first by the erasure transaction.</summary>
    Task EnterAsErasureAsync(CancellationToken cancellationToken);
}
