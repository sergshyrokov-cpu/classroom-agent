namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Refuses while the installation is in read-only mode (US-007 spec FR-002), determining the mode through
/// <see cref="GetLegitimacyModeQuery"/> at the moment of the call - never from a cached value (AC-008).
/// </summary>
public sealed class ReadOnlyModeGuard(GetLegitimacyModeQuery mode) : IReadOnlyModeGuard
{
    public Task EnsureAllowedAsync(string operation, CancellationToken cancellationToken)
    {
        // Skeleton declared at TEST_WRITING (US-007 OD-003, option 1); IMPLEMENTATION owns this file.
        _ = mode;
        throw new NotImplementedException();
    }
}
