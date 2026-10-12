using System.Globalization;
using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The teacher list (US-022 openapi <c>GET /workspace/teachers</c>): the course list's <c>ViewCourses</c> policy for the
/// §2 row "Просмотр курсов и участников", Admin and Dean (spec FR-012).
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3): every occurrence of <c>status</c> and <c>page</c> is handed to the query, which decides
/// "exactly one value" (api-design §2.3). A refusal answers <c>400</c> with the page itself, never the host's error page
/// (api-design §2.4). Other parameters are not collected and so never echoed (spec VR-003). Nothing is written and
/// nothing is guarded: viewing is permitted in read-only mode (spec FR-013).
/// </remarks>
[Authorize(Policy = InstallationPolicies.ViewCourses)]
public sealed class TeachersController(GetTeacherListQuery teachers, ILogger<TeachersController> logger) : Controller
{
    public const string StatusParameter = "status";

    public const string PageParameter = "page";

    [HttpGet(SignInRoutes.Teachers)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var request = new TeacherListRequest(
            Request.Query[StatusParameter].ToArray(), Request.Query[PageParameter].ToArray());

        var result = await teachers.ExecuteAsync(request, cancellationToken);
        var page = result.Page;

        if (result.Outcome == TeacherListPageOutcome.Invalid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            foreach (var key in page.MessageKeys)
            {
                TeacherListLog.Refused(logger, HttpContext.TraceIdentifier, ParameterOf(key), key.ToString());
            }
        }
        else if (page.List is { } list)
        {
            TeacherListLog.Shown(logger, ActorId(), page.Status.ToString(), list.Page, list.TotalCount);
        }

        return View(page);
    }

    /// <summary>Spec FR-016: the parameter a message key is about, for the log line.</summary>
    private static string ParameterOf(TeacherListMessageKey key) =>
        key == TeacherListMessageKey.StatusMalformed ? StatusParameter : PageParameter;

    /// <summary>The signed-in account from the session, never a request value (spec VR-004); the policy guarantees one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            CultureInfo.InvariantCulture);
}
