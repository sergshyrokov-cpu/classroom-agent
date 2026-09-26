using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// "Проверить доступ" (US-011 openapi; spec FR-007, FR-011): the page and the run, one path. Admin only — the §2
/// matrix row, its own policy (spec I-9).
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3). The <c>GET</c> calls nothing and writes nothing (API-4). The <c>POST</c> binds no field
/// (spec VR-001) and answers the page with the result — no Post-Redirect-Get, because nothing is stored to redirect
/// to (api-design §2.2). A finding is a result, <c>200</c>; an unusable connection is <c>409</c> with the page; read-only
/// mode is the guard's <see cref="Application.Exceptions.ReadOnlyModeException"/>, which the host handler maps to
/// <c>409</c> (US-008 FR-014).
/// </remarks>
[Authorize(Policy = InstallationPolicies.RunAccessCheck)]
public sealed class AccessCheckController(
    GetWorkspaceConnectionQuery connectionQuery,
    GetLegitimacyModeQuery legitimacyMode,
    RunAccessCheckUseCase run,
    ILogger<AccessCheckController> logger) : Controller
{
    [HttpGet(SignInRoutes.AccessCheck)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await PageAsync(null, null, cancellationToken));

    [HttpPost(SignInRoutes.AccessCheck)]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        var actorId = ActorId();
        var outcome = await run.ExecuteAsync(actorId, HttpContext.TraceIdentifier, cancellationToken);
        if (outcome.Result is { } result)
        {
            AccessCheckLog.Run(logger, actorId, result, HttpContext.TraceIdentifier);
            return View(nameof(Index), await PageAsync(null, result, cancellationToken));
        }

        var refusal = outcome.Refusal!.Value;
        AccessCheckLog.Refused(logger, refusal, HttpContext.TraceIdentifier);
        Response.StatusCode = StatusCodes.Status409Conflict;
        return View(nameof(Index), await PageAsync(AccessCheckTextKeys.Of(refusal), null, cancellationToken));
    }

    private async Task<AccessCheckPageModel> PageAsync(
        string? messageKey,
        AccessCheckResult? result,
        CancellationToken cancellationToken)
    {
        var view = await connectionQuery.ExecuteAsync(cancellationToken);
        var mode = await legitimacyMode.ExecuteAsync(cancellationToken);
        return new AccessCheckPageModel(
            view.State,
            view.SavedImpersonationUserEmail,
            view.InstallationDomain,
            mode.IsReadOnly,
            mode.Reason,
            messageKey,
            result);
    }

    /// <summary>The signed-in Admin's account id; the policy guarantees there is one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            System.Globalization.CultureInfo.InvariantCulture);
}
