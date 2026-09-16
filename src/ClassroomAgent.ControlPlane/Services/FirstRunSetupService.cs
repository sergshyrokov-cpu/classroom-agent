using ClassroomAgent.ControlPlane.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Creates the single Owner account with the one-time setup code (FR-004 steps 3–5,
/// FR-005, FR-006). The request fields are already validated at binding (OD-004).
/// </summary>
public class FirstRunSetupService(
    ControlPlaneDbContext db,
    SetupCodeState setupCodeState,
    OwnerExistenceCache existence,
    IPasswordHasher<Owner> passwordHasher,
    ILookupNormalizer normalizer,
    TimeProvider timeProvider)
{
    private static readonly string[] OwnerUniqueIndexes = ["uq_owner_singleton", "uq_owner_normalized_user_name"];

    public async Task<FirstRunSetupResult> CreateOwnerAsync(
        string login,
        string password,
        string? setupCode,
        string? requestId,
        CancellationToken cancellationToken)
    {
        if (existence.Exists || await db.Owners.AnyAsync(cancellationToken))
        {
            return new FirstRunSetupResult(FirstRunSetupOutcome.AlreadyExists, null);
        }

        if (!setupCodeState.Matches(setupCode))
        {
            db.AuditEvents.Add(AuditEvent.OwnerFirstRunSetupRefusedWrongCode(timeProvider.GetUtcNow(), requestId));
            await db.SaveChangesAsync(cancellationToken);
            return new FirstRunSetupResult(FirstRunSetupOutcome.WrongSetupCode, null);
        }

        var owner = new Owner
        {
            UserName = login,
            NormalizedUserName = normalizer.NormalizeName(login),
            SecurityStamp = OwnerStamps.New(),
            ConcurrencyStamp = OwnerStamps.New(),
            UiLanguage = UiLanguage.Uk,
        };
        owner.PasswordHash = passwordHasher.HashPassword(owner, password);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.Owners.Add(owner);
            await db.SaveChangesAsync(cancellationToken);
            db.AuditEvents.Add(AuditEvent.OwnerFirstRunSetupSucceeded(owner.Id, timeProvider.GetUtcNow(), requestId));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsOwnerUniquenessViolation(exception))
        {
            // The concurrent loser: nothing of this transaction remains (FR-006).
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            existence.MarkExists();
            return new FirstRunSetupResult(FirstRunSetupOutcome.AlreadyExists, null);
        }

        setupCodeState.Void();
        existence.MarkExists();
        return new FirstRunSetupResult(
            FirstRunSetupOutcome.Created,
            new OwnerSessionDto(owner.Id, "uk", owner.SecurityStamp));
    }

    private static bool IsOwnerUniquenessViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && OwnerUniqueIndexes.Contains(postgres.ConstraintName);
}
