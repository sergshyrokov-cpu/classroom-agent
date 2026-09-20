namespace ClassroomAgent.Web.Security;

/// <summary>
/// Keeps the Google OAuth callback a <c>GET</c> (US-008 security review F-3; SC-4). The authentication handler
/// claims its callback path for <em>any</em> method and reads the code and <c>state</c> from the query string, so
/// without this a <c>POST</c> would reach the one writing path of the sign-in — and, not being an MVC endpoint, it
/// would carry no antiforgery token either.
/// </summary>
/// <remarks>
/// The substantive protection was never the method: the request still needs a <c>state</c> matching the correlation
/// cookie the visitor's own browser holds. What this restores is the rule as written — the callback is the only
/// <c>GET</c> that writes (<c>trebovaniya.md</c> §8, v64), and every other state-changing request carries a token.
/// </remarks>
public sealed class CallbackMethodMiddleware(RequestDelegate next)
{
    private static readonly string[] Allowed = [HttpMethods.Get, HttpMethods.Head];

    public Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(SignInRoutes.Callback, StringComparison.OrdinalIgnoreCase)
            || Allowed.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            return next(context);
        }

        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        return Task.CompletedTask;
    }
}
