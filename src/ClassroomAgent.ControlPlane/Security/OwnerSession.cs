using System.Globalization;
using System.Security.Claims;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ClassroomAgent.ControlPlane.Security;

/// <summary>
/// Issues and validates the Owner session cookie: non-persistent, 30 minutes sliding idle
/// expiry, 8 hours absolute from sign-in, invalidated server-side by the security stamp
/// (FR-010, FR-011, OD-002).
/// </summary>
public static class OwnerSession
{
    public const string OwnerRole = "Owner";

    public const string OwnerPolicy = "Owner";

    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan AbsoluteLimit = TimeSpan.FromHours(8);

    public static Task SignInAsync(HttpContext context, OwnerSessionDto session, TimeProvider timeProvider)
    {
        var signedInAt = timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, session.OwnerId.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Role, OwnerRole),
                new Claim(OwnerClaimTypes.UiLanguage, session.UiLanguage),
                new Claim(OwnerClaimTypes.SignedInAt, signedInAt),
                new Claim(OwnerClaimTypes.SecurityStamp, session.SecurityStamp),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
    }

    /// <summary>The Owner id of a signed-in principal, or null.</summary>
    public static long? OwnerId(ClaimsPrincipal principal) =>
        long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    /// <summary>Rejects a principal past the absolute limit or with a stale security stamp.</summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var ownerId = principal is null ? null : OwnerId(principal);
        var stamp = principal?.FindFirstValue(OwnerClaimTypes.SecurityStamp);
        var signedInAt = principal?.FindFirstValue(OwnerClaimTypes.SignedInAt);
        var services = context.HttpContext.RequestServices;
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();

        var valid = ownerId is not null
            && stamp is not null
            && long.TryParse(signedInAt, NumberStyles.None, CultureInfo.InvariantCulture, out var signedInSeconds)
            && now - DateTimeOffset.FromUnixTimeSeconds(signedInSeconds) <= AbsoluteLimit
            && await services.GetRequiredService<OwnerSessionService>()
                .IsSessionCurrentAsync(ownerId.Value, stamp, context.HttpContext.RequestAborted);

        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
