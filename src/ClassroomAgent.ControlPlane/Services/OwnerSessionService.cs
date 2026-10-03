using ClassroomAgent.ControlPlane.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Session-side reads and writes of the Owner account: whether it exists (setup gate),
/// whether a session's security stamp is still current, ending a session (FR-001,
/// FR-011), and the Owner's own choice of UI language (US-039 spec FR-004).
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

    /// <summary>
    /// Stores the Owner's UI language and returns the accepted code, or null when the code is not exactly
    /// <c>uk</c> or <c>en</c> (US-039 VR-001: compared as a string, never parsed as an enum) or the Owner does not
    /// exist. Not a credential change: the security stamp is not rotated (I-3); no audit row (OD-005).
    /// </summary>
    public async Task<string?> ChooseLanguageAsync(long ownerId, string? languageCode, CancellationToken cancellationToken)
    {
        UiLanguage? language = languageCode switch
        {
            "uk" => UiLanguage.Uk,
            "en" => UiLanguage.En,
            _ => null,
        };
        if (language is not { } chosen)
        {
            return null;
        }

        var owner = await db.Owners.SingleOrDefaultAsync(o => o.Id == ownerId, cancellationToken);
        if (owner is null)
        {
            return null;
        }

        owner.UiLanguage = chosen;
        owner.ConcurrencyStamp = OwnerStamps.New();
        await db.SaveChangesAsync(cancellationToken);
        return languageCode;
    }

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
