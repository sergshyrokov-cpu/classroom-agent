namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The single enforcement point of read-only mode (US-007 spec FR-002; AD-6, SC-5). Every write use case
/// and every use case that would reach a port marked <c>IGoogleDataPort</c> calls it as its first
/// statement, before any repository, port or transaction.
/// </summary>
public interface IReadOnlyModeGuard
{
    /// <summary>
    /// Returns when the installation may act; throws <see cref="Exceptions.ReadOnlyModeException"/> when it is
    /// in read-only mode. <paramref name="operation"/> is a compile-time constant of the calling use case
    /// and never carries data (spec VR-001).
    /// </summary>
    Task EnsureAllowedAsync(string operation, CancellationToken cancellationToken);
}
