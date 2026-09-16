using ClassroomAgent.ControlPlane.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Session-side reads and writes of the Owner account: whether it exists (setup gate),
/// whether a session's security stamp is still current, and ending a session (FR-001,
/// FR-011).
/// </summary>
public class OwnerSessionService(ControlPlaneDbContext db, OwnerExistenceCache existence)
{
    public async Task<bool> OwnerExistsAsync(CancellationToken cancellationToken)
    {
        if (existence.Exists)
        {
            return true;
        }

        var exists = await db.Owners.AnyAsync(cancellationToken);
        if (exists)
        {
            existence.MarkExists();
        }

        return exists;
    }

    public Task<bool> IsSessionCurrentAsync(long ownerId, string securityStamp, CancellationToken cancellationToken) =>
        db.Owners.AnyAsync(o => o.Id == ownerId && o.SecurityStamp == securityStamp, cancellationToken);

    /// <summary>Rotates the security stamp, so every cookie issued before stops authenticating.</summary>
    public async Task EndSessionAsync(long ownerId, CancellationToken cancellationToken)
    {
        var owner = await db.Owners.SingleOrDefaultAsync(o => o.Id == ownerId, cancellationToken);
        if (owner is null)
        {
            return;
        }

        owner.SecurityStamp = OwnerStamps.New();
        owner.ConcurrencyStamp = OwnerStamps.New();
        await db.SaveChangesAsync(cancellationToken);
    }
}
