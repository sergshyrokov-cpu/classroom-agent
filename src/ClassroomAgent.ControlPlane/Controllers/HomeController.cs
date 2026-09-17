using ClassroomAgent.ControlPlane.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>The home page: the signed-in state, sign-out and the link to the installations (US-001 FR-013; US-002 FR-002).</summary>
[Authorize(Policy = OwnerSession.OwnerPolicy)]
public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View("~/Views/Home/Index.cshtml");
}
