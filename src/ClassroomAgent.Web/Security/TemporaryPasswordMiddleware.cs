using ClassroomAgent.Web.Security;

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

        context.Response.Redirect(SignInRoutes.ForcedPasswordChange);
        return Task.CompletedTask;
    }
}
