using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The landing page a signed-in user reaches (US-008 spec FR-019): who they are, their role, and the
/// installation's legitimacy status with the read-only reason when it applies. Reachable by Admin <b>and</b> Dean,
/// from the <c>trebovaniya.md</c> §2 permission matrix row "Просмотр статуса легитимности".
/// </summary>
/// <remarks>
/// The status is read through <c>Application</c> and never recomputed in the view (AD-3, AD-6); the view receives a
/// DTO, never a domain entity (AD-8). The signed-in user's email and role come from the session claims, so the page
/// needs no query of its own (db-design §3.4).
/// </remarks>
[Authorize(Policy = InstallationPolicies.ViewLegitimacyStatus)]
public sealed class HomeController(GetLegitimacyModeQuery legitimacyMode) : Controller
{
    [HttpGet(SignInRoutes.Landing)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var mode = await legitimacyMode.ExecuteAsync(cancellationToken);
        var role = User.IsInRole(InstallationSession.RoleName(AppRole.Dean)) ? AppRole.Dean : AppRole.Admin;
        return View(new LandingPageModel(
            User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            role == AppRole.Dean ? "Landing.Role.Dean" : "Landing.Role.Admin",
            mode.IsReadOnly,
            mode.Reason,
            mode.LastSuccessfulCheckAt));
    }
}
