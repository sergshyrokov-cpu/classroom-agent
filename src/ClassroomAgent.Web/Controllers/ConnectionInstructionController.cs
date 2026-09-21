using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The connection instruction for the school's super-admin (US-010 openapi; spec FR-007, FR-011). Admin only —
/// the §2 permission-matrix row "Просмотр инструкции по подключению", ✔ Admin, ✘ Dean (v39).
/// </summary>
/// <remarks>
/// One <c>GET</c> and nothing else: no <c>POST</c>, no download endpoint (OD-002), so this Story adds no
/// antiforgery concern and answers no <c>400</c> and no <c>409</c> (api-design §2.1). The operation takes no
/// parameter; a query string is ignored, because there is nothing to bind it to (spec VR-001).
///
/// The policy is its own, not US-009's: §2 split the two matrix rows because one is a write read-only mode
/// blocks and the other a read it permits (spec I-7).
/// </remarks>
[Authorize(Policy = InstallationPolicies.ViewConnectionInstruction)]
public sealed class ConnectionInstructionController(
    GetConnectionInstructionQuery instruction,
    GetLegitimacyModeQuery legitimacyMode) : Controller
{
    [HttpGet(SignInRoutes.ConnectionInstruction)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var view = await instruction.ExecuteAsync(cancellationToken);
        var mode = await legitimacyMode.ExecuteAsync(cancellationToken);

        return View(new ConnectionInstructionPageModel(
            view.State,
            view.InstallationDomain,
            view.ServiceAccountClientId,
            view.Scopes,
            mode.IsReadOnly,
            mode.Reason));
    }
}
