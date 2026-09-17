using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Whether the installation is in read-only mode, and why (US-005 spec FR-008; BR-025), from the stored
/// <see cref="LegitimacyState"/> and the clock, independent of any UI (AD-6). In order: never confirmed,
/// suspended by the Owner, grace period expired (strictly more than 7 days), otherwise not read-only.
/// </summary>
public sealed class GetLegitimacyModeQuery(ILegitimacyStateRepository states, TimeProvider timeProvider)
{
    public async Task<LegitimacyMode> ExecuteAsync(CancellationToken cancellationToken) =>
        Determine(await states.GetForReadAsync(cancellationToken), timeProvider.GetUtcNow());

    public static LegitimacyMode Determine(LegitimacyState? state, DateTimeOffset now)
    {
        if (state?.LastSuccessfulCheckAt is not { } lastSuccess)
        {
            return new LegitimacyMode(true, LegitimacyModeReason.NotYetConfirmed, null);
        }

        if (state.Status == InstallationStatus.Suspended)
        {
            return new LegitimacyMode(true, LegitimacyModeReason.SuspendedByOwner, lastSuccess);
        }

        return GracePeriod.HasExpired(lastSuccess, now)
            ? new LegitimacyMode(true, LegitimacyModeReason.GracePeriodExpired, lastSuccess)
            : new LegitimacyMode(false, null, lastSuccess);
    }
}
