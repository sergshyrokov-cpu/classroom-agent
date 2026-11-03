using System.Globalization;
using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The results page of the header search (US-071 openapi <c>GET /workspace/search</c>): <c>ViewCourses</c>, Admin and Dean, as
/// both also hold <c>ViewMeetCodes</c> (spec section 7).
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3): every occurrence of <c>q</c> goes to the query, which decides. A refusal answers <c>400</c> with the
/// page itself (VR-002). Other parameters are not collected and so never echoed (VR-004). Viewing is permitted in read-only
/// mode. The log lines carry counts or the parameter's name, never the text (SC-10, SC-14, api-design D-10).
/// </remarks>
[Authorize(Policy = InstallationPolicies.ViewCourses)]
public sealed class SearchController(GetSearchResultsQuery search, ILogger<SearchController> logger) : Controller
{
    public const string SearchParameter = "q";

    [HttpGet(SignInRoutes.Search)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var page = await search.ExecuteAsync(new SearchResultsRequest(Request.Query[SearchParameter].ToArray()), cancellationToken);

        if (page.State == SearchResultsState.Refused)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            SearchResultsLog.Refused(logger, HttpContext.TraceIdentifier, SearchParameter, page.Refusal.ToString() ?? string.Empty);
        }
        else if (page.State == SearchResultsState.Results)
        {
            SearchResultsLog.Shown(
                logger, ActorId(), page.Students.Total, page.Teachers.Total, page.Courses.Total, page.MeetCodes.Total);
        }

        return View(page);
    }

    /// <summary>The signed-in account from the session, never a request value; the policy guarantees one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            CultureInfo.InvariantCulture);
}
