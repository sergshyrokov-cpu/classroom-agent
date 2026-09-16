using ClassroomAgent.ControlPlane.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>The home page: the signed-in state and sign-out, nothing else (FR-013, spec I-8).</summary>
[Authorize(Policy = OwnerSession.OwnerPolicy)]
public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View("~/Views/Home/Index.cshtml");
}
