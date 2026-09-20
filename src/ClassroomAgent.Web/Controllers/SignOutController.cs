using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// Sign-out (US-008 spec FR-016): a <c>POST</c> with the antiforgery token — a GET does not sign anyone out
/// (<c>trebovaniya.md</c> §8, API-4). It clears the session cookie and lands the user on the sign-in page, and it
/// writes <b>no</b> audit row: §5 lists sign-in and refused sign-in, and the list is closed (spec I-13).
/// </summary>
[Authorize(Policy = InstallationPolicies.AuthenticatedUser)]
public sealed class SignOutController(AccountSessionService sessions) : Controller
{
    [HttpPost(SignInRoutes.SignOut)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // AC-014: the previous cookie must stop authenticating, so every session of this account ends here and not
        // only the one this browser holds.
        if (InstallationSession.AccountId(User) is { } accountId)
        {
            await sessions.EndSessionsAsync(accountId, cancellationToken);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect(SignInRoutes.SignInPage);
    }
}
