using ClassroomAgent.ControlPlane.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// The Owner sign-in sequence of SC-2 v66 (FR-008, FR-009): unknown login → lockout in
/// force (password not checked) → wrong password → success. Every outcome is audited in
/// the same save as the state change it causes (db-design §4.3). Lockout time comes from
/// the injectable clock.
/// </summary>
public class OwnerSignInService(
    ControlPlaneDbContext db,
    IPasswordHasher<Owner> passwordHasher,
    ILookupNormalizer normalizer,
    TimeProvider timeProvider)
{
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private const int MaxConcurrencyRetries = 3;

    private static readonly Owner HashTimingOwner = new();

    private static readonly Lazy<string> HashTimingHash =
        new(() => new PasswordHasher<Owner>().HashPassword(HashTimingOwner, OwnerStamps.New()));

    public async Task<OwnerSignInResult> SignInAsync(
        string login,
        string password,
        string? requestId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await AttemptAsync(login, password, requestId, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // A parallel attempt changed the counter; re-read the account and run the sequence again.
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<OwnerSignInResult> AttemptAsync(
        string login,
        string password,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var normalizedLogin = normalizer.NormalizeName(login);
        var owner = await db.Owners.SingleOrDefaultAsync(o => o.NormalizedUserName == normalizedLogin, cancellationToken);

        if (owner is null)
        {
            // Spend the same hashing work as a real check, so timing does not reveal the login.
            passwordHasher.VerifyHashedPassword(HashTimingOwner, HashTimingHash.Value, password);
            db.AuditEvents.Add(AuditEvent.OwnerSignInRefusedUnknownLogin(now, requestId));
            await db.SaveChangesAsync(cancellationToken);
            return Refused();
        }

        if (owner.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
        {
            db.AuditEvents.Add(AuditEvent.OwnerSignInRefused(owner.Id, AuditRefusalCategory.LockedOut, now, requestId));
            await db.SaveChangesAsync(cancellationToken);
            return Refused();
        }

        var verification = passwordHasher.VerifyHashedPassword(owner, owner.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            owner.AccessFailedCount++;
            if (owner.AccessFailedCount >= MaxFailedAttempts)
            {
                // The lockout starts and the count of consecutive failures starts again (spec I-4).
                owner.LockoutEnd = now + LockoutDuration;
                owner.AccessFailedCount = 0;
            }

            owner.ConcurrencyStamp = OwnerStamps.New();
            db.AuditEvents.Add(AuditEvent.OwnerSignInRefused(owner.Id, AuditRefusalCategory.WrongPassword, now, requestId));
            await db.SaveChangesAsync(cancellationToken);
            return Refused();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            owner.PasswordHash = passwordHasher.HashPassword(owner, password);
        }

        owner.AccessFailedCount = 0;
        owner.LockoutEnd = null;
        owner.ConcurrencyStamp = OwnerStamps.New();
        db.AuditEvents.Add(AuditEvent.OwnerSignInSucceeded(owner.Id, now, requestId));
        await db.SaveChangesAsync(cancellationToken);

        return new OwnerSignInResult(
            OwnerSignInOutcome.SignedIn,
            new OwnerSessionDto(owner.Id, owner.UiLanguage == UiLanguage.En ? "en" : "uk", owner.SecurityStamp));
    }

    private static OwnerSignInResult Refused() => new(OwnerSignInOutcome.Refused, null);
}
