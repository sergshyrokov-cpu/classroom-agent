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
/// The «Стан даних» page and the Admin's confirmation that courses were deleted in Classroom (US-062 openapi; spec
/// FR-004 … FR-006). Viewing is under <c>ViewDataHealth</c> (Admin, Dean), the confirmation under
/// <c>ConfirmCoursesDeleted</c> (Admin) — api-design §2.2.
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3): the problems, their shaping for the role, the read-only guard, VR-001 and VR-002 are in
/// <c>Application</c>. A success ends in Post-Redirect-Get with the count in TempData; a refusal re-renders the page as it
/// now is (api-design §2.6); a <see cref="ClassroomAgent.Application.Exceptions.ReadOnlyModeException"/> is left to the
/// host's exception handler, which answers <c>409</c>.
/// </remarks>
public sealed class DataHealthController(
    GetDataHealthQuery page,
    ConfirmCoursesDeletedUseCase confirm,
    ILogger<DataHealthController> logger) : Controller
{
    public const string CourseIdsField = "courseIds";

    [HttpGet(SignInRoutes.DataHealth)]
    [Authorize(Policy = InstallationPolicies.ViewDataHealth)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await page.ExecuteAsync(ActorRole(), cancellationToken);
        if (MessageOf(TempData[SignInRoutes.DataHealthMessageTempDataKey] as string) is { } confirmedCount)
        {
            model = model with { Message = DataHealthMessage.Confirmed, ConfirmedCount = confirmedCount };
        }

        return View("Index", model);
    }

    [HttpPost(SignInRoutes.DataHealthCourseDeletionConfirmation)]
    [Authorize(Policy = InstallationPolicies.ConfirmCoursesDeleted)]
    public async Task<IActionResult> CourseDeletionConfirmation(CancellationToken cancellationToken)
    {
        var adminId = ActorId();
        var form = new CourseDeletionConfirmationForm
        {
            CourseIds = Request.HasFormContentType && Request.Form.TryGetValue(CourseIdsField, out var values)
                ? values.Select(v => v ?? string.Empty).ToList()
                : null,
        };

        var result = await confirm.ExecuteAsync(adminId, form, HttpContext.TraceIdentifier, cancellationToken);
        switch (result.Outcome)
        {
            case CourseDeletionConfirmationOutcome.Confirmed:
                DataHealthLog.CoursesConfirmedDeleted(logger, adminId, result.ConfirmedCount);
                TempData[SignInRoutes.DataHealthMessageTempDataKey] =
                    result.ConfirmedCount.ToString(CultureInfo.InvariantCulture);
                return Redirect(SignInRoutes.DataHealth);

            case CourseDeletionConfirmationOutcome.Invalid:
                DataHealthLog.ConfirmationRefused(logger, adminId, "VR-001");
                return await PageAsync(DataHealthMessage.Invalid, StatusCodes.Status400BadRequest, cancellationToken);

            case CourseDeletionConfirmationOutcome.ListChanged:
                DataHealthLog.ConfirmationRefused(logger, adminId, "VR-002");
                return await PageAsync(DataHealthMessage.ListChanged, StatusCodes.Status409Conflict, cancellationToken);

            default:
                throw new InvalidOperationException("The outcome is not mapped to a response.");
        }
    }

    /// <summary>Api-design §2.6: a refusal shows the page as it now is, with the one-line message.</summary>
    private async Task<IActionResult> PageAsync(DataHealthMessage message, int status, CancellationToken cancellationToken)
    {
        var model = await page.ExecuteAsync(ActorRole(), cancellationToken);
        Response.StatusCode = status;
        return View("Index", model with { Message = message });
    }

    /// <summary>The stored count of a confirmation; anything else is ignored.</summary>
    private static int? MessageOf(string? stored) =>
        int.TryParse(stored, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count > 0 ? count : null;

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
