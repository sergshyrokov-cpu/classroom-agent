using ClassroomAgent.ControlPlane.Persistence;
using ClassroomAgent.ControlPlane.Push;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Suspending and resuming an installation (US-004 FR-002 … FR-006). The status is changed by one
/// conditional update on the stored value, so concurrent or repeated submissions never produce a
/// change without its audit row or an audit row without its change (db-design §5). When the status
/// really changed, the push address stored at that moment is read inside the same transaction and the
/// push is handed to the background sender after the commit (US-006 spec FR-005, I-6; db-design §4.3).
/// </summary>
public partial class InstallationStatusService(
    ControlPlaneDbContext db,
    TimeProvider timeProvider,
    StatusPushDispatcher pushes,
    ILogger<InstallationStatusService> logger)
{
    /// <summary>The status the transition sets.</summary>
    public static InstallationStatus TargetOf(InstallationStatusTransition transition) =>
        transition == InstallationStatusTransition.Suspend ? InstallationStatus.Suspended : InstallationStatus.Active;

    public Task<InstallationStatusConfirmationDto?> GetConfirmationAsync(
        Guid installationIdentifier,
        CancellationToken cancellationToken) =>
        db.Installations
            .AsNoTracking()
            .Where(i => i.Identifier == installationIdentifier)
            .Select(i => new InstallationStatusConfirmationDto(i.Identifier, i.Name, i.Domain, i.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<InstallationStatusChangeResult> ChangeStatusAsync(
        Guid installationIdentifier,
        InstallationStatusTransition transition,
        long ownerId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var installationId = await db.Installations
            .AsNoTracking()
            .Where(i => i.Identifier == installationIdentifier)
            .Select(i => (long?)i.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (installationId is not { } id)
        {
            return InstallationStatusChangeResult.NotFound;
        }

        var target = TargetOf(transition);
        var expected = target == InstallationStatus.Suspended ? InstallationStatus.Active : InstallationStatus.Suspended;
        var now = timeProvider.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Set-based, so the timestamp interceptor does not see it: updated_at is set here (PC-6).
        var changed = await db.Installations
            .Where(i => i.Id == id && i.Status == expected)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(i => i.Status, target)
                    .SetProperty(i => i.UpdatedAt, now),
                cancellationToken);
        if (changed == 0)
        {
            // Installations are never deleted, so no row means the status already is the target.
            await transaction.RollbackAsync(cancellationToken);
            return InstallationStatusChangeResult.Unchanged;
        }

        // Read under the lock the update just took, so the address is the one stored as the change commits.
        var pushAddress = await db.Installations
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => i.PushAddress)
            .SingleAsync(cancellationToken);

        db.AuditEvents.Add(transition == InstallationStatusTransition.Suspend
            ? AuditEvent.InstallationSuspended(ownerId, id, now, requestId)
            : AuditEvent.InstallationResumed(ownerId, id, now, requestId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        HandOff(id, installationIdentifier, pushAddress);
        return InstallationStatusChangeResult.Changed;
    }

    /// <summary>
    /// Hands the push to the sender after the commit. It never throws to the caller: the status change is
    /// already committed and the periodic check delivers the status anyway (spec FR-005; api-design §6).
    /// </summary>
    private void HandOff(long installationId, Guid identifier, string? pushAddress)
    {
        if (pushAddress is null)
        {
            LogNoAddress(logger, installationId);
            return;
        }

        try
        {
            pushes.Enqueue(installationId, identifier, pushAddress);
        }
        catch (Exception exception)
        {
            // Only the type: a message may carry the address (SC-10).
            LogHandOffFailed(logger, installationId, exception.GetType().Name);
        }
    }

    [LoggerMessage(
        EventId = 5015,
        EventName = "StatusPushNoAddress",
        Level = LogLevel.Warning,
        Message = "Installation {InstallationId} changed status without a push address; the periodic check delivers it")]
    private static partial void LogNoAddress(ILogger logger, long installationId);

    [LoggerMessage(
        EventId = 5016,
        EventName = "StatusPushHandOffFailed",
        Level = LogLevel.Error,
        Message = "Handing the status push for installation {InstallationId} to the sender failed with {ExceptionType}")]
    private static partial void LogHandOffFailed(ILogger logger, long installationId, string exceptionType);
}
