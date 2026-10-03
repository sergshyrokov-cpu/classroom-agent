using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The connection settings of the admin panel (US-009 openapi; spec FR-004, FR-005): the school's Workspace
/// domain, shown, and its technical account, entered. Admin only — the §2 permission matrix row (FR-010).
/// </summary>
/// <remarks>
/// HTTP mapping only: the state comes from <see cref="GetWorkspaceConnectionQuery"/> and the decision from
/// <see cref="SaveWorkspaceConnectionUseCase"/>, which holds BR-020 (AD-3). A successful save redirects so a
/// reload cannot re-submit a form that writes an audit row; a refusal re-renders in place, keeping the typed
/// address so it can be corrected without carrying a school account address through TempData (api-design §2.5).
/// </remarks>
[Authorize(Policy = InstallationPolicies.ConfigureWorkspaceConnection)]
public sealed class WorkspaceConnectionController(
    GetWorkspaceConnectionQuery connectionQuery,
    SaveWorkspaceConnectionUseCase save,
    GetLegitimacyModeQuery legitimacyMode,
    GetLastSynchronizationQuery lastSynchronization,
    ILogger<WorkspaceConnectionController> logger) : Controller
{
    private const string SavedTempDataKey = "WorkspaceConnectionSaved";

    [HttpGet(SignInRoutes.WorkspaceConnection)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var saved = TempData[SavedTempDataKey] as string;
        return View(await PageAsync(string.Empty, saved, [], cancellationToken));
    }

    [HttpPost(SignInRoutes.WorkspaceConnection)]
    public async Task<IActionResult> Save(
        [FromForm] SaveWorkspaceConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var typed = request.ImpersonationUserEmail ?? string.Empty;
        if (!ModelState.IsValid)
        {
            // The rejected value is never logged and never echoed beyond its own field (SC-10).
            WorkspaceConnectionLog.Rejected(logger, HttpContext.TraceIdentifier);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(nameof(Index), await PageAsync(typed, null, FieldErrorKeys(), cancellationToken));
        }

        var outcome = await save.ExecuteAsync(
            ActorId(),
            typed,
            HttpContext.TraceIdentifier,
            cancellationToken);

        if (outcome.IsSaved)
        {
            WorkspaceConnectionLog.Saved(logger, ActorId(), HttpContext.TraceIdentifier);
            TempData[SavedTempDataKey] = WorkspaceConnectionTextKeys.Saved;
            return Redirect(SignInRoutes.WorkspaceConnection);
        }

        var refusal = outcome.Refusal!.Value;
        WorkspaceConnectionLog.Refused(logger, refusal, HttpContext.TraceIdentifier);
        Response.StatusCode = StatusCodes.Status409Conflict;
        return View(nameof(Index), await PageAsync(typed, RefusalKey(refusal), [], cancellationToken));
    }

    private async Task<WorkspaceConnectionPageModel> PageAsync(
        string typed,
        string? messageKey,
        IReadOnlyList<string> fieldErrorKeys,
        CancellationToken cancellationToken)
    {
        var view = await connectionQuery.ExecuteAsync(cancellationToken);
        var mode = await legitimacyMode.ExecuteAsync(cancellationToken);
        var last = await lastSynchronization.ExecuteAsync(cancellationToken);
        return new WorkspaceConnectionPageModel(
            view.State,
            view.InstallationDomain,
            view.SavedImpersonationUserEmail,
            typed,
            mode.IsReadOnly,
            mode.Reason,
            messageKey,
            fieldErrorKeys,
            last);
    }

    /// <summary>The message keys of the rejected fields; the values themselves never leave the form.</summary>
    private IReadOnlyList<string> FieldErrorKeys() =>
        ModelState.Values
            .SelectMany(state => state.Errors)
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string RefusalKey(SaveWorkspaceConnectionRefusal refusal) => refusal switch
    {
        SaveWorkspaceConnectionRefusal.DomainMismatch => WorkspaceConnectionTextKeys.RefusedDomainMismatch,
        SaveWorkspaceConnectionRefusal.ImpersonationDomainMismatch =>
            WorkspaceConnectionTextKeys.RefusedImpersonationDomainMismatch,
        SaveWorkspaceConnectionRefusal.DomainNotConfirmed => WorkspaceConnectionTextKeys.RefusedDomainNotConfirmed,
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, null),
    };

    /// <summary>The signed-in Admin's account id; the policy guarantees there is one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            System.Globalization.CultureInfo.InvariantCulture);
}
