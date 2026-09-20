using ClassroomAgent.ControlPlane.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// The Owner's first point of control, answered on every Admin sign-in at a school (US-008 spec FR-009;
/// <c>trebovaniya.md</c> §9). It reads <c>AllowedAdmin</c> and <b>writes nothing</b>: no audit row, no
/// <c>InstanceLicenseCheck</c>, no last-seen stamp — the Control Plane audits Owner actions, and a school's
/// sign-in is the school's audit (§5, S-17).
/// </summary>
/// <remarks>
/// The rule lives here and is the only caller of <c>Persistence</c>; the controller holds no rule and never
/// touches <c>DbContext</c> (AD-3). The comparison is on the stored lower-cased email of <b>that</b>
/// installation, so an entry of another school never matches (SC-3, BR-079).
/// </remarks>
public class AdminLoginCheckService(ControlPlaneDbContext db, ILogger<AdminLoginCheckService> logger)
{
    public async Task<AdminLoginCheckResult> CheckAsync(
        Guid installationIdentifier,
        string email,
        CancellationToken cancellationToken)
    {
        var installationId = await db.Installations
            .AsNoTracking()
            .Where(i => i.Identifier == installationIdentifier)
            .Select(i => (long?)i.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (installationId is null)
        {
            // The received id only: nothing about any email (SC-10).
            logger.LogWarning(
                new EventId(2101, "AdminLoginCheckUnknownInstallation"),
                "Admin login check for an unknown installation {InstallationId}",
                installationIdentifier);
            return AdminLoginCheckResult.UnknownInstallation;
        }

        // The status plays no part: a suspended school is answered exactly like an active one (AC-005).
        var normalized = email.Trim().ToLowerInvariant();
        var allowed = await db.AllowedAdmins
            .AsNoTracking()
            .AnyAsync(a => a.InstallationId == installationId.Value && a.Email == normalized, cancellationToken);

        logger.LogInformation(
            new EventId(2100, "AdminLoginCheckAnswered"),
            "Admin login check for installation {InstallationId} answered {Allowed}",
            installationIdentifier,
            allowed);
        return allowed ? AdminLoginCheckResult.Allowed : AdminLoginCheckResult.NotAllowed;
    }
}
