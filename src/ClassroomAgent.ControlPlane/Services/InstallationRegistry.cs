using ClassroomAgent.ControlPlane.Persistence;
using ClassroomAgent.ControlPlane.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Registration and correction of installations (US-002 FR-001, FR-003 … FR-009). Requests
/// reaching it are already valid. Uniqueness is guaranteed by the database, and a lost race
/// becomes the conflict result, never an exception. Each change is written with its audit
/// row in one transaction; no refusal is audited.
/// </summary>
public class InstallationRegistry(ControlPlaneDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<InstallationListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var installations = await db.Installations
            .AsNoTracking()
            .Select(i => new InstallationListItemDto(i.Identifier, i.Name, i.Domain, i.Status, i.CreatedAt))
            .ToListAsync(cancellationToken);

        // Ordered in memory so the result does not depend on the database collation (I-1, I-2).
        return installations
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Domain, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<InstallationDetailDto?> GetAsync(Guid identifier, CancellationToken cancellationToken)
    {
        var installation = await db.Installations
            .AsNoTracking()
            .Where(i => i.Identifier == identifier)
            .Select(i => new { i.Id, i.Identifier, i.Name, i.Domain, i.Status, i.CreatedAt, i.ClientId, i.PushAddress })
            .SingleOrDefaultAsync(cancellationToken);
        if (installation is null)
        {
            return null;
        }

        var admins = await db.AllowedAdmins
            .AsNoTracking()
            .Where(a => a.InstallationId == installation.Id)
            .Select(a => new AllowedAdminItemDto(a.Identifier, a.Email, a.CreatedAt))
            .ToListAsync(cancellationToken);

        var lastCheck = await db.InstanceLicenseChecks
            .AsNoTracking()
            .Where(c => c.InstallationId == installation.Id)
            .Select(c => new InstallationLastCheckDto(c.AnsweredAt, c.ApplicationVersion, c.ContractVersion, c.AnsweredStatus, c.AnsweredCompatibility))
            .SingleOrDefaultAsync(cancellationToken);

        // Entries of this installation only, ordered by email in memory, independent of the collation (US-003 I-4).
        return new InstallationDetailDto(
            installation.Identifier,
            installation.Name,
            installation.Domain,
            installation.Status,
            installation.CreatedAt,
            installation.ClientId,
            installation.PushAddress,
            admins.OrderBy(a => a.Email, StringComparer.Ordinal).ToList(),
            lastCheck);
    }

    public async Task<RegisterInstallationResult> RegisterAsync(
        string name,
        string domain,
        string clientId,
        string? pushAddress,
        long ownerId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var installation = Installation.Register(name, domain, clientId, pushAddress);

        var (domainTaken, clientIdTaken) = await TakenAsync(installation.Domain, installation.ClientId, cancellationToken);
        if (domainTaken || clientIdTaken)
        {
            return RegisterInstallationResult.Conflict(domainTaken, clientIdTaken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.Installations.Add(installation);
            await db.SaveChangesAsync(cancellationToken);
            db.AuditEvents.Add(AuditEvent.InstallationCreated(ownerId, installation.Id, timeProvider.GetUtcNow(), requestId));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ViolatedUniqueIndex(exception) is { } index)
        {
            // Lost the race: nothing of this transaction remains; report every field now taken (db-design §5).
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            (domainTaken, clientIdTaken) = await TakenAsync(installation.Domain, installation.ClientId, cancellationToken);
            return domainTaken || clientIdTaken
                ? RegisterInstallationResult.Conflict(domainTaken, clientIdTaken)
                : RegisterInstallationResult.Conflict(
                    index == InstallationConfiguration.DomainUniqueIndex,
                    index == InstallationConfiguration.ClientIdUniqueIndex);
        }

        return RegisterInstallationResult.Registered(installation.Identifier);
    }

    public async Task<RenameInstallationResult> RenameAsync(
        Guid identifier,
        string name,
        long ownerId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var installation = await db.Installations.SingleOrDefaultAsync(i => i.Identifier == identifier, cancellationToken);
        if (installation is null)
        {
            return RenameInstallationResult.NotFound;
        }

        if (string.Equals(installation.Name, name, StringComparison.Ordinal))
        {
            return RenameInstallationResult.Unchanged;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        installation.Rename(name);
        db.AuditEvents.Add(AuditEvent.InstallationRenamed(ownerId, installation.Id, timeProvider.GetUtcNow(), requestId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RenameInstallationResult.Renamed;
    }

    public async Task<ChangeClientIdResult> ChangeClientIdAsync(
        Guid identifier,
        string clientId,
        long ownerId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var installation = await db.Installations.SingleOrDefaultAsync(i => i.Identifier == identifier, cancellationToken);
        if (installation is null)
        {
            return ChangeClientIdResult.NotFound;
        }

        if (string.Equals(installation.ClientId, clientId, StringComparison.Ordinal))
        {
            return ChangeClientIdResult.Unchanged;
        }

        if (await db.Installations.AnyAsync(i => i.ClientId == clientId, cancellationToken))
        {
            return ChangeClientIdResult.ClientIdTaken;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            installation.ChangeClientId(clientId);
            db.AuditEvents.Add(AuditEvent.InstallationClientIdChanged(ownerId, installation.Id, timeProvider.GetUtcNow(), requestId));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ViolatedUniqueIndex(exception) == InstallationConfiguration.ClientIdUniqueIndex)
        {
            // Lost the race: the change and its audit row are rolled back together.
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return ChangeClientIdResult.ClientIdTaken;
        }

        return ChangeClientIdResult.Changed;
    }

    /// <summary>
    /// Sets, changes or clears the push address (US-006 spec FR-002, FR-003; db-design §4.2). The value is
    /// already canonical, or null for "not set"; an unchanged submission writes nothing and audits nothing.
    /// </summary>
    public async Task<ChangePushAddressResult> ChangePushAddressAsync(
        Guid identifier,
        string? pushAddress,
        long ownerId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var installation = await db.Installations.SingleOrDefaultAsync(i => i.Identifier == identifier, cancellationToken);
        if (installation is null)
        {
            return ChangePushAddressResult.NotFound;
        }

        if (string.Equals(installation.PushAddress, pushAddress, StringComparison.Ordinal))
        {
            return ChangePushAddressResult.Unchanged;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        installation.ChangePushAddress(pushAddress);
        db.AuditEvents.Add(AuditEvent.InstallationPushAddressChanged(ownerId, installation.Id, timeProvider.GetUtcNow(), requestId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ChangePushAddressResult.Changed;
    }

    private async Task<(bool DomainTaken, bool ClientIdTaken)> TakenAsync(
        string domain,
        string clientId,
        CancellationToken cancellationToken)
    {
        var domainTaken = await db.Installations.AnyAsync(i => i.Domain == domain, cancellationToken);
        var clientIdTaken = await db.Installations.AnyAsync(i => i.ClientId == clientId, cancellationToken);
        return (domainTaken, clientIdTaken);
    }

    private static string? ViolatedUniqueIndex(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.ConstraintName is InstallationConfiguration.DomainUniqueIndex or InstallationConfiguration.ClientIdUniqueIndex
            ? postgres.ConstraintName
            : null;
}
