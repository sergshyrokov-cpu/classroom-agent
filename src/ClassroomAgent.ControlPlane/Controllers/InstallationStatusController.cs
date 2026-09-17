using ClassroomAgent.ControlPlane.Security;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// Suspend and resume confirmations and submissions (US-004 FR-002 … FR-009): HTTP mapping only;
/// the rules are in <see cref="InstallationStatusService"/>. The target status is fixed by the
/// endpoint and nothing is bound from the request. A non-UUID <c>{id}</c> reaches the 404 catch-all.
/// </summary>
[Authorize(Policy = OwnerSession.OwnerPolicy)]
[Route("installations/{id:guid}")]
public sealed class InstallationStatusController(InstallationStatusService service) : Controller
{
    private const string ConfirmationView = "~/Views/InstallationStatus/Confirmation.cshtml";

    [HttpGet("suspension")]
    public Task<IActionResult> Suspension(Guid id, CancellationToken cancellationToken) =>
        Confirmation(id, InstallationStatusTransition.Suspend, cancellationToken);

    [HttpPost("suspension")]
    public Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken) =>
        Change(id, InstallationStatusTransition.Suspend, cancellationToken);

    [HttpGet("resumption")]
    public Task<IActionResult> Resumption(Guid id, CancellationToken cancellationToken) =>
        Confirmation(id, InstallationStatusTransition.Resume, cancellationToken);

    [HttpPost("resumption")]
    public Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken) =>
        Change(id, InstallationStatusTransition.Resume, cancellationToken);

    private async Task<IActionResult> Confirmation(
        Guid id,
        InstallationStatusTransition transition,
        CancellationToken cancellationToken)
    {
        if (await service.GetConfirmationAsync(id, cancellationToken) is not { } installation)
        {
            return NotFound();
        }

        // A stale link for an installation already in the target status (spec I-3).
        return installation.Status == InstallationStatusService.TargetOf(transition)
            ? Redirect(InstallationStatusNotice.DetailPathWithNotice(id, installation.Status))
            : View(ConfirmationView, new InstallationStatusConfirmationPageModel(installation, transition));
    }

    private async Task<IActionResult> Change(
        Guid id,
        InstallationStatusTransition transition,
        CancellationToken cancellationToken)
    {
        if (OwnerSession.OwnerId(User) is not { } ownerId)
        {
            return Forbid();
        }

        var result = await service.ChangeStatusAsync(id, transition, ownerId, HttpContext.TraceIdentifier, cancellationToken);
        return result switch
        {
            InstallationStatusChangeResult.NotFound => NotFound(),
            InstallationStatusChangeResult.Unchanged => Redirect(
                InstallationStatusNotice.DetailPathWithNotice(id, InstallationStatusService.TargetOf(transition))),
            _ => Redirect($"/installations/{id:D}"),
        };
    }
}
