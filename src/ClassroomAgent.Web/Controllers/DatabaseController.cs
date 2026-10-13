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
/// The Database page (US-024 openapi <c>GET /database</c>, policy <c>ViewDatabaseStatistics</c>) and its own
/// "Synchronize" press (<c>POST /database/synchronization-requests</c>, policy <c>StartSynchronization</c>; OD-011 a).
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3). The page binds no parameter, so nothing from the address is echoed (spec VR-001). The press
/// calls the US-019 use case exactly as <see cref="SynchronizationRequestController"/> does — guard, connection check,
/// enqueue and audit are the use case's — and differs only in where the user lands: always this page, a constant of
/// the endpoint, never a value from the request (spec VR-002, S-07). Read-only mode is the guard's
/// <see cref="Application.Exceptions.ReadOnlyModeException"/>, which the host handler maps to <c>409</c>.
/// </remarks>
public sealed class DatabaseController(
    GetDatabaseStatisticsQuery statistics,
    RequestSynchronizationUseCase request,
    ILogger<DatabaseController> logger) : Controller
{
    [Authorize(Policy = InstallationPolicies.ViewDatabaseStatistics)]
    [HttpGet(SignInRoutes.Database)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var actorId = ActorId();
        var page = await PageAsync(
            TempData[SynchronizationRequestController.MessageTempDataKey] as string,
            cancellationToken);
        DatabaseStatisticsLog.Shown(logger, actorId);
        return View(page);
    }

    [Authorize(Policy = InstallationPolicies.StartSynchronization)]
    [HttpPost(SignInRoutes.DatabaseSynchronizationRequests)]
    public async Task<IActionResult> RequestSynchronization(CancellationToken cancellationToken)
    {
        var isDean = IsDean();
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
            return View(nameof(Index), await PageAsync(messageKey, cancellationToken));
        }

        SynchronizationRequestLog.Requested(logger, actorId, HttpContext.TraceIdentifier, outcome);
        TempData[SynchronizationRequestController.MessageTempDataKey] = messageKey;
        return Redirect(SignInRoutes.Database);
    }

    private async Task<DatabaseStatisticsPageModel> PageAsync(string? messageKey, CancellationToken cancellationToken) =>
        new(await statistics.ExecuteAsync(IsDean() ? AppRole.Dean : AppRole.Admin, cancellationToken), messageKey);

    /// <summary>The role from the session, never from the request (spec VR-004); the policies admit only Admin and Dean.</summary>
    private bool IsDean() => User.IsInRole(InstallationSession.RoleName(AppRole.Dean));

    /// <summary>The signed-in account's id; the policy guarantees there is one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            CultureInfo.InvariantCulture);
}
