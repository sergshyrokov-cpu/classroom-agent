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

    public Task<InstallationDetailDto?> GetAsync(Guid identifier, CancellationToken cancellationToken) =>
        db.Installations
            .AsNoTracking()
            .Where(i => i.Identifier == identifier)
            .Select(i => new InstallationDetailDto(i.Identifier, i.Name, i.Domain, i.Status, i.CreatedAt, i.ClientId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<RegisterInstallationResult> RegisterAsync(
        string name,
        string domain,
        string clientId,
        long ownerId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var installation = Installation.Register(name, domain, clientId);

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
