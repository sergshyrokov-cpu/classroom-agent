using System.Globalization;
using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// While a Dean's password is still the temporary one an Admin typed, the session may reach the forced change
/// form and nothing else: every other request of that session is sent back to it (US-012 spec FR-006,
/// api-design §2.6). Signing out stays reachable, so nobody is trapped in the form, and so does choosing a language
/// (US-039 OD-007, spec FR-012): the re-issued session keeps the temporary claim, so the next request lands here again.
/// </summary>
public sealed class TemporaryPasswordMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true
            || user.FindFirst(InstallationClaimTypes.PasswordIsTemporary) is null)
        {
            return next(context);
        }

        var path = context.Request.Path;
        if (path.StartsWithSegments(SignInRoutes.ForcedPasswordChange, StringComparison.Ordinal)
            || path.StartsWithSegments(SignInRoutes.SignOut, StringComparison.Ordinal)
            || path.StartsWithSegments(SignInRoutes.Language, StringComparison.Ordinal)
            || path.StartsWithSegments(SignInRoutes.SignInPage, StringComparison.Ordinal))
        {
            return next(context);
        }

        if (ApiErrorResponse.IsApi(path))
        {
            return RefuseApiAsync(context);
        }

        context.Response.Redirect(SignInRoutes.ForcedPasswordChange);
        return Task.CompletedTask;
    }

    /// <summary>
    /// US-028 api-design §2.5: a script under <c>/api/v1</c> gets <c>403</c> with the API-6 body instead of a redirect.
    /// This middleware runs before request localization, so the message is resolved in the account's own language the
    /// way <see cref="AccountCultureProvider"/> would decide it.
    /// </summary>
    private static async Task RefuseApiAsync(HttpContext context)
    {
        var language = context.User.FindFirst(InstallationClaimTypes.UiLanguage)?.Value
            ?? InstallationSession.LanguageCode(context.RequestServices.GetRequiredService<SchoolDefaults>().UiLanguage);
        var culture = CultureInfo.GetCultureInfo(language);
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            await ApiErrorResponse.WriteAsync(
                context,
                ApiErrorResponse.Create(context, StatusCodes.Status403Forbidden, JournalExportTextKeys.PasswordChangeRequired),
                context.RequestAborted);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }
}
