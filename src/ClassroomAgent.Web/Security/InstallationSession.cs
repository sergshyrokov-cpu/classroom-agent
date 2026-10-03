using System.Globalization;
using System.Security.Claims;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// Issues and validates the installation's session cookie (US-008 spec FR-015; NFR-072): not persistent, 60
/// minutes of sliding idle expiry, and 8 hours absolute from the sign-in. There is no "remember me".
/// </summary>
public static class InstallationSession
{
    public const string SessionCookieName = "__Host-ca-session";

    public const string AntiforgeryCookieName = "__Host-ca-antiforgery";

    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(60);

    public static readonly TimeSpan AbsoluteLimit = TimeSpan.FromHours(8);

    /// <summary>Issues the session for an account the use case admitted (spec FR-010: never before that).</summary>
    public static Task SignInAsync(
        HttpContext context,
        SignedInUser user,
        TimeProvider timeProvider,
        bool passwordIsTemporary = false)
    {
        var signedInAt = timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        List<Claim> claims =
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, RoleName(user.Role)),
                new Claim(InstallationClaimTypes.UiLanguage, LanguageCode(user.UiLanguage)),
                new Claim(InstallationClaimTypes.SignedInAt, signedInAt),
                new Claim(InstallationClaimTypes.SecurityStamp, user.SecurityStamp),
            ];

        // US-012 spec FR-006: step 5 of the sequence issues a session that may reach the forced change form
        // and nothing else. The claim is the whole of that state — no second authentication mechanism.
        if (passwordIsTemporary)
        {
            claims.Add(new Claim(InstallationClaimTypes.PasswordIsTemporary, "true"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        return context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
    }

    /// <summary>
    /// Re-issues the current session with only the language claim replaced (US-039 spec FR-005, api-design §2.3).
    /// Every other claim is copied — the sign-in time above all, so the 8-hour absolute limit still counts from the
    /// original sign-in (I-2), and the security stamp, so the per-request check and sign-out keep working (I-3).
    /// </summary>
    public static Task ReissueWithLanguageAsync(HttpContext context, UiLanguage language)
    {
        var current = context.User.Claims
            .Where(c => c.Type != InstallationClaimTypes.UiLanguage)
            .Select(c => new Claim(c.Type, c.Value))
            .Append(new Claim(InstallationClaimTypes.UiLanguage, LanguageCode(language)));
        var identity = new ClaimsIdentity(current, CookieAuthenticationDefaults.AuthenticationScheme);

        return context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
    }

    public static string RoleName(AppRole role) => role switch
    {
        AppRole.Admin => nameof(AppRole.Admin),
        AppRole.Dean => nameof(AppRole.Dean),
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public static string LanguageCode(UiLanguage language) => language switch
    {
        UiLanguage.Uk => "uk",
        UiLanguage.En => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };

    /// <summary>
    /// Rejects a principal past the 8-hour absolute limit (NFR-072) or carrying a stamp the account no longer has —
    /// which is how a signed-out cookie stops authenticating, even when someone kept a copy of it (AC-014). The
    /// idle limit is the cookie's own.
    /// </summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var signedInAt = principal?.FindFirstValue(InstallationClaimTypes.SignedInAt);
        var services = context.HttpContext.RequestServices;
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();

        var valid = long.TryParse(signedInAt, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && now - DateTimeOffset.FromUnixTimeSeconds(seconds) <= AbsoluteLimit
            && await IsStampCurrentAsync(context, principal!);

        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    /// <summary>The account id of a signed-in principal, or null.</summary>
    public static long? AccountId(System.Security.Claims.ClaimsPrincipal principal) =>
        long.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var id)
            ? id
            : null;

    private static async Task<bool> IsStampCurrentAsync(
        CookieValidatePrincipalContext context,
        System.Security.Claims.ClaimsPrincipal principal)
    {
        if (AccountId(principal) is not { } accountId
            || principal.FindFirstValue(InstallationClaimTypes.SecurityStamp) is not { } stamp)
        {
            return false;
        }

        var sessions = context.HttpContext.RequestServices.GetRequiredService<ClassroomAgent.Application.UseCases.AccountSessionService>();
        return await sessions.IsCurrentAsync(accountId, stamp, context.HttpContext.RequestAborted);
    }
}
