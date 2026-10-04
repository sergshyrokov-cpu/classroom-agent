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
/// The journal of one course for a period (US-025 openapi <c>GET /workspace/journal</c>; spec FR-001, FR-012): its
/// own policy for the §2 row "Просмотр и экспорт журнала успеваемости", Admin and Dean.
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3): every occurrence of the four query parameters is handed to the query, which decides
/// "exactly one value" (spec §6). A refused query answers <c>400</c> or <c>404</c> with this same page — the form
/// and the message — never the host's error page (api-design §2.3). Unknown parameters are not bound and so never
/// echoed (spec VR-005). Nothing is written and nothing is guarded: viewing is permitted in read-only mode (FR-013).
/// </remarks>
[Authorize(Policy = InstallationPolicies.ViewJournal)]
public sealed class JournalController(GetJournalQuery journal, ILogger<JournalController> logger) : Controller
{
    public const string CourseIdParameter = "courseId";

    public const string FromParameter = "from";

    public const string ToParameter = "to";

    public const string ViewParameter = "view";

    [HttpGet(SignInRoutes.Journal)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var request = new JournalRequest(
            Request.Query[CourseIdParameter].ToArray(),
            Request.Query[FromParameter].ToArray(),
            Request.Query[ToParameter].ToArray(),
            Request.Query[ViewParameter].ToArray());

        var result = await journal.ExecuteAsync(request, CultureInfo.CurrentUICulture, cancellationToken);
        var page = result.Page;

        switch (result.Outcome)
        {
            case JournalPageOutcome.Invalid:
                Response.StatusCode = StatusCodes.Status400BadRequest;
                JournalLog.Refused(logger, HttpContext.TraceIdentifier, string.Join(", ", page.MessageKeys));
                break;
            case JournalPageOutcome.CourseUnknown:
                Response.StatusCode = StatusCodes.Status404NotFound;
                JournalLog.Refused(logger, HttpContext.TraceIdentifier, string.Join(", ", page.MessageKeys));
                break;
            case JournalPageOutcome.Shown when page.SelectedCourseId is { } courseId:
                JournalLog.Built(
                    logger,
                    courseId,
                    page.From,
                    page.To,
                    page.View,
                    ActorId(),
                    page.Journal?.Rows.Count ?? 0,
                    page.Journal?.Columns.Count ?? 0);
                break;
        }

        return View(page);
    }

    /// <summary>Spec VR-007: the signed-in account from the session, never a request value; the policy guarantees one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            CultureInfo.InvariantCulture);
}
