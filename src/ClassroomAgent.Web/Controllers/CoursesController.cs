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
/// The course list (US-020 openapi <c>GET /workspace/courses</c>): its own policy for the §2 row "Просмотр курсов и
/// участников", Admin and Dean (api-design §2.10).
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3): every occurrence of each of the four parameters is handed to the query, which decides
/// "exactly one value" (api-design §2.3). A refusal answers <c>400</c> or <c>404</c> with the page itself, never the
/// host's error page (api-design §2.4). Unknown parameters are not collected and so never echoed (spec VR-005).
/// Nothing is written and nothing is guarded: viewing is permitted in read-only mode (spec FR-015).
/// </remarks>
[Authorize(Policy = InstallationPolicies.ViewCourses)]
public sealed class CoursesController(GetCourseListQuery courses, ILogger<CoursesController> logger) : Controller
{
    public const string StatusParameter = "status";

    public const string TeacherIdParameter = "teacherId";

    public const string SearchParameter = "q";

    public const string PageParameter = "page";

    [HttpGet(SignInRoutes.Courses)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var request = new CourseListRequest(
            Request.Query[StatusParameter].ToArray(),
            Request.Query[TeacherIdParameter].ToArray(),
            Request.Query[SearchParameter].ToArray(),
            Request.Query[PageParameter].ToArray());

        var result = await courses.ExecuteAsync(request, cancellationToken);
        var page = result.Page;

        switch (result.Outcome)
        {
            case CourseListPageOutcome.Invalid:
            case CourseListPageOutcome.TeacherUnknown:
                Response.StatusCode = result.Outcome == CourseListPageOutcome.Invalid
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status404NotFound;
                foreach (var key in page.MessageKeys)
                {
                    CourseListLog.Refused(logger, HttpContext.TraceIdentifier, ParameterOf(key), key.ToString());
                }

                break;
            case CourseListPageOutcome.Shown when page.List is { } list:
                CourseListLog.Shown(
                    logger,
                    ActorId(),
                    page.Status.ToString(),
                    page.SelectedTeacherId,
                    page.SearchText is not null,
                    list.Page,
                    list.TotalCount);
                break;
        }

        return View(page);
    }

    /// <summary>Spec FR-018: the parameter a message key is about, for the log line.</summary>
    private static string ParameterOf(CourseListMessageKey key) => key switch
    {
        CourseListMessageKey.StatusMalformed => StatusParameter,
        CourseListMessageKey.TeacherMalformed or CourseListMessageKey.TeacherUnknown => TeacherIdParameter,
        CourseListMessageKey.PageMalformed => PageParameter,
        _ => SearchParameter,
    };

    /// <summary>The signed-in account from the session, never a request value (spec VR-006); the policy guarantees one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            CultureInfo.InvariantCulture);
}
