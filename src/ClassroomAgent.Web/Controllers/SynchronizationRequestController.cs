using System.Globalization;
using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// "Синхронизировать" (US-019 openapi <c>POST /synchronization/requests</c>; spec FR-004, FR-007): one form for
/// Admin and Dean, its own policy for its own §2 matrix row.
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3). The request binds no field (spec VR-001). The page to return to comes from the role
/// in the session, never from the request (spec VR-002). An accepted press is Post-Redirect-Get with a one-time
/// message (api-design §2.2); an unusable connection re-renders the role's page with <c>409</c>; read-only mode is
/// the guard's <see cref="Application.Exceptions.ReadOnlyModeException"/>, which the host handler maps to <c>409</c>
/// (US-008 FR-014).
/// </remarks>
[Authorize(Policy = InstallationPolicies.StartSynchronization)]
public sealed class SynchronizationRequestController(
    RequestSynchronizationUseCase request,
    InstallationPages pages,
    ILogger<SynchronizationRequestController> logger) : Controller
{
    /// <summary>The TempData key of the one-time message (api-design §2.2).</summary>
    public const string MessageTempDataKey = "SynchronizationMessage";

    [HttpPost(SignInRoutes.SynchronizationRequests)]
    public async Task<IActionResult> Submit(CancellationToken cancellationToken)
    {
        var isDean = User.IsInRole(InstallationSession.RoleName(AppRole.Dean));
        var actorId = ActorId();
        RequestSynchronizationOutcome outcome;
        try
        {
            outcome = await request.ExecuteAsync(
                actorId,
                isDean ? AppRole.Dean : AppRole.Admin,
                HttpContext.TraceIdentifier,
                cancellationToken);
        }
        catch (Application.Exceptions.ReadOnlyModeException)
        {
            SynchronizationRequestLog.Refused(logger, HttpContext.TraceIdentifier, nameof(AuditRefusalCategory.ReadOnlyMode));
            throw;
        }

        var messageKey = SynchronizationRequestTextKeys.Of(outcome, isDean);
        if (outcome == RequestSynchronizationOutcome.ConnectionNotUsable)
        {
            SynchronizationRequestLog.Refused(
                logger,
                HttpContext.TraceIdentifier,
                nameof(AuditRefusalCategory.ConnectionNotUsable));
            Response.StatusCode = StatusCodes.Status409Conflict;
            return isDean
                ? View("~/Views/Home/Index.cshtml", await pages.LandingAsync(User, messageKey, cancellationToken))
                : View(
                    "~/Views/WorkspaceConnection/Index.cshtml",
                    await pages.WorkspaceConnectionAsync(string.Empty, null, [], messageKey, cancellationToken));
        }

        SynchronizationRequestLog.Requested(logger, actorId, HttpContext.TraceIdentifier, outcome);
        TempData[MessageTempDataKey] = messageKey;
        return Redirect(isDean ? SignInRoutes.Landing : SignInRoutes.WorkspaceConnection);
    }

    /// <summary>The signed-in account's id; the policy guarantees there is one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            CultureInfo.InvariantCulture);
}
