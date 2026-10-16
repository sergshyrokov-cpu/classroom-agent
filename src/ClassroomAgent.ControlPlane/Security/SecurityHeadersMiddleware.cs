namespace ClassroomAgent.ControlPlane.Security;

/// <summary>
/// The one host-wide rule of US-050 (SC-15, <c>trebovaniya.md</c> §8 v95): every response of the Control Plane —
/// static files, the error page and the setup gate included — carries the four browser security headers. Registered
/// first in the pipeline; the headers are written when the response starts, so redirects, refusals and the error
/// page written further down are covered too (spec FR-002).
/// </summary>
/// <remarks>
/// Each header is set, not appended: antiforgery's <c>X-Frame-Options: SAMEORIGIN</c> is replaced by <c>DENY</c>, so
/// every header occurs exactly once (spec FR-005, I-4). No form target outside the host (spec FR-004).
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; "
        + "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            Apply(context.Response.Headers);
            return Task.CompletedTask;
        });
        return next(context);
    }

    private static void Apply(IHeaderDictionary headers)
    {
        headers.XFrameOptions = "DENY";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "same-origin";
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
    }
}
