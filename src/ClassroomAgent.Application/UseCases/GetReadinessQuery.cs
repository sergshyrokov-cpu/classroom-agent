using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Readiness of the installation (US-005 spec FR-013; DC-11): <c>Unhealthy</c> when the database cannot be
/// read; <c>Degraded</c> in read-only mode or when the last check since startup was unsuccessful; otherwise
/// <c>Healthy</c>. Before the first check since startup completes, only the stored state decides.
/// </summary>
public sealed class GetReadinessQuery(ILegitimacyStateRepository states, LegitimacyCheckMemory memory, TimeProvider timeProvider)
{
    public async Task<ReadinessState> ExecuteAsync(CancellationToken cancellationToken)
    {
        LegitimacyMode mode;
        try
        {
            // Reading the row is also the database probe.
            mode = GetLegitimacyModeQuery.Determine(await states.GetForReadAsync(cancellationToken), timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return ReadinessState.Unhealthy;
        }

        return mode.IsReadOnly || memory.LastOutcome is { Succeeded: false }
            ? ReadinessState.Degraded
            : ReadinessState.Healthy;
    }
}
