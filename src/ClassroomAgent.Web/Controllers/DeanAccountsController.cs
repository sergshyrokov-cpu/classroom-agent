using System.Globalization;
using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.Models;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The Admin's Dean accounts screen and the four actions of BR-014 (US-012 openapi; spec FR-011, FR-016).
/// Admin only — the §2 matrix row "Створення, відключення та включення, скидання пароля облікових записів
/// Деканів", its own policy.
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3). Every successful write answers with Post-Redirect-Get and its confirmation in
/// TempData, so a reload never repeats it; a refusal re-renders the screen. There is deliberately **no**
/// delete action anywhere: an account is never deleted (spec FR-010, BR-014).
/// </remarks>
[Authorize(Policy = InstallationPolicies.ManageDeanAccounts)]
public sealed class DeanAccountsController(
    ListDeanAccountsQuery list,
    GetLegitimacyModeQuery legitimacyMode,
    CreateDeanAccountUseCase create,
    SetDeanAccountStateUseCase setState,
    ResetDeanPasswordUseCase resetPassword) : Controller
{
    [HttpGet(SignInRoutes.Deans)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await PageAsync(null, TempData[SignInRoutes.MessageTempDataKey] as string, cancellationToken));

    [HttpPost(SignInRoutes.Deans)]
    public async Task<IActionResult> Create(
        [FromForm] string? email,
        [FromForm] string? temporaryPassword,
        CancellationToken cancellationToken)
    {
        var outcome = await create.ExecuteAsync(
            ActorId(),
            email ?? string.Empty,
            temporaryPassword ?? string.Empty,
            HttpContext.TraceIdentifier,
            cancellationToken);

        if (outcome.Refusal is null)
        {
            TempData[SignInRoutes.MessageTempDataKey] = DeanAccountTextKeys.CreatedConfirmation;
            return Redirect(SignInRoutes.Deans);
        }

        // The typed email is preserved so it can be corrected; the password never is (spec VR-006, S-10).
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(nameof(Index), await PageAsync(email, MessageOf(outcome), cancellationToken));
    }

    [HttpPost(SignInRoutes.DeanState)]
    public async Task<IActionResult> SetState(
        long deanId,
        [FromForm] string? desiredState,
        CancellationToken cancellationToken)
    {
        if (!TryParseState(desiredState, out var disable))
        {
            return BadRequest();
        }

        var outcome = await setState.ExecuteAsync(
            ActorId(),
            deanId,
            disable,
            HttpContext.TraceIdentifier,
            cancellationToken);

        return await AnswerAsync(
            outcome,
            disable ? DeanAccountTextKeys.DisabledConfirmation : DeanAccountTextKeys.ReEnabledConfirmation,
            null,
            cancellationToken);
    }

    [HttpPost(SignInRoutes.DeanPassword)]
    public async Task<IActionResult> ResetPassword(
        long deanId,
        [FromForm] string? temporaryPassword,
        CancellationToken cancellationToken)
    {
        var outcome = await resetPassword.ExecuteAsync(
            ActorId(),
            deanId,
            temporaryPassword ?? string.Empty,
            HttpContext.TraceIdentifier,
            cancellationToken);

        return await AnswerAsync(
            outcome,
            DeanAccountTextKeys.PasswordResetConfirmation,
            null,
            cancellationToken);
    }

    /// <summary>
    /// The shared answer of the two actions on one account: `404` when the id names no Dean — never `403`,
    /// which would announce that the id exists (api-design §2.7) — `409` for a stale screen, `400` for a
    /// password the policy refused, and Post-Redirect-Get on success.
    /// </summary>
    private async Task<IActionResult> AnswerAsync(
        DeanAccountActionOutcome outcome,
        string confirmationKey,
        string? emailInput,
        CancellationToken cancellationToken)
    {
        switch (outcome.Refusal)
        {
            case null:
                TempData[SignInRoutes.MessageTempDataKey] = confirmationKey;
                return Redirect(SignInRoutes.Deans);

            case DeanAccountRefusal.NoSuchDeanAccount:
                return NotFound();

            case DeanAccountRefusal.AlreadyInThatState:
                Response.StatusCode = StatusCodes.Status409Conflict;
                return View(nameof(Index), await PageAsync(emailInput, MessageOf(outcome), cancellationToken));

            default:
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return View(nameof(Index), await PageAsync(emailInput, MessageOf(outcome), cancellationToken));
        }
    }

    private static string MessageOf(DeanAccountActionOutcome outcome) =>
        outcome.Violation is { } violation
            ? DeanAccountTextKeys.Of(violation)
            : DeanAccountTextKeys.Of(outcome.Refusal!.Value);

    private static bool TryParseState(string? desiredState, out bool disable)
    {
        disable = string.Equals(desiredState, "Disabled", StringComparison.Ordinal);
        return disable || string.Equals(desiredState, "Active", StringComparison.Ordinal);
    }

    private async Task<DeanAccountsPageModel> PageAsync(
        string? emailInput,
        string? messageKey,
        CancellationToken cancellationToken)
    {
        var deans = await list.ExecuteAsync(cancellationToken);
        var mode = await legitimacyMode.ExecuteAsync(cancellationToken);
        return new DeanAccountsPageModel(deans, mode.IsReadOnly, mode.Reason, emailInput, messageKey);
    }

    private long ActorId() =>
        long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
}
