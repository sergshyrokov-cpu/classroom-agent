using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Readiness of the installation (US-005 spec FR-013, US-013 spec FR-014; DC-11): <c>Unhealthy</c> when the
/// database cannot be read or the synchronization background service is not running; <c>Degraded</c> in read-only
/// mode or when the last check since startup was unsuccessful; otherwise <c>Healthy</c>. Before the first check
/// since startup completes, only the stored state decides.
/// </summary>
/// <remarks>
/// The precedence is explicit and matters: read-only mode stays <c>Degraded</c> with HTTP 200, because it is a
/// defined operating state in which users keep viewing and exporting (DC-7, DC-11). Only an installation that
/// cannot serve users at all is <c>Unhealthy</c> — and a synchronization service that is not running is exactly
/// that (US-013 spec FR-014).
/// </remarks>
public sealed class GetReadinessQuery(
    ILegitimacyStateRepository states,
    LegitimacyCheckMemory memory,
    SynchronizationServiceMemory synchronization,
    TimeProvider timeProvider)
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

        if (!synchronization.IsRunning)
        {
            return ReadinessState.Unhealthy;
        }

        return mode.IsReadOnly || memory.LastOutcome is { Succeeded: false }
            ? ReadinessState.Degraded
            : ReadinessState.Healthy;
    }
}
