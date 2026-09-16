using ClassroomAgent.ControlPlane.Security;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// Sign-out (FR-011): POST only, with the antiforgery token; ends the session server-side
/// by rotating the security stamp. There is no GET endpoint (API-4). Not audited.
/// </summary>
[Authorize(Policy = OwnerSession.OwnerPolicy)]
[Route("sign-out")]
public sealed class SignOutController(OwnerSessionService sessions) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Submit(CancellationToken cancellationToken)
    {
        if (OwnerSession.OwnerId(User) is { } ownerId)
        {
            await sessions.EndSessionAsync(ownerId, cancellationToken);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/sign-in");
    }
}
