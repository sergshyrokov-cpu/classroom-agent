using ClassroomAgent.ControlPlane.Security;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// Owner sign-in (FR-007, FR-008): HTTP mapping only; the sequence and lockout are in
/// <see cref="OwnerSignInService"/>. Anonymous per SC-4 "Owner sign-in". No return URL
/// is accepted (spec I-6).
/// </summary>
[AllowAnonymous]
[Route("sign-in")]
public sealed class SignInController(OwnerSignInService signInService, TimeProvider timeProvider) : Controller
{
    private const string SignInView = "~/Views/SignIn/Index.cshtml";

    [HttpGet]
    public IActionResult Show() =>
        User.Identity?.IsAuthenticated == true
            ? Redirect("/")
            : View(SignInView, new SignInPageModel(null, Refused: false));

    [HttpPost]
    public async Task<IActionResult> Submit([FromForm] SignInRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Form(new SignInPageModel(request.Login, Refused: false), StatusCodes.Status400BadRequest);
        }

        var result = await signInService.SignInAsync(
            request.Login!,
            request.Password!,
            HttpContext.TraceIdentifier,
            cancellationToken);

        if (result is { Outcome: OwnerSignInOutcome.SignedIn, Session: not null })
        {
            await OwnerSession.SignInAsync(HttpContext, result.Session, timeProvider);
            return Redirect("/");
        }

        return Form(new SignInPageModel(request.Login, Refused: true), StatusCodes.Status401Unauthorized);
    }

    private ViewResult Form(SignInPageModel model, int statusCode)
    {
        var view = View(SignInView, model);
        view.StatusCode = statusCode;
        return view;
    }
}
