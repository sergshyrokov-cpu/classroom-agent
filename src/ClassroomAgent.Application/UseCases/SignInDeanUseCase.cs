using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The six-step sign-in sequence of SC-2, in order (US-012 spec FR-012, FR-013). It is deliberately not built
/// on <c>SignInManager</c>: that checks <c>CanSignInAsync</c> before the lockout and resets the counter on a
/// correct password, which would break steps 2 and 4 (SC-2, spec I-1, S-09).
/// </summary>
/// <remarks>
/// Its writes — the failed-attempt counter, the lockout and the last successful sign-in — are sign-in
/// bookkeeping, one of the closed list of service writes permitted in read-only mode (BR-026, spec FR-015), so
/// this use case consults no read-only guard and declares <see cref="PermittedServiceWrite.SignInBookkeeping"/>
/// around every commit.
/// </remarks>
public sealed class SignInDeanUseCase(
    IAppUserRepository users,
    IAuditEventRepository auditEvents,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    /// <summary>Consecutive failures that lock sign-in (SC-2).</summary>
    public const int MaximumFailedAttempts = 5;

    /// <summary>How long the lockout lasts. It expires by itself; there is no permanent lockout (SC-2 v62).</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// A hash of a value nobody knows, verified against on the unknown-login path so that step 1 costs what a
    /// real check costs (SC-2). It is <b>static</b> on purpose: the use case is registered per request, so an
    /// instance field would be hashed again on every unknown login and that path would cost a hash **plus** a
    /// verification while a real check costs one verification — the same oracle, merely inverted (security
    /// review F-3). The Control Plane's <c>OwnerSignInService</c> declares its equivalent the same way.
    /// Step 2 deliberately does **not** verify at all: skipping the password while a lockout is in force is
    /// what SC-2 v66 requires, and equalising it there would defeat the rule.
    /// </summary>
    private static string? _timingEqualisationHash;

    public async Task<DeanSignInOutcome> ExecuteAsync(
        string email,
        string password,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        var now = timeProvider.GetUtcNow();
        var normalized = AppUser.Normalize(email);
        var account = await users.FindByNormalizedEmailAsync(normalized, cancellationToken);

        // Step 1: the login is unknown. The row names no actor and no target: the typed login is never
        // recorded (SC-11, spec FR-012).
        if (account is null || account.Role != AppRole.Dean || account.PasswordHash is not { } hash)
        {
            // Spend the same hashing work as a real check, so the response time does not reveal whether the
            // login exists (SC-2: "the response never reveals whether the login exists"; §2 v62 notes this page
            // is reachable from the internet and the login is a guessable work email). The Control Plane's
            // Owner sign-in does the same. The result is discarded on purpose.
            passwordHasher.Verify(TimingEqualisationHashOf(passwordHasher), password);
            await CommitAsync(
                () => auditEvents.Add(AuditEvent.DeanSignInRefusedUnknownLogin(now, requestId)),
                cancellationToken);
            return new DeanSignInOutcome(DeanSignInResult.UnknownLogin, null);
        }

        // Step 2: a lockout is in force. The password is NOT verified and the counter does not move, so a
        // disabled account cannot be used to test passwords past the lockout (SC-2 v65, v66).
        if (account.IsLockedOut(now))
        {
            await AuditRefusalAsync(account, AuditRefusalCategory.LockedOut, now, requestId, cancellationToken);
            return new DeanSignInOutcome(DeanSignInResult.LockedOut, account.Id);
        }

        // Step 3: the password does not match. The counter rises and may lock sign-in.
        if (!passwordHasher.Verify(hash, password))
        {
            account.RecordFailedSignIn(now, MaximumFailedAttempts, LockoutDuration);
            await AuditRefusalAsync(account, AuditRefusalCategory.WrongPassword, now, requestId, cancellationToken);
            return new DeanSignInOutcome(DeanSignInResult.WrongPassword, account.Id);
        }

        // Step 4: the account is disabled. Only reachable with the correct password and no lockout, and the
        // counter is neither incremented nor reset (SC-2 v65).
        if (account.IsDisabled)
        {
            await AuditRefusalAsync(account, AuditRefusalCategory.AccountDisabled, now, requestId, cancellationToken);
            return new DeanSignInOutcome(DeanSignInResult.AccountDisabled, account.Id);
        }

        // Steps 5 and 6: the sign-in succeeded. The counter is reset and the time is recorded either way; a
        // temporary password leads to the forced change instead of a session (spec FR-006, I-3).
        account.RecordSuccessfulSignIn(now);
        await CommitAsync(
            () => auditEvents.Add(AuditEvent.DeanSignInSucceeded(account.Id, now, requestId)),
            cancellationToken);

        return new DeanSignInOutcome(
            account.PasswordIsTemporary ? DeanSignInResult.TemporaryPassword : DeanSignInResult.SignedIn,
            account.Id);
    }

    /// <summary>
    /// The dummy hash, computed at most once for the process. Two threads racing here would each hash once and
    /// one value would win; that is bounded and harmless, and it keeps the field free of a lock on the
    /// sign-in path.
    /// </summary>
    private static string TimingEqualisationHashOf(IPasswordHasher hasher) =>
        _timingEqualisationHash ??= hasher.Hash(Guid.NewGuid().ToString("N"));

    private Task AuditRefusalAsync(
        AppUser account,
        AuditRefusalCategory category,
        DateTimeOffset now,
        string? requestId,
        CancellationToken cancellationToken) =>
        CommitAsync(
            () => auditEvents.Add(AuditEvent.DeanSignInRefused(account.Id, category, now, requestId)),
            cancellationToken);

    /// <summary>
    /// Stages the audit row beside whatever bookkeeping the step changed and commits once, declaring the
    /// BR-026 write so it lands in read-only mode too (spec FR-015).
    /// </summary>
    private async Task CommitAsync(Action stageAuditRow, CancellationToken cancellationToken)
    {
        stageAuditRow();
        using (writeScope.Declare(PermittedServiceWrite.SignInBookkeeping))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
