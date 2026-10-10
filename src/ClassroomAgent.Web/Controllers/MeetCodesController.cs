using System.Globalization;
using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The Meet meetings page, the course-choice form and the three link writes (US-032 openapi; spec FR-007 … FR-015).
/// Viewing is under <c>ViewMeetCodes</c>; the form and every write under <c>LinkMeetCodes</c> (api-design §2.10).
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3): every rule — the shape of the code and the form, the read-only guard, the stale-state
/// check — is in <c>Application</c>. A success ends in Post-Redirect-Get with the confirmation in TempData; a
/// <see cref="ClassroomAgent.Application.Exceptions.ReadOnlyModeException"/> is left to the host's exception handler,
/// which answers <c>409</c> (api-design §2.7).
/// </remarks>
public sealed class MeetCodesController(
    GetMeetCodesQuery page,
    GetMeetCodeCourseChoiceQuery choices,
    SetMeetCodeCourseUseCase setCourse,
    ConfirmMeetCodeLinkUseCase confirm,
    MarkMeetCodeNotACourseUseCase mark) : Controller
{
    public const string ListParameter = "list";

    public const string PageParameter = "page";

    public const string SizeParameter = "size";

    public const string ReturnPageParameter = "returnPage";

    [HttpGet(SignInRoutes.MeetCodes)]
    [Authorize(Policy = InstallationPolicies.ViewMeetCodes)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var request = new MeetCodesRequest(
            Request.Query[ListParameter].ToArray(),
            Request.Query[PageParameter].ToArray(),
            Request.Query[SizeParameter].ToArray());

        var result = await page.ExecuteAsync(
            request, ConfirmationOf(TempData[SignInRoutes.MeetCodeMessageTempDataKey] as string), cancellationToken);
        if (result.QueryInvalid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        return View("Index", result.Model);
    }

    [HttpGet(SignInRoutes.MeetCodeCourseChoice)]
    [Authorize(Policy = InstallationPolicies.LinkMeetCodes)]
    public async Task<IActionResult> CourseChoice(string? meetingCode, CancellationToken cancellationToken)
    {
        var returnPage = Request.Query[ReturnPageParameter].FirstOrDefault();
        return await ChoiceOrPageAsync(meetingCode, returnPage, null, StatusCodes.Status200OK, cancellationToken);
    }

    [HttpPost(SignInRoutes.MeetCodeLink)]
    [Authorize(Policy = InstallationPolicies.LinkMeetCodes)]
    public async Task<IActionResult> Link(string? meetingCode, CancellationToken cancellationToken) =>
        await AnswerAsync(
            await setCourse.ExecuteAsync(
                ActorId(), ActorRole(), meetingCode, FormInput(), HttpContext.TraceIdentifier, cancellationToken),
            meetingCode,
            cancellationToken);

    [HttpPost(SignInRoutes.MeetCodeConfirmation)]
    [Authorize(Policy = InstallationPolicies.LinkMeetCodes)]
    public async Task<IActionResult> Confirmation(string? meetingCode, CancellationToken cancellationToken) =>
        await AnswerAsync(
            await confirm.ExecuteAsync(
                ActorId(), ActorRole(), meetingCode, FormInput(), HttpContext.TraceIdentifier, cancellationToken),
            meetingCode,
            cancellationToken);

    [HttpPost(SignInRoutes.MeetCodeNotACourseMark)]
    [Authorize(Policy = InstallationPolicies.LinkMeetCodes)]
    public async Task<IActionResult> NotACourseMark(string? meetingCode, CancellationToken cancellationToken) =>
        await AnswerAsync(
            await mark.ExecuteAsync(
                ActorId(), ActorRole(), meetingCode, FormInput(), HttpContext.TraceIdentifier, cancellationToken),
            meetingCode,
            cancellationToken);

    /// <summary>Api-design §2.7: one status per outcome; a success redirects to the list the code came from.</summary>
    private async Task<IActionResult> AnswerAsync(MeetCodeChangeResult result, string? meetingCode, CancellationToken cancellationToken)
    {
        switch (result.Outcome)
        {
            case MeetCodeChangeOutcome.CoursePicked:
                return Done(result, MeetCodesMessageKey.CoursePicked);
            case MeetCodeChangeOutcome.Relinked:
                return Done(result, MeetCodesMessageKey.Relinked);
            case MeetCodeChangeOutcome.MarkRemoved:
                return Done(result, MeetCodesMessageKey.MarkRemoved);
            case MeetCodeChangeOutcome.Confirmed:
                return Done(result, MeetCodesMessageKey.Confirmed);
            case MeetCodeChangeOutcome.Marked:
                return Done(result, MeetCodesMessageKey.Marked);

            case MeetCodeChangeOutcome.Malformed:
                return ErrorController.Page(HttpContext, StatusCodes.Status400BadRequest, SignInRoutes.MeetCodes);

            case MeetCodeChangeOutcome.SameCourse:
                return await ChoiceOrPageAsync(
                    meetingCode,
                    result.ReturnPage.ToString(CultureInfo.InvariantCulture),
                    MeetCodeFieldError.SameCourse,
                    StatusCodes.Status400BadRequest,
                    cancellationToken);

            case MeetCodeChangeOutcome.CourseNotFound:
                return await ChoiceOrPageAsync(
                    meetingCode,
                    result.ReturnPage.ToString(CultureInfo.InvariantCulture),
                    MeetCodeFieldError.CourseNotFound,
                    StatusCodes.Status404NotFound,
                    cancellationToken);

            case MeetCodeChangeOutcome.CodeNotFound:
                return await PageAsync(
                    MeetCodeList.Unassigned, 0, MeetCodesMessageKey.CodeNotFound, StatusCodes.Status404NotFound, cancellationToken);

            case MeetCodeChangeOutcome.StateChanged:
                return await PageAsync(
                    result.List, 0, MeetCodesMessageKey.StateChanged, StatusCodes.Status409Conflict, cancellationToken);

            default:
                throw new InvalidOperationException("The outcome is not mapped to a response.");
        }
    }

    private async Task<IActionResult> ChoiceOrPageAsync(
        string? meetingCode,
        string? returnPage,
        MeetCodeFieldError? fieldError,
        int status,
        CancellationToken cancellationToken)
    {
        var result = await choices.ExecuteAsync(meetingCode, returnPage, fieldError, cancellationToken);
        if (result.Malformed)
        {
            return ErrorController.Page(HttpContext, StatusCodes.Status400BadRequest, SignInRoutes.MeetCodes);
        }

        if (result.Model is not { } model)
        {
            return await PageAsync(
                MeetCodeList.Unassigned, 0, MeetCodesMessageKey.CodeNotFound, StatusCodes.Status404NotFound, cancellationToken);
        }

        Response.StatusCode = status;
        return View("CourseChoice", model);
    }

    private async Task<IActionResult> PageAsync(
        MeetCodeList list, int pageNumber, MeetCodesMessageKey message, int status, CancellationToken cancellationToken)
    {
        var model = await page.ExecuteAsync(list, pageNumber, message, cancellationToken);
        Response.StatusCode = status;
        return View("Index", model);
    }

    private RedirectResult Done(MeetCodeChangeResult result, MeetCodesMessageKey message)
    {
        TempData[SignInRoutes.MeetCodeMessageTempDataKey] = message.ToString();
        return Redirect(
            SignInRoutes.MeetCodes + "?" + ListParameter + "=" + ListText(result.List) + "&" + PageParameter + "="
            + result.ReturnPage.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The list as the query string spells it (openapi <c>MeetCodeList</c>).</summary>
    public static string ListText(MeetCodeList list) => list switch
    {
        MeetCodeList.Unassigned => "unassigned",
        MeetCodeList.Linked => "linked",
        MeetCodeList.NotACourse => "not-a-course",
        _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
    };

    private static MeetCodesMessageKey? ConfirmationOf(string? stored) =>
        Enum.TryParse<MeetCodesMessageKey>(stored, out var key) && Enum.IsDefined(key) ? key : null;

    /// <summary>Every posted name/value pair, in order, without the antiforgery field — the use case decides what is malformed.</summary>
    private MeetCodeFormInput FormInput() =>
        new(Request.Form
            .Where(field => !string.Equals(field.Key, "__RequestVerificationToken", StringComparison.Ordinal))
            .SelectMany(field => field.Value.Select(value => new KeyValuePair<string, string?>(field.Key, value)))
            .ToList());

    /// <summary>The signed-in account from the session, never a request value; the policy guarantees one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            CultureInfo.InvariantCulture);

    private AppRole ActorRole() =>
        Enum.TryParse<AppRole>(User.FindFirstValue(ClaimTypes.Role), out var role) && Enum.IsDefined(role)
            ? role
            : throw new InvalidOperationException("An authorized request carries no known role.");
}
