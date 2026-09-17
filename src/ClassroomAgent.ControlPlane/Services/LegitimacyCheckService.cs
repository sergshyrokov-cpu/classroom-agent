using ClassroomAgent.ControlPlane.Persistence;
using ClassroomAgent.ControlPlane.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// The Control Plane side of the legitimacy check (US-005 spec FR-004 … FR-006, FR-012): finds the
/// installation, decides compatibility, replaces its one <see cref="InstanceLicenseCheck"/> and answers.
/// Requests reaching it are already valid. Nothing else changes and nothing is audited (S-14). Log lines
/// carry the installation id, versions, status and compatibility — never domain or client ID (S-08).
/// </summary>
public partial class LegitimacyCheckService(
    ControlPlaneDbContext db,
    CompatibilityPolicy compatibilityPolicy,
    TimeProvider timeProvider,
    ILogger<LegitimacyCheckService> logger)
{
    public async Task<LegitimacyCheckResult> CheckAsync(
        Guid installationIdentifier,
        InstallationVersion applicationVersion,
        int contractVersion,
        CancellationToken cancellationToken)
    {
        var installation = await db.Installations
            .AsNoTracking()
            .Where(i => i.Identifier == installationIdentifier)
            .Select(i => new { i.Id, i.Status, i.Domain, i.ClientId })
            .SingleOrDefaultAsync(cancellationToken);
        if (installation is null)
        {
            LogUnknownInstallation(logger, installationIdentifier);
            return LegitimacyCheckResult.UnknownInstallation;
        }

        var compatibility = compatibilityPolicy.Decide(applicationVersion, contractVersion);
        var versionText = applicationVersion.ToText();
        await ReplaceLastCheckAsync(installation.Id, versionText, contractVersion, installation.Status, compatibility, cancellationToken);

        LogAnswered(logger, installationIdentifier, versionText, contractVersion, installation.Status, compatibility);
        return new LegitimacyCheckResult.Known(installation.Status, compatibility, installation.Domain, installation.ClientId);
    }

    /// <summary>
    /// One record per installation, last write wins (db-design §3.2): a first call that loses the race to
    /// insert updates the row the other call inserted, once; any other failure propagates.
    /// </summary>
    private async Task ReplaceLastCheckAsync(
        long installationId,
        string applicationVersion,
        int contractVersion,
        InstallationStatus status,
        CompatibilityState compatibility,
        CancellationToken cancellationToken)
    {
        var answeredAt = timeProvider.GetUtcNow();
        var existing = await db.InstanceLicenseChecks.SingleOrDefaultAsync(c => c.InstallationId == installationId, cancellationToken);
        if (existing is null)
        {
            var added = InstanceLicenseCheck.Record(installationId, answeredAt, applicationVersion, contractVersion, status, compatibility);
            db.InstanceLicenseChecks.Add(added);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException exception) when (IsLostFirstCallRace(exception))
            {
                db.Entry(added).State = EntityState.Detached;
                existing = await db.InstanceLicenseChecks.SingleAsync(c => c.InstallationId == installationId, cancellationToken);
            }
        }

        existing.Replace(answeredAt, applicationVersion, contractVersion, status, compatibility);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsLostFirstCallRace(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.ConstraintName == InstanceLicenseCheckConfiguration.InstallationUniqueIndex;

    [LoggerMessage(
        EventId = 5001,
        EventName = "LegitimacyCheckAnswered",
        Level = LogLevel.Information,
        Message = "Legitimacy check of installation {InstallationId} answered: application version {ApplicationVersion}, contract version {ContractVersion}, status {Status}, compatibility {Compatibility}")]
    private static partial void LogAnswered(
        ILogger logger,
        Guid installationId,
        string applicationVersion,
        int contractVersion,
        InstallationStatus status,
        CompatibilityState compatibility);

    [LoggerMessage(
        EventId = 5002,
        EventName = "LegitimacyCheckUnknownInstallation",
        Level = LogLevel.Warning,
        Message = "Legitimacy check for unknown installation {InstallationId}")]
    private static partial void LogUnknownInstallation(ILogger logger, Guid installationId);
}
