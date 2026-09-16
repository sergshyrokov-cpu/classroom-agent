using ClassroomAgent.ControlPlane.Security;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// First-run setup (FR-003, FR-004): HTTP mapping only; the account rules are in
/// <see cref="FirstRunSetupService"/>. Anonymous per SC-4 "First-run setup".
/// </summary>
[AllowAnonymous]
[SetupGateExempt]
[Route("setup")]
public sealed class SetupController(
    FirstRunSetupService setupService,
    OwnerSessionService sessions,
    TimeProvider timeProvider) : Controller
{
    private const string SetupView = "~/Views/Setup/Index.cshtml";

    [HttpGet]
    public async Task<IActionResult> Show(CancellationToken cancellationToken)
    {
        if (await sessions.OwnerExistsAsync(cancellationToken))
        {
            return Redirect(User.Identity?.IsAuthenticated == true ? "/" : "/sign-in");
        }

        return View(SetupView, new SetupPageModel(null, AlreadyCreated: false));
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromForm] SetupRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Form(new SetupPageModel(request.Login, AlreadyCreated: false), StatusCodes.Status400BadRequest);
        }

        var result = await setupService.CreateOwnerAsync(
            request.Login!,
            request.Password!,
            request.SetupCode,
            HttpContext.TraceIdentifier,
            cancellationToken);

        switch (result.Outcome)
        {
            case FirstRunSetupOutcome.Created when result.Session is not null:
                await OwnerSession.SignInAsync(HttpContext, result.Session, timeProvider);
                return Redirect("/");
            case FirstRunSetupOutcome.WrongSetupCode:
                ModelState.AddModelError(nameof(SetupRequest.SetupCode), "Setup.SetupCode.Invalid");
                return Form(new SetupPageModel(request.Login, AlreadyCreated: false), StatusCodes.Status400BadRequest);
            default:
                return Form(new SetupPageModel(null, AlreadyCreated: true), StatusCodes.Status409Conflict);
        }
    }

    private ViewResult Form(SetupPageModel model, int statusCode)
    {
        var view = View(SetupView, model);
        view.StatusCode = statusCode;
        return view;
    }
}
