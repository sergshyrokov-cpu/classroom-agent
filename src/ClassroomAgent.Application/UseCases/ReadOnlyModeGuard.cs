using ClassroomAgent.Application.Exceptions;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Refuses while the installation is in read-only mode (US-007 spec FR-002), determining the mode through
/// <see cref="GetLegitimacyModeQuery"/> at the moment of the call - never from a cached value (AC-008).
/// It throws before the caller has reached a repository, a port or a transaction (AC-001, AC-006).
/// </summary>
public sealed class ReadOnlyModeGuard(GetLegitimacyModeQuery mode) : IReadOnlyModeGuard
{
    public async Task EnsureAllowedAsync(string operation, CancellationToken cancellationToken)
    {
        // The name is a constant of the calling use case, never data (spec VR-001).
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var current = await mode.ExecuteAsync(cancellationToken);
        if (current is { IsReadOnly: true, Reason: { } reason })
        {
            throw new ReadOnlyModeException(reason, current.LastSuccessfulCheckAt, operation);
        }
    }
}
