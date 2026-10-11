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
/// The Meet reports (US-033 openapi <c>GET /workspace/meet-reports</c> and
/// <c>GET /workspace/meet-reports/meetings/{meetingId}</c>): one policy for the §2 row "Просмотр и экспорт статистики
/// Meet-занятий", Admin and Dean (api-design §2.2).
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3): every occurrence of each query parameter is handed to the query, which decides "exactly
/// one value" (spec §6). A refusal answers <c>400</c> or <c>404</c> with the page itself, never the host's error page
/// (api-design §2.3). Unknown parameters are not bound and so never echoed (spec VR-004). Nothing is written and
/// nothing is guarded: viewing is permitted in read-only mode (spec FR-015).
/// </remarks>
[Authorize(Policy = InstallationPolicies.ViewMeetReports)]
public sealed class MeetReportsController(
    GetMeetReportsQuery reports,
    GetMeetReportMeetingQuery meeting,
    ILogger<MeetReportsController> logger) : Controller
{
    public const string CourseIdParameter = "courseId";

    public const string FromParameter = "from";

    public const string ToParameter = "to";

    public const string MeetingIdParameter = "meetingId";

    [HttpGet(SignInRoutes.MeetReports)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var request = new MeetReportsRequest(
            Request.Query[CourseIdParameter].ToArray(),
            Request.Query[FromParameter].ToArray(),
            Request.Query[ToParameter].ToArray());

        var result = await reports.ExecuteAsync(request, CultureInfo.CurrentUICulture, cancellationToken);
        var page = result.Page;

        switch (result.Outcome)
        {
            case MeetReportsPageOutcome.Invalid:
            case MeetReportsPageOutcome.CourseUnknown:
                Response.StatusCode = result.Outcome == MeetReportsPageOutcome.Invalid
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status404NotFound;
                MeetReportsLog.Refused(
                    logger,
                    HttpContext.TraceIdentifier,
                    string.Join(", ", page.MessageKeys.Select(ParameterOf).Distinct()),
                    string.Join(", ", page.MessageKeys));
                break;
            case MeetReportsPageOutcome.Shown when page.SelectedCourseId is { } courseId:
                MeetReportsLog.Built(
                    logger,
                    courseId,
                    page.From,
                    page.To,
                    ActorId(),
                    page.Reports?.Meetings.Count ?? 0,
                    page.Reports?.Students.Count ?? 0);
                break;
        }

        return View(page);
    }

    [HttpGet(SignInRoutes.MeetReportMeeting)]
    public async Task<IActionResult> Meeting(string meetingId, CancellationToken cancellationToken)
    {
        var request = new MeetReportMeetingRequest(
            meetingId,
            Request.Query[CourseIdParameter].ToArray(),
            Request.Query[FromParameter].ToArray(),
            Request.Query[ToParameter].ToArray());

        var result = await meeting.ExecuteAsync(request, CultureInfo.CurrentUICulture, cancellationToken);
        var page = result.Page;

        switch (result.Outcome)
        {
            case MeetReportMeetingOutcome.Invalid:
                Response.StatusCode = StatusCodes.Status400BadRequest;
                MeetReportsLog.MeetingRefused(
                    logger, HttpContext.TraceIdentifier, CourseIdParameter, nameof(MeetReportMeetingMessageKey.CourseMalformed));
                break;
            case MeetReportMeetingOutcome.NotFound:
                Response.StatusCode = StatusCodes.Status404NotFound;
                MeetReportsLog.MeetingRefused(
                    logger,
                    HttpContext.TraceIdentifier,
                    MeetingIdParameter + ", " + CourseIdParameter,
                    nameof(MeetReportMeetingMessageKey.MeetingNotFound));
                break;
            case MeetReportMeetingOutcome.Shown when page.Meeting is { } detail && page.BackLink.CourseId is { } courseId:
                MeetReportsLog.MeetingBuilt(
                    logger,
                    long.Parse(meetingId, NumberStyles.None, CultureInfo.InvariantCulture),
                    courseId,
                    ActorId(),
                    detail.People.Count);
                break;
        }

        return View(page);
    }

    /// <summary>Spec FR-013: the parameter a message key is about, for the log line.</summary>
    private static string ParameterOf(MeetReportsMessageKey key) => key switch
    {
        MeetReportsMessageKey.FromMalformed => FromParameter,
        MeetReportsMessageKey.ToMalformed => ToParameter,
        MeetReportsMessageKey.PeriodInverted => FromParameter + ", " + ToParameter,
        _ => CourseIdParameter,
    };

    /// <summary>The signed-in account from the session, never a request value; the policy guarantees one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            CultureInfo.InvariantCulture);
}
