using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The sign-in decision (US-008 spec FR-010): the order is fixed in one place and testable without a browser.
/// The callback has already proven <c>state</c> and the correlation cookie before this runs.
/// </summary>
/// <remarks>
/// Creating the Admin's account, stamping the sign-in time and writing the audit row are the BR-026 service
/// writes <see cref="PermittedServiceWrite.SignInBookkeeping"/> and <see cref="PermittedServiceWrite.AuditEvent"/>,
/// so they run in read-only mode; the closed list is not widened and this use case carries no private exemption
/// (spec FR-013; US-007 FR-004, FR-005). Failures are outcomes, not exceptions (AD-9), and the outcome carries
/// no user-visible string (AD-6).
/// </remarks>
public sealed class CompleteGoogleSignInUseCase(
    IControlPlaneClient controlPlane,
    IAppUserRepository users,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    InstallationIdentity identity,
    SchoolDefaults schoolDefaults,
    TimeProvider timeProvider)
{
    /// <summary>
    /// The callback failed before a trustworthy identity existed — a missing, unknown or replayed <c>state</c>, no
    /// correlation cookie, or an email Google did not return or did not report as verified (spec FR-007, VR-005,
    /// I-6). Always attributed anonymously, and the Control Plane is never asked.
    /// </summary>
    public async Task<SignInOutcome> RecordFailedCallbackAsync(string? requestId, CancellationToken cancellationToken)
    {
        await WriteAsync(
            AuditEvent.AdminSignInRefusedAnonymous(AuditRefusalCategory.CallbackFailed, Now(), requestId),
            PermittedServiceWrite.AuditEvent,
            cancellationToken);
        return SignInOutcome.Refused(SignInRefusal.SignInFailed);
    }

    /// <summary>Decides one sign-in for the address Google reported, and writes exactly one audit row.</summary>
    public async Task<SignInOutcome> ExecuteAsync(string email, string? requestId, CancellationToken cancellationToken)
    {
        // 2: the email is lower-cased before it is used for anything (BR-079, SC-3).
        var normalized = AppUser.Normalize(email);

        // 3: ask the Control Plane — every time, with no cache and no fallback (S-02, S-03).
        var reply = await controlPlane.CheckAdminLoginAsync(identity.InstallationId, normalized, cancellationToken);

        var existing = await users.FindByNormalizedEmailAsync(normalized, cancellationToken);
        if (reply != AdminLoginCheckReply.Allowed)
        {
            return await RefuseAsync(reply, existing, requestId, cancellationToken);
        }

        // The disabled check is written and tested now, so no window exists in which a disabled account could
        // sign in between this Story and US-012 (spec I-9).
        if (existing is not null && existing.IsDisabled)
        {
            return await RefuseWithAsync(
                AuditRefusalCategory.AccountDisabled,
                SignInRefusal.AccountDisabled,
                existing,
                requestId,
                cancellationToken);
        }

        return await AdmitAsync(normalized, existing, requestId, cancellationToken);
    }

    /// <summary>
    /// Creates or reuses the account, stamps the sign-in and writes the success row. Both commits run inside one
    /// transaction, so a successful sign-in cannot leave an account without its audit row, and the row carries the
    /// identity the insert generated (db-design §4.4).
    /// </summary>
    private async Task<SignInOutcome> AdmitAsync(
        string normalizedEmail,
        AppUser? existing,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var now = Now();
        AppUser? admitted = null;
        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var user = existing;
                if (user is null)
                {
                    user = AppUser.CreateAdmin(normalizedEmail, schoolDefaults.UiLanguage, now);
                    users.Add(user);
                    try
                    {
                        await CommitAsync(PermittedServiceWrite.SignInBookkeeping, token);
                    }
                    catch (UniqueEmailViolationException)
                    {
                        // Two first sign-ins of the same address at once: someone else created the row, so re-read
                        // and stamp it instead of surfacing an error (spec I-10; db-design §3.3).
                        users.Forget(user);
                        user = await users.FindByNormalizedEmailAsync(normalizedEmail, token)
                            ?? throw new InvalidOperationException(
                                "The unique index rejected the insert but no account with that address exists.");
                        user.RecordSuccessfulSignIn(now);
                        await CommitAsync(PermittedServiceWrite.SignInBookkeeping, token);
                    }
                }
                else
                {
                    user.RecordSuccessfulSignIn(now);
                    await CommitAsync(PermittedServiceWrite.SignInBookkeeping, token);
                }

                auditEvents.Add(AuditEvent.AdminSignInSucceeded(user.Id, now, requestId));
                await CommitAsync(PermittedServiceWrite.AuditEvent, token);
                admitted = user;
            },
            cancellationToken);

        var signedIn = admitted ?? throw new InvalidOperationException("The sign-in committed no account.");
        return SignInOutcome.Success(new SignedInUser(
            signedIn.Id,
            signedIn.Email,
            signedIn.Role,
            signedIn.UiLanguage,
            signedIn.SecurityStamp));
    }

    private Task<SignInOutcome> RefuseAsync(
        AdminLoginCheckReply reply,
        AppUser? existing,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var (category, refusal) = reply switch
        {
            AdminLoginCheckReply.NotAllowed => (AuditRefusalCategory.NotInAllowedAdmin, SignInRefusal.NotApproved),
            AdminLoginCheckReply.UnknownInstallation => (AuditRefusalCategory.UnknownInstallation, SignInRefusal.CouldNotConfirm),
            AdminLoginCheckReply.Unavailable => (AuditRefusalCategory.ControlPlaneUnavailable, SignInRefusal.CouldNotConfirm),
            _ => throw new ArgumentOutOfRangeException(nameof(reply), reply, null),
        };

        return RefuseWithAsync(category, refusal, existing, requestId, cancellationToken);
    }

    /// <summary>
    /// Writes the refusal row and returns the refusal. The account, when there is one, is neither modified nor
    /// deleted: it is kept for history and removed only by the retention purge (BR-012, PC-11).
    /// </summary>
    private async Task<SignInOutcome> RefuseWithAsync(
        AuditRefusalCategory category,
        SignInRefusal refusal,
        AppUser? existing,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var row = existing is null
            ? AuditEvent.AdminSignInRefusedAnonymous(category, Now(), requestId)
            : AuditEvent.AdminSignInRefused(existing.Id, existing.Role, category, Now(), requestId);
        await WriteAsync(row, PermittedServiceWrite.AuditEvent, cancellationToken);
        return SignInOutcome.Refused(refusal);
    }

    private async Task WriteAsync(AuditEvent row, PermittedServiceWrite write, CancellationToken cancellationToken)
    {
        auditEvents.Add(row);
        await CommitAsync(write, cancellationToken);
    }

    private async Task CommitAsync(PermittedServiceWrite write, CancellationToken cancellationToken)
    {
        using (writeScope.Declare(write))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private DateTimeOffset Now() => timeProvider.GetUtcNow();
}
