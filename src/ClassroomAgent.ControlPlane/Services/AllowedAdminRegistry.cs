using ClassroomAgent.ControlPlane.Persistence;
using ClassroomAgent.ControlPlane.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Adding and revoking AllowedAdmin entries (US-003 FR-001 … FR-008). The email reaching it has
/// passed the format rules; the domain match and uniqueness are checked here, and uniqueness is
/// guaranteed by the database. Each change is written with its audit row in one transaction; no
/// refusal is audited. The installation's status is never consulted (AC-006).
/// </summary>
public class AllowedAdminRegistry(ControlPlaneDbContext db, TimeProvider timeProvider)
{
    public Task<AddAllowedAdminFormDto?> GetAddFormAsync(Guid installationIdentifier, CancellationToken cancellationToken) =>
        db.Installations
            .AsNoTracking()
            .Where(i => i.Identifier == installationIdentifier)
            .Select(i => new AddAllowedAdminFormDto(i.Identifier, i.Name, i.Domain))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AddAllowedAdminResult> AddAsync(
        Guid installationIdentifier,
        string email,
        long ownerId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var installation = await db.Installations
            .AsNoTracking()
            .Where(i => i.Identifier == installationIdentifier)
            .Select(i => new { i.Id, i.Domain })
            .SingleOrDefaultAsync(cancellationToken);
        if (installation is null)
        {
            return AddAllowedAdminResult.NotFound;
        }

        var entry = AllowedAdmin.Add(installation.Id, email, ownerId);
        var domainPart = entry.Email[(entry.Email.IndexOf('@', StringComparison.Ordinal) + 1)..];
        if (!string.Equals(domainPart, installation.Domain, StringComparison.Ordinal))
        {
            return AddAllowedAdminResult.WrongDomain(installation.Domain);
        }

        if (await db.AllowedAdmins.AnyAsync(a => a.InstallationId == installation.Id && a.Email == entry.Email, cancellationToken))
        {
            return AddAllowedAdminResult.Taken;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.AllowedAdmins.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
            db.AuditEvents.Add(AuditEvent.AllowedAdminAdded(ownerId, entry.Id, timeProvider.GetUtcNow(), requestId));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateEntry(exception))
        {
            // Lost the race: the entry and its audit row are rolled back together (db-design §5).
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return AddAllowedAdminResult.Taken;
        }

        return AddAllowedAdminResult.Added;
    }

    public async Task<RevokeAllowedAdminConfirmationDto?> GetRevokeConfirmationAsync(
        Guid installationIdentifier,
        Guid adminIdentifier,
        CancellationToken cancellationToken)
    {
        var entry = await db.AllowedAdmins
            .AsNoTracking()
            .Where(a => a.Identifier == adminIdentifier)
            .Join(
                db.Installations.Where(i => i.Identifier == installationIdentifier),
                a => a.InstallationId,
                i => i.Id,
                (a, i) => new { InstallationId = i.Id, InstallationName = i.Name, a.Email })
            .SingleOrDefaultAsync(cancellationToken);
        if (entry is null)
        {
            return null;
        }

        var count = await db.AllowedAdmins.CountAsync(a => a.InstallationId == entry.InstallationId, cancellationToken);
        return new RevokeAllowedAdminConfirmationDto(
            installationIdentifier,
            entry.InstallationName,
            adminIdentifier,
            entry.Email,
            count <= 2);
    }

    public async Task<RevokeAllowedAdminResult> RevokeAsync(
        Guid installationIdentifier,
        Guid adminIdentifier,
        long ownerId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var entryId = await db.AllowedAdmins
            .Where(a => a.Identifier == adminIdentifier
                && db.Installations.Any(i => i.Id == a.InstallationId && i.Identifier == installationIdentifier))
            .Select(a => (long?)a.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (entryId is not { } id)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RevokeAllowedAdminResult.NotFound;
        }

        // A concurrent or repeated revocation deleted it meanwhile: nothing to audit (db-design §5).
        var deleted = await db.AllowedAdmins.Where(a => a.Id == id).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RevokeAllowedAdminResult.NotFound;
        }

        db.AuditEvents.Add(AuditEvent.AllowedAdminRevoked(ownerId, id, timeProvider.GetUtcNow(), requestId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RevokeAllowedAdminResult.Revoked;
    }

    private static bool IsDuplicateEntry(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.ConstraintName == AllowedAdminConfiguration.InstallationEmailUniqueIndex;
}
