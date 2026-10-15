using System.Globalization;
using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Application.Validation;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The Admin's erasure of one participant's data and the lifting of an exclusion (US-038 openapi; spec FR-001 …
/// FR-009, FR-015). Admin only — the §2 matrix row "Удаление всех данных участника … и снятие исключения", its own
/// policy.
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3). The search term and the email to lift travel in POST bodies and are never logged (S-09,
/// SC-10). A successful erasure or lifting answers with Post-Redirect-Get and a one-time message in TempData; a read-only
/// refusal is the use case's <see cref="Application.Exceptions.ReadOnlyModeException"/>, which the host handler maps to
/// <c>409</c> (AD-6, api-design §2.4).
/// </remarks>
[Authorize(Policy = InstallationPolicies.EraseParticipantData)]
public sealed class ErasureController(
    GetLegitimacyModeQuery legitimacyMode,
    SearchParticipantsForErasureQuery search,
    GetErasureConfirmationQuery confirmation,
    EraseParticipantUseCase erase,
    LiftErasureExclusionUseCase lift,
    ILogger<ErasureController> logger) : Controller
{
    private static readonly SearchBlock NoSearch = new(null, null, false, [], false);

    [HttpGet(SignInRoutes.Erasure)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var message = Enum.TryParse<ErasureMessageKey>(TempData[SignInRoutes.ErasureMessageTempDataKey] as string, out var key)
            ? key
            : (ErasureMessageKey?)null;
        return View(await PageAsync(message, NoSearch, LiftBlock.Empty, cancellationToken));
    }

    [HttpPost(SignInRoutes.ErasureSearches)]
    public async Task<IActionResult> Search([FromForm] string? term, CancellationToken cancellationToken)
    {
        var block = await search.ExecuteAsync(term, cancellationToken);
        if (block.FieldError is not null)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        return View(nameof(Index), await PageAsync(null, block, LiftBlock.Empty, cancellationToken));
    }

    [HttpGet(SignInRoutes.ErasureConfirmation)]
    public async Task<IActionResult> Confirmation(string participantId, CancellationToken cancellationToken)
    {
        if (!TryParseId(participantId, out var id))
        {
            return NotFound();
        }

        var page = await confirmation.ExecuteAsync(id, cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpPost(SignInRoutes.ErasureErase)]
    public async Task<IActionResult> Erase(
        string participantId,
        [FromForm] string? irreversibleConfirmed,
        CancellationToken cancellationToken)
    {
        if (!TryParseId(participantId, out var id))
        {
            return NotFound();
        }

        var adminId = ActorId();
        var result = await erase.ExecuteAsync(
            adminId,
            id,
            ErasureMark.IsSet(irreversibleConfirmed),
            HttpContext.TraceIdentifier,
            cancellationToken);

        switch (result.Outcome)
        {
            case ParticipantErasureOutcome.Erased when result.Counts is { } counts:
                ErasureLog.ParticipantErased(
                    logger,
                    id,
                    adminId,
                    counts.Memberships,
                    counts.Submissions,
                    counts.MeetParticipations,
                    counts.ClearedMeetOrganizers,
                    counts.ClearedCourseOwners);
                TempData[SignInRoutes.ErasureMessageTempDataKey] = ErasureMessageKey.Erased.ToString();
                return Redirect(SignInRoutes.Erasure);

            case ParticipantErasureOutcome.IrreversibleMarkRequired:
                var page = await confirmation.ExecuteAsync(id, cancellationToken);
                if (page is null)
                {
                    return NotFound();
                }

                Response.StatusCode = StatusCodes.Status400BadRequest;
                return View(nameof(Confirmation), page with { MarkError = ErasureMarkError.IrreversibleMarkRequired });

            default:
                return NotFound();
        }
    }

    [HttpPost(SignInRoutes.ErasureLiftings)]
    public async Task<IActionResult> Lift([FromForm] string? email, CancellationToken cancellationToken)
    {
        var result = await lift.ExecuteAsync(ActorId(), email, HttpContext.TraceIdentifier, cancellationToken);
        switch (result.Outcome)
        {
            case ExclusionLiftingOutcome.Lifted:
                TempData[SignInRoutes.ErasureMessageTempDataKey] = ErasureMessageKey.ExclusionLifted.ToString();
                return Redirect(SignInRoutes.Erasure);

            case ExclusionLiftingOutcome.NoExclusionForEmail:
                return View(nameof(Index), await PageAsync(null, NoSearch, new LiftBlock(email, null, true), cancellationToken));

            default:
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return View(
                    nameof(Index),
                    await PageAsync(null, NoSearch, new LiftBlock(email, result.FieldError, false), cancellationToken));
        }
    }

    private async Task<ErasurePageModel> PageAsync(
        ErasureMessageKey? message,
        SearchBlock searchBlock,
        LiftBlock liftBlock,
        CancellationToken cancellationToken)
    {
        var mode = await legitimacyMode.ExecuteAsync(cancellationToken);
        return new ErasurePageModel(mode.IsReadOnly, message, searchBlock, liftBlock);
    }

    /// <summary>Api-design §2.6: a positive internal id; anything else is the ordinary 404.</summary>
    private static bool TryParseId(string participantId, out long id) =>
        long.TryParse(participantId, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

    private long ActorId() =>
        long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
}
