using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Whether a session cookie still names a current account (US-008 spec FR-015, FR-016; AC-014), and the rotation
/// that ends every session of an account. The <c>Owner</c> session of US-001 works the same way: the stamp travels
/// in the cookie and is compared with the stored one on every request, so a cookie someone kept a copy of stops
/// authenticating the moment its owner signs out.
/// </summary>
/// <remarks>
/// The rotation is sign-in bookkeeping on the BR-026 closed list, so signing out works in read-only mode too
/// (spec FR-013); it writes no audit row, because <c>trebovaniya.md</c> §5 lists sign-in and refused sign-in and
/// the list is closed (spec I-13).
/// </remarks>
public sealed class AccountSessionService(
    IAppUserRepository users,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope)
{
    /// <summary>
    /// True when the account exists, is not disabled, and still carries that stamp. Read untracked: this runs on
    /// every authenticated request and changes nothing (security review F-4).
    /// </summary>
    public async Task<bool> IsCurrentAsync(long accountId, string securityStamp, CancellationToken cancellationToken)
    {
        var state = await users.GetSessionStateAsync(accountId, cancellationToken);
        return state is { IsDisabled: false } && string.Equals(state.SecurityStamp, securityStamp, StringComparison.Ordinal);
    }

    /// <summary>Ends every session of the account by rotating its stamp (AC-014).</summary>
    public async Task EndSessionsAsync(long accountId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(accountId, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.RotateSecurityStamp();
        using (writeScope.Declare(PermittedServiceWrite.SignInBookkeeping))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
