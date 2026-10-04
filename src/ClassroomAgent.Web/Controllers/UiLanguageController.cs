using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The signed-in Admin's or Dean's own language choice (US-039 openapi <c>POST /account/language</c>; spec FR-003,
/// FR-005, FR-006). The request names no account: the target is always the session's own (AC-004). A choice is
/// stored, the session re-issued with the new language, and the browser sent back to the page it came from.
/// </summary>
public sealed class UiLanguageController(ChooseUiLanguageUseCase chooseLanguage) : Controller
{
    /// <summary>VR-002: the longest return path that is used at all.</summary>
    public const int MaxReturnPathLength = 2048;

    /// <summary>
    /// US-025 spec VR-005: a page whose query must not be echoed sets its own return path under this ViewData key;
    /// the switcher otherwise returns to the request's path and query (US-039).
    /// </summary>
    public const string ReturnPathViewDataKey = "LanguageReturnPath";

    [HttpPost(SignInRoutes.Language)]
    [Authorize(Policy = InstallationPolicies.AuthenticatedUser)]
    public async Task<IActionResult> Choose(
        [FromForm(Name = "language")] string? language,
        [FromForm(Name = "returnPath")] string? returnPath,
        CancellationToken cancellationToken)
    {
        var chosen = InstallationSession.AccountId(User) is { } accountId
            ? await chooseLanguage.ExecuteAsync(accountId, language, cancellationToken)
            : null;

        // I-6: the switcher never sends anything but uk or en, so a refusal here is a tampered or stale form — the
        // same page and text as a refused antiforgery token. The value is neither echoed nor logged (SC-10).
        if (chosen is not { } stored)
        {
            return ErrorController.Page(HttpContext, StatusCodes.Status400BadRequest, SignInRoutes.Landing);
        }

        await InstallationSession.ReissueWithLanguageAsync(HttpContext, stored);
        return Redirect(LocalReturnPath(returnPath));
    }

    /// <summary>VR-002, AC-010: only a local application path is followed; anything else lands on the landing page.</summary>
    private string LocalReturnPath(string? returnPath) =>
        returnPath is { Length: > 0 and <= MaxReturnPathLength } && Url.IsLocalUrl(returnPath)
            ? returnPath
            : SignInRoutes.Landing;
}
