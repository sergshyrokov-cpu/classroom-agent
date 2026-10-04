using System.Globalization;
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
/// The on-screen report of one template for one course and period (US-027 openapi <c>GET /reports</c>; spec FR-005,
/// FR-011, FR-012): its own policy for the §2 row "Использование шаблонов отчётов", Admin and Dean.
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3), the pattern of <see cref="JournalController"/>: every occurrence of the four query
/// parameters is handed to the query, which decides "exactly one value". A refused query answers <c>400</c> or
/// <c>404</c> with this same page — the form and the messages — never the host's error page (api-design §2.5).
/// Unknown parameters are not bound and so never echoed. Nothing is written and nothing is guarded: viewing is
/// permitted in read-only mode (spec FR-013).
/// </remarks>
[Authorize(Policy = InstallationPolicies.UseReportTemplates)]
public sealed class ReportController(GetReportQuery report, ILogger<ReportController> logger) : Controller
{
    public const string TemplateParameter = "template";

    public const string CourseIdParameter = "courseId";

    public const string FromParameter = "from";

    public const string ToParameter = "to";

    [HttpGet(SignInRoutes.Report)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var request = new ReportRequest(
            Request.Query[TemplateParameter].ToArray(),
            Request.Query[CourseIdParameter].ToArray(),
            Request.Query[FromParameter].ToArray(),
            Request.Query[ToParameter].ToArray());

        var result = await report.ExecuteAsync(request, CultureInfo.CurrentUICulture, cancellationToken);
        var page = result.Page;

        switch (result.Outcome)
        {
            case ReportPageOutcome.Invalid:
                Response.StatusCode = StatusCodes.Status400BadRequest;
                ReportTemplateLog.QueryRefused(logger, HttpContext.TraceIdentifier, string.Join(", ", page.MessageKeys));
                break;
            case ReportPageOutcome.NotFound:
                Response.StatusCode = StatusCodes.Status404NotFound;
                ReportTemplateLog.QueryRefused(logger, HttpContext.TraceIdentifier, string.Join(", ", page.MessageKeys));
                break;
            case ReportPageOutcome.Shown when page.SelectedCourseId is { } courseId && page.Report is { } built:
                ReportTemplateLog.Built(
                    logger,
                    page.SelectedTemplate,
                    courseId,
                    page.From,
                    page.To,
                    ActorId(),
                    built.Grading?.Rows.Count ?? 0,
                    built.Grading?.Columns.Count ?? 0,
                    built.LessonTopics?.Rows.Count ?? 0);
                break;
        }

        return View(page);
    }

    /// <summary>Spec VR-008: the signed-in account from the session, never a request value; the policy guarantees one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            CultureInfo.InvariantCulture);
}
