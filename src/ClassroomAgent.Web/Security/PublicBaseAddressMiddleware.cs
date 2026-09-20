namespace ClassroomAgent.Web.Security;

/// <summary>
/// Makes the OAuth redirect URI come from the <b>configured</b> public base address rather than from the incoming
/// request (US-008 spec FR-001, VR-001; <c>trebovaniya.md</c> v78). It rewrites the scheme and host of the two
/// OAuth paths, so the value the handler sends with the authorization request and the value it repeats at the code
/// exchange are the same one, and neither can be forged through <c>Host</c> or <c>X-Forwarded-*</c> behind a
/// reverse proxy.
/// </summary>
/// <remarks>
/// It runs after HTTPS redirection, so the redirect to HTTPS still happens on its own terms (SC-2), and it touches
/// only the sign-in start and the callback: every other link keeps the request's own host.
/// </remarks>
public sealed class PublicBaseAddressMiddleware(RequestDelegate next, Uri publicBaseAddress)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(SignInRoutes.Start, StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments(SignInRoutes.Callback, StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Scheme = publicBaseAddress.Scheme;
            context.Request.Host = publicBaseAddress.IsDefaultPort
                ? new HostString(publicBaseAddress.Host)
                : new HostString(publicBaseAddress.Host, publicBaseAddress.Port);
        }

        return next(context);
    }
}
