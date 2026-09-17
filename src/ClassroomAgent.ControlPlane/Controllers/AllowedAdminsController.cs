using ClassroomAgent.ControlPlane.Security;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The add-Admin form and the revoke confirmation of an installation (US-003 FR-002 … FR-010):
/// HTTP mapping only; the rules are in <see cref="AllowedAdminRegistry"/>. A non-UUID
/// <c>{id}</c> or <c>{adminId}</c> matches no route and reaches the 404 catch-all.
/// </summary>
[Authorize(Policy = OwnerSession.OwnerPolicy)]
[Route("installations/{id:guid}/admins")]
public sealed class AllowedAdminsController(AllowedAdminRegistry registry) : Controller
{
    private const string NewView = "~/Views/AllowedAdmins/New.cshtml";
    private const string RevocationView = "~/Views/AllowedAdmins/Revocation.cshtml";

    [HttpGet("new")]
    public async Task<IActionResult> New(Guid id, CancellationToken cancellationToken) =>
        await registry.GetAddFormAsync(id, cancellationToken) is { } installation
            ? View(NewView, new AddAllowedAdminPageModel(installation, null, null))
            : NotFound();

    [HttpPost]
    public async Task<IActionResult> Add(Guid id, [FromForm] AddAllowedAdminRequest request, CancellationToken cancellationToken)
    {
        // An unknown installation is 404 even when the submitted email is invalid (api-design §4).
        if (await registry.GetAddFormAsync(id, cancellationToken) is not { } installation)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Form(new AddAllowedAdminPageModel(installation, request.Email, null), StatusCodes.Status400BadRequest);
        }

        if (OwnerSession.OwnerId(User) is not { } ownerId)
        {
            return Forbid();
        }

        var result = await registry.AddAsync(id, request.Email!, ownerId, HttpContext.TraceIdentifier, cancellationToken);
        switch (result.Outcome)
        {
            case AddAllowedAdminOutcome.NotFound:
                return NotFound();
            case AddAllowedAdminOutcome.WrongDomain:
                return Form(new AddAllowedAdminPageModel(installation, request.Email, result.ExpectedDomain), StatusCodes.Status400BadRequest);
            case AddAllowedAdminOutcome.Taken:
                ModelState.AddModelError(nameof(AddAllowedAdminRequest.Email), "AllowedAdmin.Email.Taken");
                return Form(new AddAllowedAdminPageModel(installation, request.Email, null), StatusCodes.Status409Conflict);
            default:
                return Redirect(DetailPath(id));
        }
    }

    [HttpGet("{adminId:guid}/revocation")]
    public async Task<IActionResult> Revocation(Guid id, Guid adminId, CancellationToken cancellationToken) =>
        await registry.GetRevokeConfirmationAsync(id, adminId, cancellationToken) is { } confirmation
            ? View(RevocationView, confirmation)
            : NotFound();

    [HttpPost("{adminId:guid}/revocation")]
    public async Task<IActionResult> Revoke(Guid id, Guid adminId, CancellationToken cancellationToken)
    {
        if (OwnerSession.OwnerId(User) is not { } ownerId)
        {
            return Forbid();
        }

        var result = await registry.RevokeAsync(id, adminId, ownerId, HttpContext.TraceIdentifier, cancellationToken);
        return result == RevokeAllowedAdminResult.Revoked ? Redirect(DetailPath(id)) : NotFound();
    }

    private static string DetailPath(Guid identifier) => $"/installations/{identifier:D}";

    private ViewResult Form(AddAllowedAdminPageModel model, int statusCode)
    {
        var view = View(NewView, model);
        view.StatusCode = statusCode;
        return view;
    }
}
