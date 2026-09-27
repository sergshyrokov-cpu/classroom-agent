using System.Globalization;
using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.Models;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The Dean's two password forms (US-012 openapi; spec FR-006, FR-014): the forced change after step 5 of the
/// sign-in sequence, and the voluntary change later. They are separate paths on purpose — the forced one asks
/// for no current password, because it was supplied seconds earlier, and it is reachable only by a session that
/// has just passed step 5 (api-design §2.5).
/// </summary>
public sealed class DeanPasswordController(
    CompleteTemporaryPasswordChangeUseCase forcedChange,
    ChangeOwnPasswordUseCase changeOwn,
    IAppUserRepository users,
    TimeProvider timeProvider) : Controller
{
    /// <summary>
    /// The forced change form (api-design §2.6). It answers only for a session that has just passed step 5: with
    /// no session at all the challenge sends the visitor to the sign-in page, and a session whose password is
    /// no longer temporary is sent to the landing page — so the form can never change a password without the
    /// old one having just been proved.
    /// </summary>
    [HttpGet(SignInRoutes.ForcedPasswordChange)]
    [Authorize(Policy = InstallationPolicies.AuthenticatedUser)]
    public IActionResult Forced() =>
        User.FindFirst(InstallationClaimTypes.PasswordIsTemporary) is null
            ? Redirect(SignInRoutes.Landing)
            : View(new ChangePasswordPageModel(true, null));

    [HttpPost(SignInRoutes.ForcedPasswordChange)]
    [Authorize(Policy = InstallationPolicies.CompleteTemporaryPasswordChange)]
    public async Task<IActionResult> CompleteForced(
        [FromForm] string? newPassword,
        CancellationToken cancellationToken)
    {
        var deanId = ActorId();
        var outcome = await forcedChange.ExecuteAsync(
            deanId,
            newPassword ?? string.Empty,
            HttpContext.TraceIdentifier,
            cancellationToken);

        if (outcome.Refused)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(nameof(Forced), new ChangePasswordPageModel(true, MessageOf(outcome)));
        }

        // The temporary mark is gone, so the session is re-issued without the claim that restricted it.
        await ReIssueSessionAsync(deanId, cancellationToken);

        return Redirect(SignInRoutes.Landing);
    }

    [HttpGet(SignInRoutes.OwnPassword)]
    [Authorize(Policy = InstallationPolicies.ChangeOwnPassword)]
    public IActionResult Own() =>
        View(new ChangePasswordPageModel(false, TempData[SignInRoutes.MessageTempDataKey] as string));

    [HttpPost(SignInRoutes.OwnPassword)]
    [Authorize(Policy = InstallationPolicies.ChangeOwnPassword)]
    public async Task<IActionResult> ChangeOwn(
        [FromForm] string? currentPassword,
        [FromForm] string? newPassword,
        CancellationToken cancellationToken)
    {
        var outcome = await changeOwn.ExecuteAsync(
            ActorId(),
            currentPassword ?? string.Empty,
            newPassword ?? string.Empty,
            HttpContext.TraceIdentifier,
            cancellationToken);

        if (outcome.Refused)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(nameof(Own), new ChangePasswordPageModel(false, MessageOf(outcome)));
        }

        // The change rotated the security stamp, so the cookie the Dean is holding no longer authenticates
        // (spec FR-019). Re-issue the session — as the forced change does — or the Post-Redirect-Get would
        // land on a page the Dean can no longer reach and the confirmation would never be shown.
        await ReIssueSessionAsync(ActorId(), cancellationToken);

        TempData[SignInRoutes.MessageTempDataKey] = DeanAccountTextKeys.OwnPasswordChanged;
        return Redirect(SignInRoutes.OwnPassword);
    }

    /// <summary>
    /// Issues a session carrying the account's **new** security stamp, and without the temporary-password
    /// claim. Both password changes need it: each rotates the stamp, which the per-request check compares
    /// with the one in the cookie (US-008 AC-014, US-012 spec FR-019).
    /// </summary>
    private async Task ReIssueSessionAsync(long deanId, CancellationToken cancellationToken)
    {
        var dean = await users.FindDeanByIdAsync(deanId, cancellationToken);
        if (dean is null)
        {
            return;
        }

        await InstallationSession.SignInAsync(
            HttpContext,
            new SignedInUser(dean.Id, dean.Email, dean.Role, dean.UiLanguage, dean.SecurityStamp),
            timeProvider);
    }

    private static string MessageOf(PasswordChangeOutcome outcome) =>
        outcome.WrongCurrentPassword
            ? DeanAccountTextKeys.OwnPasswordWrongCurrent
            : outcome.Violation is { } violation
                ? DeanAccountTextKeys.Of(violation)
                : DeanAccountTextKeys.OwnPasswordWrongCurrent;

    private long ActorId() =>
        long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
}
