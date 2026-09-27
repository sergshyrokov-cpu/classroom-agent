using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace ClassroomAgent.Infrastructure.Security;

/// <summary>
/// The password primitive of SC-2, and nothing more (US-012 spec FR-018, OD-002): ASP.NET Core Identity's
/// <see cref="IPasswordHasher{TUser}"/> behind the application's own port, so no Identity type crosses into
/// <c>Application</c> or <c>Domain</c> (AD-4).
/// </summary>
/// <remarks>
/// The sign-in sequence itself is an application use case: SC-2 forbids building it on <c>SignInManager</c>,
/// which checks <c>CanSignInAsync</c> before the lockout and resets the failed-attempt counter on a correct
/// password.
/// </remarks>
public sealed class PasswordHasherAdapter(IPasswordHasher<AppUser> hasher)
    : Application.Ports.IPasswordHasher
{
    /// <summary>
    /// The account Identity's hasher is handed. Its implementation ignores the instance for the algorithm used
    /// here, so a throw-away one keeps the port free of an account parameter it does not need. The address is
    /// in the reserved <c>.invalid</c> domain and belongs to nobody (RFC 2606).
    /// </summary>
    private static readonly AppUser Unused =
        AppUser.CreateAdmin("hasher@invalid.invalid", UiLanguage.Uk, DateTimeOffset.UnixEpoch);

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return hasher.HashPassword(Unused, password);
    }

    public bool Verify(string hash, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(hash);
        ArgumentNullException.ThrowIfNull(password);

        try
        {
            return hasher.VerifyHashedPassword(Unused, hash, password) is not PasswordVerificationResult.Failed;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            // A stored value that is not in the hasher's format cannot match any password. It is a failed
            // verification, not a server error: the sign-in path must answer the common refusal rather than
            // 500, which would both break the page and tell the caller that this account is special (SC-2,
            // US-012 spec S-05). The value itself is never logged (SC-10).
            return false;
        }
    }
}
