using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
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

    /// <summary>
    /// The Dean's sign-in (US-012 openapi <c>POST /sign-in</c>; spec FR-012). **Every** outcome of the six-step
    /// sequence answers with a redirect: the outcome travels in the <c>Location</c> and in TempData, never in the
    /// status code, so steps 1, 2 and 3 are indistinguishable to the caller (spec S-05, I-6).
    /// </summary>
    [HttpPost(SignInRoutes.SignInPage)]
    public async Task<IActionResult> SignInWithPassword(
        [FromForm] string? email,
        [FromForm] string? password,
        [FromServices] SignInDeanUseCase signIn,
        [FromServices] IAppUserRepository users,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (email is null || password is null)
        {
            return BadRequest();
        }

        var outcome = await signIn.ExecuteAsync(email, password, HttpContext.TraceIdentifier, cancellationToken);
        switch (outcome.Result)
        {
            // Steps 1, 2 and 3: one message, one redirect, nothing that tells them apart.
            case DeanSignInResult.UnknownLogin:
            case DeanSignInResult.LockedOut:
            case DeanSignInResult.WrongPassword:
                TempData[SignInRoutes.RefusalTempDataKey] = DeanAccountTextKeys.SignInRefused;
                return Redirect(SignInRoutes.SignInPage);

            // Step 4: the one distinction SC-2 allows, and only with the correct password and no lockout.
            case DeanSignInResult.AccountDisabled:
                TempData[SignInRoutes.RefusalTempDataKey] = DeanAccountTextKeys.SignInAccountDisabled;
                return Redirect(SignInRoutes.SignInPage);

            default:
                var dean = await users.FindDeanByIdAsync(outcome.AccountId!.Value, cancellationToken);
                if (dean is null)
                {
                    TempData[SignInRoutes.RefusalTempDataKey] = DeanAccountTextKeys.SignInRefused;
                    return Redirect(SignInRoutes.SignInPage);
                }

                var temporary = outcome.Result is DeanSignInResult.TemporaryPassword;
                await InstallationSession.SignInAsync(
                    HttpContext,
                    new SignedInUser(dean.Id, dean.Email, dean.Role, dean.UiLanguage, dean.SecurityStamp),
                    timeProvider,
                    temporary);

                return Redirect(temporary ? SignInRoutes.ForcedPasswordChange : SignInRoutes.Landing);
        }
    }
}
