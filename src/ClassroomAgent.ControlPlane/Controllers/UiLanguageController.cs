using ClassroomAgent.ControlPlane.Security;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The Owner's own language choice (US-039 openapi <c>POST /account/language</c>, host ClassroomAgent.ControlPlane;
/// spec FR-003, FR-005, FR-006). The request names no account: the target is the session's Owner (AC-004). POST
/// only, with the antiforgery token; there is no GET endpoint (API-4).
/// </summary>
[Authorize(Policy = OwnerSession.OwnerPolicy)]
public sealed class UiLanguageController(OwnerSessionService sessions) : Controller
{
    /// <summary>VR-002: the longest return path that is used at all.</summary>
    public const int MaxReturnPathLength = 2048;

    [HttpPost("account/language")]
    public async Task<IActionResult> Choose(
        [FromForm(Name = "language")] string? language,
        [FromForm(Name = "returnPath")] string? returnPath,
        CancellationToken cancellationToken)
    {
        var chosen = OwnerSession.OwnerId(User) is { } ownerId
            ? await sessions.ChooseLanguageAsync(ownerId, language, cancellationToken)
            : null;

        // I-6: a refused code is a tampered or stale form — the antiforgery refusal's page and text. The value is
        // neither echoed nor logged (SC-10).
        if (chosen is null)
        {
            return ErrorController.Page(HttpContext, StatusCodes.Status400BadRequest, "/");
        }

        await OwnerSession.ReissueWithLanguageAsync(HttpContext, chosen);
        return Redirect(LocalReturnPath(returnPath));
    }

    /// <summary>VR-002, AC-010: only a local application path is followed; anything else lands on the home page.</summary>
    private string LocalReturnPath(string? returnPath) =>
        returnPath is { Length: > 0 and <= MaxReturnPathLength } && Url.IsLocalUrl(returnPath)
            ? returnPath
            : "/";
}
