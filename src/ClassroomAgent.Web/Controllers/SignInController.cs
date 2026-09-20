using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The installation's sign-in page and the start of the Google sign-in (US-008 spec FR-006; api-design §3).
/// </summary>
/// <remarks>
/// The page is the SC-4 "Dean sign-in page" entry (spec I-1): one page serves the host, carrying only the Google
/// form in this Story, and US-012 adds the Dean password form to this same page rather than opening a second
/// anonymous endpoint. The start is a <c>POST</c> with the antiforgery token, because it sets the correlation
/// cookie and the <c>state</c> — it changes state, and a GET never does (<c>trebovaniya.md</c> §8).
/// </remarks>
[AllowAnonymous]
public sealed class SignInController : Controller
{
    [HttpGet(SignInRoutes.SignInPage)]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(SignInRoutes.Landing);
        }

        // The refusal of the previous callback, as a category in TempData: never a query parameter, which would let
        // anyone craft a link rendering arbitrary text on the school's own sign-in page (api-design §2.2).
        var refusal = TempData[SignInRoutes.RefusalTempDataKey] as string;
        return View(new SignInPageModel(refusal));
    }

    [HttpPost(SignInRoutes.Start)]
    public IActionResult Start() =>
        Challenge(
            new AuthenticationProperties { RedirectUri = SignInRoutes.Landing },
            InstallationSecurityServices.GoogleScheme);
}
